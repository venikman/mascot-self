# AI Chat Frontend with OpenTelemetry

React + TypeScript frontend application with OpenTelemetry integration, built entirely with Bun.

## Tech Stack

- **React 18** - UI framework
- **TypeScript** - Type safety
- **Bun** - Runtime, package manager, bundler, and dev server (all-in-one!)
- **OpenTelemetry Web SDK** - Telemetry collection
- **AI SDK UI Utils** - Vercel AI SDK utilities

## Why Bun?

This project uses **Bun for everything**:
- ✅ **No Vite, No Webpack, No additional bundlers**
- ✅ **Native TypeScript/TSX support** - runs directly without transpilation
- ✅ **Built-in bundler** - faster than Webpack, Rollup, or Parcel
- ✅ **Built-in dev server** - with proxy support
- ✅ **3x faster** installs than npm
- ✅ **All-in-one toolchain** - simpler setup

## Getting Started

### Prerequisites

- [Bun](https://bun.sh/) v1.0.0 or higher
- Backend server running on `http://localhost:5000`

### Installation

```bash
cd AgentLmLocal/ClientApp
bun install
```

### Development

```bash
# Start Bun dev server with hot reload
bun run dev
```

This starts Bun's HTTP server on `http://localhost:5173`:
- ⚡ Instant TypeScript/TSX transpilation
- 🔄 Proxies `/otel` and `/chat` to backend
- 🔥 Hot reload on file changes
- 📦 On-the-fly bundling

### Build for Production

```bash
# Build with Bun's native bundler
bun run build
```

The production build:
- Outputs to `../wwwroot` for .NET backend serving
- Minifies and optimizes code
- Generates source maps
- Creates content-hashed filenames for caching
- Uses code splitting for optimal loading

### Preview Production Build

```bash
# Preview the production build locally
bun run preview
```

## Project Structure

```
src/
├── components/
│   ├── ChatContainer.tsx    # Main chat UI container
│   ├── ChatInput.tsx         # Message input form
│   ├── ChatMessage.tsx       # Individual message display
│   └── TelemetryStatus.tsx   # Telemetry status indicator
├── hooks/
│   ├── useChat.ts            # Chat functionality hook
│   └── useOpenTelemetry.ts   # OpenTelemetry initialization hook
├── services/
│   ├── chatApi.ts            # Chat API client with OTEL tracing
│   └── telemetry.ts          # OpenTelemetry service (singleton)
├── types/
│   └── index.ts              # TypeScript type definitions
├── App.tsx                   # Root component
├── App.css                   # Global styles
└── main.tsx                  # Entry point
```

## OpenTelemetry Integration

This project uses **OpenTelemetry auto-instrumentation** for comprehensive, zero-maintenance telemetry.

### Automatic Instrumentation

The app automatically instruments:

- **Document Load**: Page load timing and resource loading
- **User Interactions**: Clicks, form submissions, and other DOM events
- **Fetch API**: All HTTP requests made with `fetch()`
- **XMLHttpRequest**: Legacy AJAX requests
- **Errors**: Unhandled exceptions with stack traces
- **Async Context**: Proper trace propagation through promises

All of this happens automatically with zero manual code!

### Custom Business Events

Only one custom span is manually created for domain-specific logic:

```typescript
import { telemetryService } from './services/telemetry';

// Record user input event with metadata
telemetryService.recordUserInput(messageLength);
```

**Everything else is auto-instrumented** - no manual span creation needed!

### Using the Hook

```typescript
import { useOpenTelemetry } from './hooks/useOpenTelemetry';

function MyComponent() {
  const { isActive, lastExportTime, lastError } = useOpenTelemetry();

  return (
    <div>
      Status: {isActive ? 'Active (Auto-instrumented)' : 'Inactive'}
      Last Export: {lastExportTime ? '2s ago' : 'No exports yet'}
    </div>
  );
}
```

### Configuration

For advanced configuration options, see **[TELEMETRY_CONFIG.md](./TELEMETRY_CONFIG.md)**

Available configurations:
- **Batch Settings**: Tune performance (queue size, batch size, delay)
- **Instrumentations**: Enable/disable specific auto-instrumentations
- **Resource Attributes**: Add custom metadata (tenant ID, region, etc.)
- **HTTP Headers**: Add authentication or API keys

Example:
```typescript
telemetryService.initialize({
  url: '/otel/traces',
  batchSettings: {
    maxExportBatchSize: 100,
    scheduledDelayMillis: 5000,
  },
  resourceAttributes: {
    'tenant.id': getTenantId(),
  }
});
```

## API Integration

### Chat Endpoint

The app calls `POST /chat` with the following format:

**Request:**
```json
{
  "message": "Hello, AI!"
}
```

**Response:**
```json
{
  "message": "Hello! How can I help you?"
}
```

### Telemetry Endpoint

OpenTelemetry data is automatically sent to `POST /otel/traces` in OTLP/HTTP JSON format.

## Configuration

### Telemetry Configuration

Initialize with configuration options at startup:

```typescript
telemetryService.initialize({
  url: '/otel/traces',
  // ... see TELEMETRY_CONFIG.md for all options
});
```

See **[TELEMETRY_CONFIG.md](./TELEMETRY_CONFIG.md)** for complete documentation on:
- Batch processor tuning
- Selective instrumentation
- Custom resource attributes
- Production configurations
- Performance optimization

### Dev Server Configuration

The dev server is configured in `server.ts`:
- Port: 5173 (default)
- Backend proxy: http://localhost:5000
- Proxied paths: `/otel`, `/chat`

For production, the app uses relative URLs.

### Build Configuration

The build process is configured in `build.ts`:
- Entry point: `src/main.tsx`
- Output directory: `../wwwroot`
- Minification: enabled
- Source maps: external
- Code splitting: enabled

## Development Tips

### Type Safety

All components and services use TypeScript for type safety. Types are defined in `src/types/index.ts`.

### Hot Reload

Bun's dev server automatically transpiles TypeScript/TSX on-the-fly. Simply refresh the browser to see changes (or use browser auto-refresh extensions).

### Debugging

1. Open browser DevTools
2. Check Console for OpenTelemetry initialization logs
3. Check Network tab for `/otel/traces` requests
4. Use React DevTools for component inspection
5. Source maps are available for debugging

## Building for Production

When building for production:

1. Run `bun run build`
2. Bun's bundler optimizes and minifies code
3. Files are output to `../wwwroot`
4. .NET backend serves these files at the root URL
5. All API calls use relative URLs
6. Content-hashed filenames for optimal caching

## Troubleshooting

### Bun Installation Issues

If `bun install` fails, try:
```bash
bun install --force
```

### Port Already in Use

If port 5173 is in use:
```bash
# Edit vite.config.ts and change the port
server: {
  port: 3000, // or any other available port
}
```

### Build Errors

Clear the cache and rebuild:
```bash
rm -rf node_modules dist ../wwwroot
bun install
bun run build
```

### TypeScript Errors

Check your TypeScript version:
```bash
bun run tsc --version
```

Ensure it's 5.6+. Update if needed:
```bash
bun add -d typescript@latest
```

## License

Same as parent project.
