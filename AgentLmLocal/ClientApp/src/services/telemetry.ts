import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { Resource } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } from '@opentelemetry/semantic-conventions';
import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { trace, Tracer } from '@opentelemetry/api';
import { ZoneContextManager } from '@opentelemetry/context-zone';
import { registerInstrumentations } from '@opentelemetry/instrumentation';
import { getWebAutoInstrumentations } from '@opentelemetry/auto-instrumentations-web';
import type { OtelExporterConfig } from '../types';
import packageJson from '../../package.json';

class TelemetryService {
  private tracer: Tracer | null = null;
  private provider: WebTracerProvider | null = null;
  private isInitialized = false;
  private lastExportTime: Date | null = null;

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
        [ATTR_SERVICE_VERSION]: packageJson.version,
        'deployment.environment': import.meta.env.MODE,
        'browser.user_agent': navigator.userAgent,
        'browser.language': navigator.language,
      })
    );

    // Create OTLP exporter pointing to our proxy endpoint
    const baseExporter = new OTLPTraceExporter({
      url: config.url,
      headers: {
        'Content-Type': 'application/json',
        ...config.headers,
      },
    });

    // Wrap exporter to track last export time
    const exporter = {
      export: (spans: any, resultCallback: any) => {
        this.lastExportTime = new Date();
        return baseExporter.export(spans, resultCallback);
      },
      shutdown: () => baseExporter.shutdown(),
    };

    // Create tracer provider
    this.provider = new WebTracerProvider({
      resource: resource,
    });

    // Add batch span processor with the exporter
    // Tuned for browser context: smaller batches, faster export to prevent data loss on tab close
    this.provider.addSpanProcessor(
      new BatchSpanProcessor(exporter, {
        maxQueueSize: 256,         // Reasonable queue size for browser
        maxExportBatchSize: 50,    // Efficient batch size without over-batching
        scheduledDelayMillis: 2000, // 2s delay - safer for browsers (users close tabs quickly)
      })
    );

    // Register the provider with ZoneContextManager for better async context propagation
    this.provider.register({
      contextManager: new ZoneContextManager(),
    });

    // Get tracer instance
    this.tracer = trace.getTracer('ai-chat-frontend', packageJson.version);

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

  getLastExportTime(): Date | null {
    return this.lastExportTime;
  }

  recordUserInput(inputLength: number): void {
    const span = this.getTracer().startSpan('user.input');
    span.setAttribute('input.length', inputLength);
    span.setAttribute('input.timestamp', Date.now());
    span.end();
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
