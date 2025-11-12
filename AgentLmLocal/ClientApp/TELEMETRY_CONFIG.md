# OpenTelemetry Configuration Guide

This document explains all configuration options available for the frontend OpenTelemetry implementation.

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
    documentLoad?: boolean;    // Default: true
    userInteraction?: boolean; // Default: true
    fetch?: boolean;          // Default: true
    xhr?: boolean;            // Default: true
  };

  resourceAttributes?: {
    serviceName?: string;      // Default: 'ai-chat-frontend'
    serviceVersion?: string;   // Default: from package.json
    environment?: string;      // Default: import.meta.env.MODE
    [key: string]: string | undefined; // Custom attributes
  };
}
```

## Usage Examples

### Basic Usage (Default Configuration)

```typescript
import { telemetryService } from './services/telemetry';

// Uses all defaults
telemetryService.initialize({
  url: '/otel/traces'
});
```

### Custom Batch Settings

Optimize for different scenarios:

**High-Volume Application:**
```typescript
telemetryService.initialize({
  url: '/otel/traces',
  batchSettings: {
    maxQueueSize: 512,           // Larger queue
    maxExportBatchSize: 100,     // Bigger batches
    scheduledDelayMillis: 5000,  // 5s delay
  }
});
```

**Real-Time Analytics (Low Latency):**
```typescript
telemetryService.initialize({
  url: '/otel/traces',
  batchSettings: {
    maxQueueSize: 100,
    maxExportBatchSize: 10,
    scheduledDelayMillis: 500,   // 500ms - faster export
  }
});
```

**Memory-Constrained Devices:**
```typescript
telemetryService.initialize({
  url: '/otel/traces',
  batchSettings: {
    maxQueueSize: 50,            // Smaller queue
    maxExportBatchSize: 10,
    scheduledDelayMillis: 1000,
  }
});
```

### Selective Instrumentation

Disable specific instrumentations to reduce overhead:

**API-Only Tracing (No User Interaction):**
```typescript
telemetryService.initialize({
  url: '/otel/traces',
  instrumentations: {
    documentLoad: true,
    userInteraction: false,  // Disable user interaction tracking
    fetch: true,
    xhr: true,
  }
});
```

**Minimal Instrumentation (Fetch Only):**
```typescript
telemetryService.initialize({
  url: '/otel/traces',
  instrumentations: {
    documentLoad: false,
    userInteraction: false,
    fetch: true,             // Only track HTTP requests
    xhr: false,
  }
});
```

### Custom Resource Attributes

Add custom metadata for better trace identification:

**Multi-Tenant Application:**
```typescript
telemetryService.initialize({
  url: '/otel/traces',
  resourceAttributes: {
    serviceName: 'my-app-frontend',
    serviceVersion: '2.0.0',
    environment: 'production',
    'tenant.id': getTenantId(),
    'app.region': 'us-east-1',
    'deployment.stage': 'blue',
  }
});
```

**Feature Flag Tracking:**
```typescript
telemetryService.initialize({
  url: '/otel/traces',
  resourceAttributes: {
    'feature.new_ui': 'enabled',
    'feature.beta_features': 'disabled',
    'user.segment': 'premium',
  }
});
```

### Production Configuration

Complete production-ready configuration:

```typescript
telemetryService.initialize({
  url: process.env.OTEL_EXPORTER_URL || '/otel/traces',

  headers: {
    'Authorization': `Bearer ${getAuthToken()}`,
    'X-API-Key': process.env.OTEL_API_KEY,
  },

  batchSettings: {
    maxQueueSize: 256,
    maxExportBatchSize: 50,
    scheduledDelayMillis: 2000,
  },

  instrumentations: {
    documentLoad: true,
    userInteraction: true,
    fetch: true,
    xhr: true,
  },

  resourceAttributes: {
    serviceName: 'my-app-frontend',
    serviceVersion: process.env.APP_VERSION,
    environment: process.env.NODE_ENV,
    'deployment.region': process.env.AWS_REGION,
    'deployment.version': process.env.BUILD_NUMBER,
    'team.owner': 'platform-team',
  }
});
```

## Configuration Best Practices

### Batch Settings

- **Browser apps**: Use `scheduledDelayMillis: 2000` (2s) to prevent data loss when users close tabs
- **High-volume**: Increase `maxExportBatchSize` to 100-200 for efficiency
- **Low-latency**: Decrease `scheduledDelayMillis` to 500-1000ms for real-time visibility
- **Memory-constrained**: Reduce `maxQueueSize` to 50-100

### Instrumentations

- Disable `userInteraction` for backend-focused debugging
- Disable `documentLoad` for SPAs that don't navigate
- Keep `fetch` enabled for API monitoring
- Disable `xhr` if your app only uses fetch

### Resource Attributes

- Always set `environment` (dev/staging/prod)
- Include `serviceVersion` for release tracking
- Add deployment metadata for incident correlation
- Use custom attributes for tenant/user segmentation

## Performance Impact

| Instrumentation | Overhead | Use Case |
|----------------|----------|----------|
| documentLoad   | Low      | Page navigation tracking |
| userInteraction| Medium   | Click/interaction analytics |
| fetch          | Low      | API monitoring |
| xhr            | Low      | Legacy AJAX tracking |

## Troubleshooting

### Spans not appearing in backend

1. Check `scheduledDelayMillis` - spans batch before export
2. Verify `maxQueueSize` isn't reached (increases delay)
3. Check network tab for OTLP requests to configured URL

### High memory usage

1. Reduce `maxQueueSize` (e.g., 50-100)
2. Reduce `scheduledDelayMillis` for faster flushing
3. Disable unused instrumentations

### Missing user interactions

1. Ensure `userInteraction: true` in instrumentations
2. Check if elements have event listeners
3. Verify Zone.js context propagation

## Environment Variables

You can externalize configuration using environment variables:

```typescript
telemetryService.initialize({
  url: import.meta.env.VITE_OTEL_URL || '/otel/traces',

  batchSettings: {
    maxQueueSize: parseInt(import.meta.env.VITE_OTEL_QUEUE_SIZE || '256'),
    maxExportBatchSize: parseInt(import.meta.env.VITE_OTEL_BATCH_SIZE || '50'),
    scheduledDelayMillis: parseInt(import.meta.env.VITE_OTEL_DELAY || '2000'),
  },

  resourceAttributes: {
    environment: import.meta.env.MODE,
    serviceVersion: import.meta.env.VITE_APP_VERSION,
  }
});
```

Example `.env` file:

```bash
VITE_OTEL_URL=https://otel-collector.example.com/v1/traces
VITE_OTEL_QUEUE_SIZE=512
VITE_OTEL_BATCH_SIZE=100
VITE_OTEL_DELAY=5000
VITE_APP_VERSION=1.2.3
```

## Advanced: Custom Spans

While auto-instrumentation handles most cases, you can still create custom spans for business logic:

```typescript
import { telemetryService } from './services/telemetry';

// Record custom business event
telemetryService.recordUserInput(messageLength);

// The service only exposes minimal API by design
// Auto-instrumentation handles the rest automatically
```

## Migration from Manual Instrumentation

If migrating from manual span creation:

1. Remove manual HTTP tracing - now auto-instrumented
2. Remove page load tracking - now auto-instrumented
3. Remove error tracking - now auto-instrumented
4. Keep only domain-specific business logic spans

**Before:**
```typescript
// Manual (old way)
const span = tracer.startSpan('fetch');
span.setAttribute('http.url', url);
const response = await fetch(url);
span.end();
```

**After:**
```typescript
// Auto-instrumented (new way)
const response = await fetch(url);  // Automatically traced!
```
