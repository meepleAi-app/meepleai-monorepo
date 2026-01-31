#!/usr/bin/env tsx
/**
 * Screenshot Generator for Real Application
 *
 * Issue #2965: Screenshot per conferma applicazione nuovo stile
 *
 * Generates screenshots of the real Admin Dashboard at multiple viewports
 * for design validation and comparison with mockups.
 *
 * Prerequisites:
 *   - Dev server running: pnpm dev (http://localhost:3000)
 *   - Admin account available for login
 *
 * Usage:
 *   pnpm tsx scripts/screenshot-app.ts
 */

import { chromium, type Browser, type Page } from '@playwright/test';
import { resolve } from 'path';

const APP_URL = process.env.APP_URL || 'http://localhost:3000';
const ADMIN_DASHBOARD_PATH = '/admin';
const OUTPUT_DIR = resolve(__dirname, '../docs/design-proposals/meepleai-style/screenshots/app');

const VIEWPORTS = [
  { name: 'desktop', width: 1920, height: 1080 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'mobile', width: 375, height: 812 },
] as const;

async function captureScreenshots() {
  console.log('🚀 Starting app screenshot capture...\n');

  let browser: Browser | null = null;
  let page: Page | null = null;

  try {
    // Launch browser
    console.log('📦 Launching Chromium...');
    browser = await chromium.launch({ headless: false }); // Non-headless per debugging
    const context = await browser.newContext();
    page = await context.newPage();

    // Navigate to admin dashboard
    const dashboardUrl = `${APP_URL}${ADMIN_DASHBOARD_PATH}`;
    console.log(`🌐 Navigating to: ${dashboardUrl}`);

    try {
      await page.goto(dashboardUrl, {
        waitUntil: 'networkidle',
        timeout: 30000
      });
    } catch (error) {
      console.error(`\n❌ Failed to load ${dashboardUrl}`);
      console.error('Possible reasons:');
      console.error('  1. Dev server not running (run: pnpm dev in apps/web)');
      console.error('  2. Auth redirect (requires login)');
      console.error('  3. Port conflict (check PORT in .env.local)');
      throw error;
    }

    // Check if redirected to login (auth required)
    const currentUrl = page.url();
    if (currentUrl.includes('/login') || currentUrl.includes('/auth')) {
      console.log('\n⚠️  Authentication required!');
      console.log('Current URL:', currentUrl);
      console.log('\nOptions:');
      console.log('  1. Login manually in the browser window that just opened');
      console.log('  2. Use test credentials if available');
      console.log('  3. Disable auth for screenshot (not recommended)');
      console.log('\nWaiting 60 seconds for manual login...');

      // Wait for manual login
      await page.waitForTimeout(60000);

      // Navigate to dashboard again after login
      await page.goto(dashboardUrl, {
        waitUntil: 'networkidle',
        timeout: 30000
      });
    }

    // Wait for dashboard to load completely
    console.log('⏳ Waiting for dashboard to load...');
    await page.waitForSelector('[data-testid="admin-dashboard"], .admin-dashboard, main', {
      timeout: 10000
    }).catch(() => {
      console.log('Note: Dashboard selector not found, proceeding anyway...');
    });

    // Wait for dynamic content (metrics, charts)
    await page.waitForTimeout(3000);

    const timestamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, -5);

    // Capture at each viewport
    for (const viewport of VIEWPORTS) {
      console.log(`\n📸 Capturing ${viewport.name} (${viewport.width}x${viewport.height})...`);

      await page.setViewportSize({
        width: viewport.width,
        height: viewport.height
      });

      // Wait for responsive layout adjustments
      await page.waitForTimeout(1000);

      const filename = `admin-dashboard-app-${viewport.name}-${timestamp}.png`;
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
    console.error('\nTroubleshooting:');
    console.error('  - Ensure dev server is running: cd apps/web && pnpm dev');
    console.error('  - Check environment variables in apps/web/.env.local');
    console.error('  - Verify admin access is configured');
    process.exit(1);
  } finally {
    if (page) await page.close();
    if (browser) await browser.close();
  }
}

// Execute
captureScreenshots();
