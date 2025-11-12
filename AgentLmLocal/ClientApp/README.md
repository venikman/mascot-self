# AI Chat Frontend with OpenTelemetry

React + TypeScript frontend application with OpenTelemetry integration.

## Tech Stack

- **React 18** - UI framework
- **TypeScript** - Type safety
- **Vite** - Build tool
- **Bun** - Package manager
- **OpenTelemetry Web SDK** - Telemetry collection
- **AI SDK UI Utils** - Vercel AI SDK utilities

## Getting Started

### Prerequisites

- [Bun](https://bun.sh/) installed
- Backend server running on `http://localhost:5000`

### Installation

```bash
cd AgentLmLocal/ClientApp
bun install
```

### Development

```bash
# Start development server with hot reload
bun run dev
```

This will start the Vite dev server on `http://localhost:5173` with proxy configured to forward API calls to the backend.

### Build for Production

```bash
# Build and output to ../wwwroot
bun run build
```

The production build will be placed in `../wwwroot` so the .NET backend can serve it as static files.

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

### Automatic Instrumentation

The app automatically instruments:

- **Page Load**: Initial page load timing
- **User Input**: Text input events
- **API Calls**: HTTP requests to `/chat` endpoint
- **UI Updates**: React component rendering
- **Visibility Changes**: Tab focus/blur events
- **Error Tracking**: Unhandled errors

### Custom Spans

Use the `telemetryService` to create custom spans:

```typescript
import { telemetryService } from './services/telemetry';

// Simple span
const span = telemetryService.startSpan('my.operation', {
  'custom.attribute': 'value',
});
// ... do work
span.end();
telemetryService.incrementSpanCount();

// Span with automatic error handling
const result = telemetryService.withSpan('my.operation', (span) => {
  span.setAttribute('custom.attribute', 'value');
  return doWork();
});
```

### Using the Hook

```typescript
import { useOpenTelemetry } from './hooks/useOpenTelemetry';

function MyComponent() {
  const { isActive, spanCount, lastError } = useOpenTelemetry();

  return (
    <div>
      Status: {isActive ? 'Active' : 'Inactive'}
      Spans: {spanCount}
    </div>
  );
}
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

Edit `src/services/telemetry.ts` to customize:

- Service name and version
- Resource attributes
- Exporter URL
- Batch processor settings

### API Base URL

The Vite proxy is configured in `vite.config.ts`. For production, the app uses relative URLs.

## Development Tips

### Type Safety

All components and services use TypeScript for type safety. Types are defined in `src/types/index.ts`.

### Hot Reload

Vite provides instant hot module replacement (HMR) during development. Changes to components update immediately without full page reload.

### Debugging

1. Open browser DevTools
2. Check Console for OpenTelemetry initialization logs
3. Check Network tab for `/otel/traces` requests
4. Use React DevTools for component inspection

## Building for Production

When building for production:

1. Run `bun run build`
2. Files are output to `../wwwroot`
3. .NET backend serves these files at the root URL
4. All API calls use relative URLs

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
