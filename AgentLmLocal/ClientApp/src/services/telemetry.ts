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

    // Merge configuration with defaults
    const serviceName = config.resourceAttributes?.serviceName ?? 'ai-chat-frontend';
    const serviceVersion = config.resourceAttributes?.serviceVersion ?? packageJson.version;
    const environment = config.resourceAttributes?.environment ?? import.meta.env.MODE;

    // Create resource with service metadata
    const resourceAttrs: Record<string, string> = {
      [ATTR_SERVICE_NAME]: serviceName,
      [ATTR_SERVICE_VERSION]: serviceVersion,
      'deployment.environment': environment,
      'browser.user_agent': navigator.userAgent,
      'browser.language': navigator.language,
    };

    // Add custom resource attributes if provided
    if (config.resourceAttributes) {
      Object.entries(config.resourceAttributes).forEach(([key, value]) => {
        if (key !== 'serviceName' && key !== 'serviceVersion' && key !== 'environment' && value !== undefined) {
          resourceAttrs[key] = value;
        }
      });
    }

    const resource = Resource.default().merge(new Resource(resourceAttrs));

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
    const batchSettings = {
      maxQueueSize: config.batchSettings?.maxQueueSize ?? 256,
      maxExportBatchSize: config.batchSettings?.maxExportBatchSize ?? 50,
      scheduledDelayMillis: config.batchSettings?.scheduledDelayMillis ?? 2000,
    };

    this.provider.addSpanProcessor(new BatchSpanProcessor(exporter, batchSettings));

    // Register the provider with ZoneContextManager for better async context propagation
    this.provider.register({
      contextManager: new ZoneContextManager(),
    });

    // Get tracer instance
    this.tracer = trace.getTracer(serviceName, serviceVersion);

    // Register auto-instrumentations for automatic infrastructure tracing
    // Use configuration or enable all by default
    const instrumentationConfig = config.instrumentations ?? {
      documentLoad: true,
      userInteraction: true,
      fetch: true,
      xhr: true,
    };

    const autoInstrumentations: Record<string, any> = {};

    if (instrumentationConfig.documentLoad !== false) {
      autoInstrumentations['@opentelemetry/instrumentation-document-load'] = {};
    }
    if (instrumentationConfig.userInteraction !== false) {
      autoInstrumentations['@opentelemetry/instrumentation-user-interaction'] = {};
    }
    if (instrumentationConfig.fetch !== false) {
      autoInstrumentations['@opentelemetry/instrumentation-fetch'] = {};
    }
    if (instrumentationConfig.xhr !== false) {
      autoInstrumentations['@opentelemetry/instrumentation-xml-http-request'] = {};
    }

    registerInstrumentations({
      instrumentations: [getWebAutoInstrumentations(autoInstrumentations)],
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
