# OpenTelemetry Configuration

Quick reference for configuring frontend OpenTelemetry auto-instrumentation.

## Configuration Interface

```typescript
interface OtelExporterConfig {
  url: string;                    // Required: OTLP endpoint URL
  headers?: Record<string, string>; // Optional: Custom HTTP headers

  batchSettings?: {
    maxQueueSize?: number;          // Default: 256
    maxExportBatchSize?: number;    // Default: 50
    scheduledDelayMillis?: number;  // Default: 2000 (2s)
  };

  instrumentations?: {
    documentLoad?: boolean;    // Default: true - Page load timing
    userInteraction?: boolean; // Default: true - Click tracking
    fetch?: boolean;          // Default: true - HTTP fetch() calls
    xhr?: boolean;            // Default: true - XMLHttpRequest calls
  };

  resourceAttributes?: {
    serviceName?: string;      // Default: 'ai-chat-frontend'
    serviceVersion?: string;   // Default: from package.json
    environment?: string;      // Default: process.env.NODE_ENV
    [key: string]: string | undefined; // Custom attributes
  };
}
```

## Basic Usage

**Default configuration** (recommended for prototypes):
```typescript
import { telemetryService } from './services/telemetry';

telemetryService.initialize({
  url: '/otel/traces'
});
```

## Custom Configuration Example

```typescript
telemetryService.initialize({
  url: '/otel/traces',

  // Tune batching for browser apps
  batchSettings: {
    maxQueueSize: 256,           // Queue size
    maxExportBatchSize: 50,      // Batch size
    scheduledDelayMillis: 2000,  // 2s delay (prevents data loss on tab close)
  },

  // Disable instrumentations you don't need
  instrumentations: {
    documentLoad: true,
    userInteraction: true,
    fetch: true,              // Keep for API monitoring
    xhr: false,               // Disable if not using XMLHttpRequest
  },

  // Add custom metadata
  resourceAttributes: {
    serviceName: 'my-app',
    environment: 'production',
    'tenant.id': getTenantId(),
  }
});
```

## Configuration Tips

**Batch Settings:**
- Browser apps: Keep `scheduledDelayMillis: 2000` (2s) to prevent data loss when users close tabs
- High volume: Increase `maxExportBatchSize` to 100
- Low latency: Decrease `scheduledDelayMillis` to 500ms

**Instrumentations:**
- Disable `userInteraction` if you only care about API calls
- Disable `xhr` if your app only uses `fetch()`
- Keep `fetch` enabled for API monitoring

**Resource Attributes:**
- Always set `environment` (dev/staging/prod)
- Add custom attributes for tenant/user segmentation

## Troubleshooting

**Spans not appearing:**
- Check network tab for POST requests to `/otel/traces`
- Spans batch before export (wait 2s or trigger activity)

**High memory usage:**
- Reduce `maxQueueSize` to 100
- Disable unused instrumentations

## Auto-Instrumentation

This app uses **automatic instrumentation** - no manual span creation needed!

Automatically traced:
- ✅ All `fetch()` HTTP requests
- ✅ All XMLHttpRequest calls
- ✅ Document load and page navigation
- ✅ User interactions (clicks, form submissions)
- ✅ Errors and exceptions
- ✅ Async context propagation

**You only need manual spans for custom business logic.**

Example:
```typescript
// Custom business event (one of the few manual spans needed)
telemetryService.recordUserInput(messageLength);
```
