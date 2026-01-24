#!/usr/bin/env tsx
/**
 * Screenshot Generator for Design Mockups
 *
 * Issue #2965: Screenshot per conferma applicazione nuovo stile
 *
 * Generates screenshots of HTML mockup files at multiple viewports
 * for design validation and comparison.
 *
 * Usage:
 *   pnpm tsx scripts/screenshot-mockup.ts
 */

import { chromium, type Browser, type Page } from '@playwright/test';
import { resolve } from 'path';

const MOCKUP_FILE = resolve(__dirname, '../docs/design-proposals/meepleai-style/admin-dashboard-dark.html');
const OUTPUT_DIR = resolve(__dirname, '../docs/design-proposals/meepleai-style/screenshots/mockup');

const VIEWPORTS = [
  { name: 'desktop', width: 1920, height: 1080 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'mobile', width: 375, height: 812 },
] as const;

async function captureScreenshots() {
  console.log('🚀 Starting mockup screenshot capture...\n');

  let browser: Browser | null = null;
  let page: Page | null = null;

  try {
    // Launch browser
    console.log('📦 Launching Chromium...');
    browser = await chromium.launch({ headless: true });
    page = await browser.newPage();

    // Load mockup file
    const fileUrl = `file://${MOCKUP_FILE}`;
    console.log(`📄 Loading mockup: ${fileUrl}`);
    await page.goto(fileUrl, { waitUntil: 'networkidle' });

    // Wait for fonts to load (Google Fonts)
    await page.waitForTimeout(2000);

    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);

    // Capture at each viewport
    for (const viewport of VIEWPORTS) {
      console.log(`\n📸 Capturing ${viewport.name} (${viewport.width}x${viewport.height})...`);

      await page.setViewportSize({
        width: viewport.width,
        height: viewport.height
      });

      // Wait for animations/transitions
      await page.waitForTimeout(500);

      const filename = `admin-dashboard-dark-${viewport.name}-${timestamp}.png`;
      const filepath = resolve(OUTPUT_DIR, filename);

      await page.screenshot({
        path: filepath,
        fullPage: true
      });

      console.log(`✅ Saved: ${filename}`);
    }

    console.log('\n🎉 Screenshot capture completed successfully!');
    console.log(`📁 Output directory: ${OUTPUT_DIR}`);

  } catch (error) {
    console.error('\n❌ Error during screenshot capture:');
    console.error(error);
    process.exit(1);
  } finally {
    if (page) await page.close();
    if (browser) await browser.close();
  }
}

// Execute
captureScreenshots();
