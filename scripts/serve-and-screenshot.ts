#!/usr/bin/env tsx
/**
 * Serve HTML and Capture Screenshots
 *
 * Issue #2965: Screenshot per conferma applicazione nuovo stile
 *
 * Serves HTML mockup via HTTP and captures screenshots using Playwright.
 *
 * Usage:
 *   pnpm tsx scripts/serve-and-screenshot.ts
 */

import { chromium } from '@playwright/test';
import { createServer } from 'http';
import { readFileSync } from 'fs';
import { resolve } from 'path';

const PORT = 9876;
const MOCKUP_FILE = resolve(__dirname, '../docs/design-proposals/meepleai-style/admin-dashboard-dark.html');
const OUTPUT_DIR = resolve(__dirname, '../docs/design-proposals/meepleai-style/screenshots/mockup');

const VIEWPORTS = [
  { name: 'desktop', width: 1920, height: 1080 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'mobile', width: 375, height: 812 },
] as const;

async function main() {
  console.log('🚀 Starting screenshot capture with embedded server...\n');

  // Create HTTP server to serve mockup
  const server = createServer((req, res) => {
    try {
      const html = readFileSync(MOCKUP_FILE, 'utf-8');
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
      res.end(html);
    } catch (error) {
      res.writeHead(500);
      res.end('Error loading mockup');
    }
  });

  await new Promise<void>((resolve) => {
    server.listen(PORT, () => {
      console.log(`🌐 HTTP server started on http://localhost:${PORT}`);
      resolve();
    });
  });

  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  try {
    const url = `http://localhost:${PORT}`;
    console.log(`📄 Loading mockup from: ${url}\n`);

    await page.goto(url, { waitUntil: 'networkidle' });

    // Wait for fonts to load
    await page.waitForTimeout(2000);

    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);

    for (const viewport of VIEWPORTS) {
      console.log(`📸 Capturing ${viewport.name} (${viewport.width}x${viewport.height})...`);

      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.waitForTimeout(500);

      const filename = `admin-dashboard-dark-${viewport.name}-${timestamp}.png`;
      const filepath = resolve(OUTPUT_DIR, filename);

      await page.screenshot({ path: filepath, fullPage: true });

      console.log(`✅ Saved: ${filename}`);
    }

    console.log(`\n🎉 Screenshot capture completed!`);
    console.log(`📁 Output: ${OUTPUT_DIR}`);

  } finally {
    await browser.close();
    server.close();
    console.log('🛑 Server stopped');
  }
}

main().catch((error) => {
  console.error('❌ Error:', error);
  process.exit(1);
});
