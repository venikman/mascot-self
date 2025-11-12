import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { Resource } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } from '@opentelemetry/semantic-conventions';
import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { trace, context, Span, Tracer } from '@opentelemetry/api';
import { ZoneContextManager } from '@opentelemetry/context-zone';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { getWebAutoInstrumentations } from '@opentelemetry/auto-instrumentations-web';
import type { OtelExporterConfig } from '../types';

class TelemetryService {
  private tracer: Tracer | null = null;
  private provider: WebTracerProvider | null = null;
  private isInitialized = false;

  initialize(config: OtelExporterConfig = { url: '/otel/traces' }): void {
    if (this.isInitialized) {
      console.warn('OpenTelemetry already initialized');
      return;
    }

    console.log('Initializing OpenTelemetry...');

    // Create resource with service metadata
    const resource = Resource.default().merge(
      new Resource({
        [ATTR_SERVICE_NAME]: 'ai-chat-frontend',
        [ATTR_SERVICE_VERSION]: '1.0.0',
        'deployment.environment': import.meta.env.MODE,
        'browser.user_agent': navigator.userAgent,
        'browser.language': navigator.language,
      })
    );

    // Create OTLP exporter pointing to our proxy endpoint
    const exporter = new OTLPTraceExporter({
      url: config.url,
      headers: {
        'Content-Type': 'application/json',
        ...config.headers,
      },
    });

    // Create tracer provider
    this.provider = new WebTracerProvider({
      resource: resource,
    });

    // Add batch span processor with the exporter
    // Increased limits to collect more data before flushing
    this.provider.addSpanProcessor(
      new BatchSpanProcessor(exporter, {
        maxQueueSize: 2048,        // Increased from 100
        maxExportBatchSize: 512,   // Increased from 10
        scheduledDelayMillis: 5000, // Increased from 500ms to 5s
      })
    );

    // Register the provider with ZoneContextManager for better async context propagation
    this.provider.register({
      contextManager: new ZoneContextManager(),
    });

    // Get tracer instance
    this.tracer = trace.getTracer('ai-chat-frontend', '1.0.0');

    // Register auto-instrumentations for automatic infrastructure tracing
    registerInstrumentations({
      instrumentations: [
        getWebAutoInstrumentations({
          '@opentelemetry/instrumentation-document-load': {},
          '@opentelemetry/instrumentation-user-interaction': {},
          '@opentelemetry/instrumentation-fetch': {},
          '@opentelemetry/instrumentation-xml-http-request': {},
        }),
      ],
    });

    this.isInitialized = true;
    console.log('OpenTelemetry initialized successfully with auto-instrumentation');
  }

  getTracer(): Tracer {
    if (!this.tracer) {
      throw new Error('Telemetry not initialized. Call initialize() first.');
    }
    return this.tracer;
  }

  recordUserInput(inputLength: number): void {
    const span = this.getTracer().startSpan('user.input');
    span.setAttribute('input.length', inputLength);
    span.setAttribute('input.timestamp', Date.now());
    span.end();
  }

  startSpan(name: string, attributes?: Record<string, string | number | boolean>): Span {
    const span = this.getTracer().startSpan(name);

    if (attributes) {
      Object.entries(attributes).forEach(([key, value]) => {
        span.setAttribute(key, value);
      });
    }

    return span;
  }

  withSpan<T>(name: string, fn: (span: Span) => T, attributes?: Record<string, string | number | boolean>): T {
    const span = this.startSpan(name, attributes);
    try {
      const result = fn(span);
      if (result instanceof Promise) {
        return result.then(
          (value) => {
            span.setStatus({ code: 1 }); // OK
            span.end();
            return value;
          },
          (error) => {
            span.setStatus({ code: 2, message: error.message }); // ERROR
            span.recordException(error);
            span.end();
            throw error;
          }
        ) as T;
      } else {
        span.setStatus({ code: 1 }); // OK
        span.end();
        return result;
      }
    } catch (error) {
      span.setStatus({ code: 2, message: (error as Error).message }); // ERROR
      span.recordException(error as Error);
      span.end();
      throw error;
    }
  }

  shutdown(): Promise<void> {
    if (this.provider) {
      // Reset initialization flag to support React StrictMode remounting
      this.isInitialized = false;
      return this.provider.shutdown();
    }
    return Promise.resolve();
  }
}

// Export singleton instance
export const telemetryService = new TelemetryService();
