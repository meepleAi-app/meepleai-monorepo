#!/usr/bin/env node
/**
 * Static file server for `admin-mockups/design_files/`.
 * Used by Playwright `mockup-baseline` project to capture visual regression
 * baselines from Claude Design mockups.
 *
 * Issue #571 — V2 Phase 0 visual regression baseline.
 *
 * Usage:
 *   node scripts/serve-mockups.cjs               # default port 5174
 *   MOCKUP_PORT=5175 node scripts/serve-mockups.cjs
 */
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');

const PORT = Number(process.env.MOCKUP_PORT ?? 5174);
const ROOT = path.resolve(__dirname, '../../../admin-mockups/design_files');

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.jsx': 'text/babel; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.json': 'application/json; charset=utf-8',
};

if (!fs.existsSync(ROOT)) {
  console.error(`[serve-mockups] root directory not found: ${ROOT}`);
  process.exit(1);
}

const server = http.createServer((req, res) => {
  // Strip query string + decode
  const urlPath = decodeURIComponent((req.url ?? '/').split('?')[0]);
  const relative = urlPath === '/' ? '/index.html' : urlPath;
  // path.join + startsWith(ROOT) is the authoritative guard against traversal:
  // it resolves `..`, `%2E%2E`, and any encoding combination before checking
  // containment. A separate string-level `..` check would be redundant and
  // could mislead readers about which layer enforces safety.
  const filePath = path.join(ROOT, relative);
  if (!filePath.startsWith(ROOT + path.sep) && filePath !== ROOT) {
    res.writeHead(403, { 'content-type': 'text/plain' });
    res.end('Forbidden');
    return;
  }
  fs.stat(filePath, (err, stat) => {
    if (err || !stat.isFile()) {
      res.writeHead(404, { 'content-type': 'text/plain' });
      res.end('Not Found');
      return;
    }
    const mime = MIME[path.extname(filePath).toLowerCase()] ?? 'application/octet-stream';
    res.writeHead(200, {
      'content-type': mime,
      'cache-control': 'no-store',
      // Permissive CORS for unpkg.com fetches in mockup pages (no-op for same-origin)
      'access-control-allow-origin': '*',
    });
    fs.createReadStream(filePath).pipe(res);
  });
});

server.listen(PORT, () => {
  // eslint-disable-next-line no-console -- intended log for Playwright webServer readiness
  console.log(`[serve-mockups] http://localhost:${PORT} → ${ROOT}`);
});
