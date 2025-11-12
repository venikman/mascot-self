# Frontend OpenTelemetry Example

This document describes the frontend OpenTelemetry integration example implemented in this project.

## Overview

This example demonstrates how to:
1. Collect OpenTelemetry traces from a browser-based frontend application
2. Send trace data to a backend proxy endpoint
3. Log the received telemetry data in JSONL format on the backend

## Available Implementations

This project provides **two frontend implementations**:

### 1. React + TypeScript (Recommended) - `AgentLmLocal/ClientApp/`

**Modern, production-ready implementation:**
- ✅ React 18 with TypeScript
- ✅ Bun for fast package management
- ✅ Vite for instant hot reload
- ✅ Proper npm packages (not CDN)
- ✅ Custom React hooks
- ✅ Component-based architecture
- ✅ Type-safe OpenTelemetry integration

**Best for:**
- Production applications
- Teams using React
- Projects requiring type safety
- Applications with complex UI requirements

**Quick Start:**
```bash
cd AgentLmLocal/ClientApp
bun install
bun run dev  # Development server
bun run build  # Production build
```

See [AgentLmLocal/ClientApp/README.md](../AgentLmLocal/ClientApp/README.md) for complete documentation.

### 2. Vanilla HTML/JS - `AgentLmLocal/wwwroot/` (Reference)

**Simple implementation for learning:**
- ✅ No build tools required
- ✅ CDN-based dependencies
- ✅ Single HTML file
- ✅ Easy to understand

**Best for:**
- Quick prototypes
- Learning OpenTelemetry
- Minimal setup requirements
- Static hosting scenarios

**Note:** The React version is recommended for production use. The vanilla version is kept as a reference implementation.

---

The rest of this document covers concepts common to both implementations, with specific notes where they differ.

## Architecture

```
┌─────────────────┐      OTLP/HTTP (JSON)      ┌──────────────────┐
│                 │ ───────────────────────────> │                  │
│  Web Browser    │                              │  Backend API     │
│  (Frontend)     │      HTTP API Calls          │  (.NET Core)     │
│                 │ <─────────────────────────── │                  │
└─────────────────┘                              └──────────────────┘
                                                          │
                                                          │ Logs to
                                                          ▼
                                                  ┌──────────────────┐
                                                  │ JSONL Output     │
                                                  │ (stdout/file)    │
                                                  └──────────────────┘
```

## Components

### Frontend (`AgentLmLocal/wwwroot/`)

#### `index.html`
- Simple AI chat interface
- Real-time telemetry status indicator
- Span counter showing telemetry activity

#### `styles.css`
- Clean, modern UI styling
- Responsive design
- Visual feedback for telemetry status

#### `app.js`
- OpenTelemetry Web SDK integration via CDN
- Automatic instrumentation of user interactions
- Custom spans for:
  - Page load
  - User input events
  - Chat API requests
  - UI updates
  - Error tracking

### Backend (`AgentLmLocal/`)

#### `Models/OtelModels.cs`
OTLP (OpenTelemetry Protocol) data models:
- `OtelTraceRequest`: Root container for trace data
- `ResourceSpans`: Resource-level span grouping
- `Span`: Individual span data with attributes, events, and status
- Full support for OTLP/HTTP JSON format

#### `Program.cs` - New Endpoints

##### POST `/otel/traces`
**Purpose**: Receives OpenTelemetry trace data from frontend

**Request Format**: OTLP/HTTP JSON
```json
{
  "resourceSpans": [{
    "resource": {
      "attributes": [
        { "key": "service.name", "value": { "stringValue": "ai-chat-frontend" } }
      ]
    },
    "scopeSpans": [{
      "spans": [{
        "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
        "spanId": "00f067aa0ba902b7",
        "name": "chat.interaction",
        "startTimeUnixNano": "1699564800000000000",
        "endTimeUnixNano": "1699564801000000000",
        "attributes": [...]
      }]
    }]
  }]
}
```

**Response**: `{ "status": "ok" }`

**Logging**: Each span is logged in JSONL format with:
- Span name
- Trace ID and Span ID
- Duration in milliseconds
- All span attributes
- Resource attributes

##### POST `/chat`
**Purpose**: AI chat endpoint for the frontend

**Request Format**:
```json
{
  "message": "Hello, how are you?"
}
```

**Response**:
```json
{
  "message": "I'm doing well, thank you for asking!"
}
```

## OpenTelemetry Configuration

### Frontend Configuration

```javascript
// Resource attributes
{
  'service.name': 'ai-chat-frontend',
  'service.version': '1.0.0',
  'deployment.environment': 'development',
  'browser.user_agent': navigator.userAgent,
  'browser.language': navigator.language
}

// Exporter configuration
{
  url: '/otel/traces',  // Backend proxy endpoint
  headers: {
    'Content-Type': 'application/json'
  }
}

// Batch processor settings
{
  maxQueueSize: 100,
  maxExportBatchSize: 10,
  scheduledDelayMillis: 500  // Export every 500ms
}
```

### Instrumented Events

The frontend automatically creates spans for:

1. **Page Load** (`page.load`)
   - Attributes: page URL, page title

2. **User Input** (`user.input`)
   - Attributes: input length, timestamp

3. **Chat Interaction** (`chat.interaction`)
   - Attributes: message content, message length, success status
   - Child spans:
     - `chat.api.request`: HTTP request to /chat endpoint
     - `chat.ui.update`: UI rendering after response

4. **Focus Events** (`user.focus.input`)
   - Tracks user engagement

5. **Visibility Changes** (`page.visibility.change`)
   - Detects when user switches tabs

6. **Errors** (`error.unhandled`)
   - Captures unhandled JavaScript errors

## Running the Example

### Prerequisites

1. .NET 9.0 SDK
2. LM Studio or compatible OpenAI API endpoint
3. Modern web browser with JavaScript enabled

### Setup

1. **Configure the backend** (see main README.md for LLM configuration)

2. **Start the server**:
   ```bash
   cd AgentLmLocal
   dotnet run
   ```

3. **Access the chat interface**:
   Open browser to `http://localhost:5000`

### Viewing Telemetry

#### In the Browser
- Status indicator shows OpenTelemetry connection state
- Span counter increments with each traced event
- Browser console shows telemetry debug information

#### On the Backend
All telemetry is logged to stdout in JSONL format:

```json
{
  "@t": "2025-11-12T12:34:56.789Z",
  "@mt": "Frontend span: {SpanName} | TraceId: {TraceId} | SpanId: {SpanId} | Duration: {DurationMs}ms | Attributes: {Attributes} | Resource: {Resource}",
  "@l": "Information",
  "SpanName": "chat.interaction",
  "TraceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "SpanId": "00f067aa0ba902b7",
  "DurationMs": 1234.56,
  "Attributes": "{\"chat.message\":\"Hello\",\"chat.success\":true}",
  "Resource": "{\"service.name\":\"ai-chat-frontend\",\"service.version\":\"1.0.0\"}"
}
```

## Example Trace Flow

When a user sends a chat message, the following trace is generated:

```
chat.interaction (parent span)
├─ chat.api.request (HTTP call to backend)
└─ chat.ui.update (render response in UI)
```

Each span includes:
- **Timing**: Start time, end time, duration
- **Context**: Trace ID, Span ID, Parent Span ID
- **Attributes**: Custom metadata about the operation
- **Status**: Success/error indication
- **Events**: Additional events within the span (if any)

## Customization

### Adding Custom Spans

```javascript
// In app.js
const span = tracer.startSpan('custom.operation');
span.setAttribute('custom.attribute', 'value');

try {
  // Your operation here
  span.setStatus({ code: 1 }); // OK
} catch (error) {
  span.setStatus({ code: 2, message: error.message }); // ERROR
  span.recordException(error);
} finally {
  span.end();
}
```

### Adding Custom Attributes

```javascript
span.setAttribute('user.id', userId);
span.setAttribute('operation.type', 'search');
span.setAttribute('result.count', results.length);
```

### Modifying Export Behavior

Edit the `BatchSpanProcessor` configuration in `app.js`:

```javascript
provider.addSpanProcessor(new BatchSpanProcessor(exporter, {
  maxQueueSize: 100,           // Max spans to queue
  maxExportBatchSize: 10,      // Spans per batch
  scheduledDelayMillis: 500,   // Export interval
}));
```

## Integration with Backend Tracing

The frontend traces can be correlated with backend traces by:

1. **Propagating Context**: Pass trace context in HTTP headers (W3C Trace Context)
2. **Trace ID Correlation**: Link frontend and backend spans using the same trace ID
3. **Unified Logging**: Both frontend and backend logs use JSONL with trace context

This creates end-to-end visibility across the full request lifecycle.

## Troubleshooting

### No Telemetry Appearing

1. Check browser console for errors
2. Verify `/otel/traces` endpoint is accessible
3. Check network tab for failed requests to `/otel/traces`
4. Ensure backend server is running

### Spans Not Being Exported

1. Check `BatchSpanProcessor` configuration
2. Verify exporter URL is correct
3. Look for CORS issues in browser console
4. Check backend logs for proxy endpoint errors

### High Overhead

1. Reduce span creation frequency
2. Increase `scheduledDelayMillis` in batch processor
3. Decrease `maxExportBatchSize`
4. Add sampling (not implemented in this example)

## Production Considerations

For production deployments:

1. **Sampling**: Implement head-based or tail-based sampling to reduce volume
2. **Security**: Add authentication to `/otel/traces` endpoint
3. **Performance**: Consider using dedicated telemetry backend (Jaeger, Zipkin, etc.)
4. **Privacy**: Sanitize user data in span attributes
5. **Rate Limiting**: Protect the proxy endpoint from abuse
6. **CORS**: Configure appropriate CORS policies if frontend is on different domain

## References

- [OpenTelemetry JavaScript](https://opentelemetry.io/docs/instrumentation/js/)
- [OTLP Specification](https://opentelemetry.io/docs/specs/otlp/)
- [W3C Trace Context](https://www.w3.org/TR/trace-context/)
- [Serilog Structured Logging](https://serilog.net/)
