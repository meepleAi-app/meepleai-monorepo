// MeepleCard Visual Regression Test
//
// Captures screenshots of the live app's MeepleCard dev showcase and the static
// admin-mockups files for side-by-side visual comparison.
//
// Usage:
//   1. Start dev server: cd apps/web && pnpm dev -p 3010
//   2. Run: cd apps/web && node scripts/meeplecard-visual-test.mjs
//   3. Check screenshots in D:/tmp/meeplecard-screenshots
//
// The script uses the playwright package via dynamic import so it works
// regardless of pnpm hoisting (playwright is a transitive dep via @playwright/test).

import { createRequire } from 'module';
import { fileURLToPath, pathToFileURL } from 'url';
import path from 'path';
import fs from 'fs';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const REPO_ROOT = path.resolve(__dirname, '../../..');
// Default: repo-relative tmp dir (works on Linux/macOS/Windows).
// Override with OUT_DIR env var if needed.
const OUT_DIR =
  process.env.OUT_DIR || path.join(REPO_ROOT, 'tmp/meeplecard-screenshots');
const APP_URL = process.env.APP_URL || 'http://localhost:3010';
const MOCKUPS_DIR = path.join(REPO_ROOT, 'admin-mockups');

if (!fs.existsSync(OUT_DIR)) {
  fs.mkdirSync(OUT_DIR, { recursive: true });
}

const targets = [
  // Live app — full dev showcase page
  {
    name: '01-app-dev-showcase-full',
    url: `${APP_URL}/dev/meeple-card`,
    waitFor: 'h2',
    fullPage: true,
  },
  // Live app — single cards from NavFooter section (use last match to skip Entity Types section)
  {
    name: '20-app-game-card-counts',
    url: `${APP_URL}/dev/meeple-card`,
    evalSelector: `
      const labels = Array.from(document.querySelectorAll('p'));
      const label = labels.find(p => p.textContent && p.textContent.includes('counts pieni'));
      return label ? label.parentElement : null;
    `,
  },
  {
    name: '21-app-game-card-empty-plus',
    url: `${APP_URL}/dev/meeple-card`,
    evalSelector: `
      const labels = Array.from(document.querySelectorAll('p'));
      const label = labels.find(p => p.textContent && p.textContent.includes('vuoto (plus)'));
      return label ? label.parentElement : null;
    `,
  },
  {
    name: '22-app-player-card',
    url: `${APP_URL}/dev/meeple-card`,
    evalSelector: `
      const labels = Array.from(document.querySelectorAll('p'));
      const matches = labels.filter(p => p.textContent && p.textContent.trim() === 'player');
      const label = matches[matches.length - 1];
      return label ? label.parentElement : null;
    `,
  },
  {
    name: '23-app-session-card',
    url: `${APP_URL}/dev/meeple-card`,
    evalSelector: `
      const labels = Array.from(document.querySelectorAll('p'));
      const matches = labels.filter(p => p.textContent && p.textContent.trim() === 'session');
      const label = matches[matches.length - 1];
      return label ? label.parentElement : null;
    `,
  },
  {
    name: '24-app-agent-card',
    url: `${APP_URL}/dev/meeple-card`,
    evalSelector: `
      const labels = Array.from(document.querySelectorAll('p'));
      const matches = labels.filter(p => p.textContent && p.textContent.trim() === 'agent');
      const label = matches[matches.length - 1];
      return label ? label.parentElement : null;
    `,
  },
  {
    name: '25-app-kb-card',
    url: `${APP_URL}/dev/meeple-card`,
    evalSelector: `
      const labels = Array.from(document.querySelectorAll('p'));
      const matches = labels.filter(p => p.textContent && p.textContent.trim() === 'kb');
      const label = matches[matches.length - 1];
      return label ? label.parentElement : null;
    `,
  },
  {
    name: '26-app-chat-card',
    url: `${APP_URL}/dev/meeple-card`,
    evalSelector: `
      const labels = Array.from(document.querySelectorAll('p'));
      const matches = labels.filter(p => p.textContent && p.textContent.trim() === 'chat');
      const label = matches[matches.length - 1];
      return label ? label.parentElement : null;
    `,
  },
  // Admin mockups — full pages
  {
    name: '50-mockup-real-app-render',
    url: pathToFileURL(path.join(MOCKUPS_DIR, 'meeple-card-real-app-render.html')).href,
    fullPage: true,
  },
  {
    name: '51-mockup-nav-buttons',
    url: pathToFileURL(path.join(MOCKUPS_DIR, 'meeple-card-nav-buttons-mockup.html')).href,
    fullPage: true,
  },
  {
    name: '52-mockup-summary-render',
    url: pathToFileURL(path.join(MOCKUPS_DIR, 'meeple-card-summary-render.html')).href,
    fullPage: true,
  },
];

async function run() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1600, height: 1200 },
    deviceScaleFactor: 2,
  });
  const page = await context.newPage();

  // Hide cookie banners that may overlap card screenshots
  await page.addInitScript(() => {
    const style = document.createElement('style');
    style.textContent = `
      [class*="cookie"], [data-testid*="cookie"], [class*="Cookie"], [id*="cookie"] {
        display: none !important;
      }
    `;
    document.documentElement.appendChild(style);
  });

  for (const t of targets) {
    console.log(`→ ${t.name}: ${t.url}`);
    try {
      await page.goto(t.url, { waitUntil: 'domcontentloaded', timeout: 60000 });
      await page.waitForTimeout(2000);

      if (t.waitFor) {
        try {
          await page.waitForSelector(t.waitFor, { timeout: 10000 });
        } catch {
          console.log(`  ⚠ waitFor not found: ${t.waitFor}`);
        }
      }

      const filepath = path.join(OUT_DIR, `${t.name}.png`);

      if (t.evalSelector) {
        const handle = await page.evaluateHandle(`(() => { ${t.evalSelector} })()`);
        const el = handle.asElement();
        if (el) {
          await el.scrollIntoViewIfNeeded();
          await page.waitForTimeout(500);
          await el.screenshot({ path: filepath });
        } else {
          console.log(`  ⚠ evalSelector returned null`);
          await page.screenshot({ path: filepath, fullPage: false });
        }
      } else {
        await page.screenshot({ path: filepath, fullPage: !!t.fullPage });
      }
      console.log(`  ✓ ${filepath}`);
    } catch (err) {
      console.error(`  ✗ ${t.name} failed: ${err.message}`);
    }
  }

  await browser.close();
  console.log(`\nDone. Screenshots in ${OUT_DIR}`);
}

run().catch(err => {
  console.error(err);
  process.exit(1);
});
