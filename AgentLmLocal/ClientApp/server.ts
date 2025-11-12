#!/usr/bin/env bun

// Bun dev server with hot reload and proxy
const PORT = parseInt(process.env.PORT || '5173', 10);
const BACKEND_URL = process.env.BACKEND_URL || 'http://localhost:5000';

console.log('🚀 Starting Bun dev server...');

// Bun's built-in dev server with JSX/TSX support
Bun.serve({
  port: PORT,
  async fetch(req) {
    const url = new URL(req.url);

    // Proxy API requests to backend
    if (url.pathname.startsWith('/otel') || url.pathname.startsWith('/chat')) {
      const backendUrl = new URL(url.pathname + url.search, BACKEND_URL);

      try {
        const response = await fetch(backendUrl, {
          method: req.method,
          headers: req.headers,
          body: req.method !== 'GET' && req.method !== 'HEAD' ? await req.text() : undefined,
        });

        return new Response(response.body, {
          status: response.status,
          statusText: response.statusText,
          headers: response.headers,
        });
      } catch (error) {
        console.error('Proxy error:', error);
        return new Response('Backend not available', { status: 503 });
      }
    }

    // Serve static files or SPA routes
    const filePath = url.pathname === '/' ? '/index.html' : url.pathname;

    // Try to serve the file
    const file = Bun.file(import.meta.dir + filePath);
    const exists = await file.exists();

    if (exists) {
      return new Response(file);
    }

    // Handle SPA routing - serve index.html for non-file routes
    if (!filePath.includes('.')) {
      return new Response(Bun.file(import.meta.dir + '/index.html'));
    }

    // Handle module imports with .tsx/.ts/.jsx/.js
    if (filePath.endsWith('.tsx') || filePath.endsWith('.ts') ||
        filePath.endsWith('.jsx') || filePath.endsWith('.js')) {
      const sourceFile = Bun.file(import.meta.dir + filePath);
      const sourceExists = await sourceFile.exists();

      if (sourceExists) {
        // Transpile on the fly
        const transpiled = await Bun.build({
          entrypoints: [import.meta.dir + filePath],
          target: 'browser',
          format: 'esm',
        });

        if (transpiled.success && transpiled.outputs.length > 0) {
          return new Response(transpiled.outputs[0], {
            headers: {
              'Content-Type': 'application/javascript',
            },
          });
        }
      }
    }

    return new Response('Not found', { status: 404 });
  },
  error(error) {
    console.error('Server error:', error);
    return new Response('Internal server error', { status: 500 });
  },
});

console.log(`✅ Dev server running at http://localhost:${PORT}`);
console.log(`🔄 Proxying /otel and /chat to ${BACKEND_URL}`);
console.log('');
console.log('Press Ctrl+C to stop');
