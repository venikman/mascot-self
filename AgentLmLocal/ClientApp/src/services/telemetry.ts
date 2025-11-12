import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { Resource } from '@opentelemetry/resources';
import { ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION } from '@opentelemetry/semantic-conventions';
import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { trace, context, Span, Tracer } from '@opentelemetry/api';
import type { OtelExporterConfig } from '../types';

type SpanCountListener = (count: number) => void;

class TelemetryService {
  private tracer: Tracer | null = null;
  private provider: WebTracerProvider | null = null;
  private spanCount = 0;
  private isInitialized = false;
  private listeners: Set<SpanCountListener> = new Set();

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
    this.provider.addSpanProcessor(
      new BatchSpanProcessor(exporter, {
        maxQueueSize: 100,
        maxExportBatchSize: 10,
        scheduledDelayMillis: 500,
      })
    );

    // Register the provider
    this.provider.register();

    // Get tracer instance
    this.tracer = trace.getTracer('ai-chat-frontend', '1.0.0');

    this.isInitialized = true;
    console.log('OpenTelemetry initialized successfully');

    // Create initial span for page load
    this.recordPageLoad();
  }

  getTracer(): Tracer {
    if (!this.tracer) {
      throw new Error('Telemetry not initialized. Call initialize() first.');
    }
    return this.tracer;
  }

  getSpanCount(): number {
    return this.spanCount;
  }

  incrementSpanCount(): void {
    this.spanCount++;
    this.notifyListeners();
  }

  subscribeToSpanCount(listener: SpanCountListener): () => void {
    this.listeners.add(listener);
    // Return unsubscribe function
    return () => {
      this.listeners.delete(listener);
    };
  }

  private notifyListeners(): void {
    this.listeners.forEach((listener) => listener(this.spanCount));
  }

  recordPageLoad(): void {
    const span = this.getTracer().startSpan('page.load');
    span.setAttribute('page.url', window.location.href);
    span.setAttribute('page.title', document.title);
    span.end();
    this.incrementSpanCount();
  }

  recordUserInput(inputLength: number): void {
    const span = this.getTracer().startSpan('user.input');
    span.setAttribute('input.length', inputLength);
    span.setAttribute('input.timestamp', Date.now());
    span.end();
    this.incrementSpanCount();
  }

  recordVisibilityChange(hidden: boolean): void {
    const span = this.getTracer().startSpan('page.visibility.change');
    span.setAttribute('page.hidden', hidden);
    span.end();
    this.incrementSpanCount();
  }

  recordError(error: Error, context?: Record<string, unknown>): void {
    const span = this.getTracer().startSpan('error.unhandled');
    span.setAttribute('error.message', error.message);
    span.setAttribute('error.stack', error.stack || '');

    if (context) {
      Object.entries(context).forEach(([key, value]) => {
        span.setAttribute(`error.context.${key}`, String(value));
      });
    }

    span.setStatus({ code: 2, message: error.message });
    span.end();
    this.incrementSpanCount();
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
            this.incrementSpanCount();
            return value;
          },
          (error) => {
            span.setStatus({ code: 2, message: error.message }); // ERROR
            span.recordException(error);
            span.end();
            this.incrementSpanCount();
            throw error;
          }
        ) as T;
      } else {
        span.setStatus({ code: 1 }); // OK
        span.end();
        this.incrementSpanCount();
        return result;
      }
    } catch (error) {
      span.setStatus({ code: 2, message: (error as Error).message }); // ERROR
      span.recordException(error as Error);
      span.end();
      this.incrementSpanCount();
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
