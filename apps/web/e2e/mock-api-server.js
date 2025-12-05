/**
 * Minimal Mock API Server for E2E Tests
 *
 * Issue #1951: Prevents ECONNREFUSED during Next.js SSR in CI
 *
 * This server responds to ALL requests with empty success responses,
 * allowing Next.js SSR to complete without real API backend.
 *
 * Usage: node e2e/mock-api-server.js
 * Listens on: http://localhost:8081
 */

const http = require('http');

const PORT = process.env.MOCK_API_PORT || 8081;

const server = http.createServer((req, res) => {
  // Log requests in CI for debugging
  if (process.env.CI || process.env.DEBUG) {
    console.log(`[MockAPI] ${req.method} ${req.url}`);
  }

  // Set CORS headers (required for browser requests)
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');

  // Handle OPTIONS preflight
  if (req.method === 'OPTIONS') {
    res.writeHead(204);
    res.end();
    return;
  }

  // Default responses for common endpoints
  res.setHeader('Content-Type', 'application/json');

  if (req.url.includes('/auth/me')) {
    // Unauthenticated response
    res.writeHead(401);
    res.end(JSON.stringify({ error: 'Unauthorized' }));
  } else if (
    req.url.includes('/games') &&
    !req.url.includes('/sessions') &&
    !req.url.includes('/pdfs')
  ) {
    // Games list endpoint - return paginated response structure
    // Issue #1951: Must match GamesClient expected response structure
    res.writeHead(200);
    res.end(
      JSON.stringify({
        games: [],
        total: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
      })
    );
  } else if (req.url.includes('/sessions')) {
    // Game sessions endpoint
    res.writeHead(200);
    res.end(JSON.stringify([]));
  } else if (req.url.includes('/pdfs')) {
    // PDFs endpoint
    res.writeHead(200);
    res.end(JSON.stringify({ pdfs: [] }));
  } else if (req.url.includes('/health')) {
    // Health check
    res.writeHead(200);
    res.end(JSON.stringify({ status: 'healthy' }));
  } else {
    // Generic success for any other endpoint
    res.writeHead(200);
    res.end(JSON.stringify({ data: [] }));
  }
});

server.listen(PORT, () => {
  console.log(`[MockAPI] Server listening on http://localhost:${PORT}`);
  console.log(`[MockAPI] Ready to serve E2E test requests`);
});

// Graceful shutdown
process.on('SIGTERM', () => {
  console.log('[MockAPI] Shutting down gracefully...');
  server.close(() => {
    console.log('[MockAPI] Server closed');
    process.exit(0);
  });
});

process.on('SIGINT', () => {
  console.log('[MockAPI] Interrupted, shutting down...');
  server.close(() => {
    console.log('[MockAPI] Server closed');
    process.exit(0);
  });
});
