// MeepleCard Flip Backs Visual Verification
//
// Captures screenshots of the sections added in PRs #282/#285/#287:
// - Showcase Completo (badge contrast fix)
// - Flip Cards — Back per Entity Type (8 cards, front + 5 flipped backs)
// - Nav Click Behavior (4 behavior cards)
// - NavItems Disabled + Tooltip (3 examples)
// - Feature Matrix (13-row table)
// - Table View (EntityTable)
//
// Usage:
//   1. Dev server running on http://localhost:3000
//   2. Run: node apps/web/scripts/meeplecard-flip-backs-verify.mjs
//
// Env vars:
//   OUT_DIR  — output dir (default: apps/web/tmp/flip-backs-screenshots)
//   BASE_URL — base URL (default: http://localhost:3000)
//   FORMAT   — 'png' or 'jpeg' (default: png)
//   SCALE    — deviceScaleFactor, 1 for docs or 2 for hi-dpi (default: 2)
//   QUALITY  — JPEG quality 1-100 (default: 85)
//
// Docs mode (compact output for git commit):
//   FORMAT=jpeg SCALE=1 OUT_DIR=docs/frontend/screenshots/meeplecard-dev \
//     node apps/web/scripts/meeplecard-flip-backs-verify.mjs

import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const playwrightModule = await import(
  '../node_modules/.pnpm/playwright@1.58.2/node_modules/playwright/index.js'
);
const { chromium } = playwrightModule.default ?? playwrightModule;

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const OUT_DIR =
  process.env.OUT_DIR ?? path.resolve(__dirname, '../tmp/flip-backs-screenshots');
const BASE_URL = process.env.BASE_URL ?? 'http://localhost:3000';
const FORMAT = (process.env.FORMAT ?? 'png').toLowerCase();
const SCALE = Number(process.env.SCALE ?? '2');
const QUALITY = Number(process.env.QUALITY ?? '85');
const EXT = FORMAT === 'jpeg' ? 'jpg' : 'png';

fs.mkdirSync(OUT_DIR, { recursive: true });

function log(msg) {
  const stamp = new Date().toISOString().slice(11, 19);
  console.log(`[${stamp}] ${msg}`);
}

function shotOpts(filename) {
  const filePath = path.join(OUT_DIR, filename);
  const opts = { path: filePath, timeout: 10_000 };
  if (FORMAT === 'jpeg') {
    opts.type = 'jpeg';
    opts.quality = QUALITY;
  } else {
    opts.type = 'png';
  }
  return opts;
}

function fname(stem) {
  return `${stem}.${EXT}`;
}

async function scrollToSectionByHeading(page, headingText) {
  const locator = page.locator(`h2:has-text("${headingText}")`).first();
  await locator.waitFor({ state: 'visible', timeout: 10_000 });
  await locator.scrollIntoViewIfNeeded();
  await page.waitForTimeout(500);
  return locator;
}

async function captureSection(page, headingText, stem) {
  log(`Capturing section: ${headingText}`);
  const heading = await scrollToSectionByHeading(page, headingText);
  const section = heading.locator('xpath=ancestor::section[1]');
  await section.waitFor({ state: 'visible', timeout: 5_000 });
  const filename = fname(stem);
  await section.screenshot(shotOpts(filename));
  log(`  → saved ${filename}`);
  return section;
}

async function main() {
  log(`Output dir: ${OUT_DIR}`);
  log(`Format: ${FORMAT}${FORMAT === 'jpeg' ? ` (quality ${QUALITY})` : ''}`);
  log(`Scale: ${SCALE}x`);
  log(`Launching headless Chromium...`);
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    deviceScaleFactor: SCALE,
  });
  const page = await context.newPage();

  page.on('pageerror', err => log(`PAGE ERROR: ${err.message}`));
  page.on('console', msg => {
    if (msg.type() === 'error' && !msg.text().includes('Failed to load resource')) {
      // Suppress noisy API 502 errors (dev page is public, API not required)
    }
  });

  log(`Navigating to ${BASE_URL}/dev/meeple-card`);
  await page.goto(`${BASE_URL}/dev/meeple-card`, { waitUntil: 'networkidle', timeout: 60_000 });

  // Suppress window.alert to avoid modal blocking when we click buttons.
  await page.evaluate(() => {
    window.alert = msg => console.log('[alert suppressed]', msg);
  });

  log('Page loaded. Starting screenshots.');

  // 0. Top of page
  await page.screenshot(shotOpts(fname('00-full-top')));
  log(`  → ${fname('00-full-top')}`);

  // 1. Flip Cards section (fronts)
  const flipSection = await captureSection(
    page,
    'Flip Cards — Back per Entity Type',
    '01-flip-fronts'
  );

  // Click-to-flip helper
  async function flipAndCapture(nthIndex, stem) {
    const flip = flipSection.locator('div[class*="perspective"]').nth(nthIndex);
    await flip.scrollIntoViewIfNeeded();
    await page.waitForTimeout(300);
    await flip.click();
    await page.waitForTimeout(900); // 600ms flip transition + buffer
    await flipSection.screenshot(shotOpts(fname(stem)));
    log(`  → ${fname(stem)}`);
    // flip back for next iteration
    await flip.click();
    await page.waitForTimeout(900);
  }

  log('Flipping card 1 (game)...');
  await flipAndCapture(0, '02-flip-game-back');
  log('Flipping card 2 (toolkit)...');
  await flipAndCapture(1, '03-flip-toolkit-back');
  log('Flipping card 5 (agent)...');
  await flipAndCapture(4, '04-flip-agent-back');
  log('Flipping card 7 (player)...');
  await flipAndCapture(6, '05-flip-player-back');
  log('Flipping card 8 (tool)...');
  // Last one — no need to flip back
  const eighthFlip = flipSection.locator('div[class*="perspective"]').nth(7);
  await eighthFlip.scrollIntoViewIfNeeded();
  await page.waitForTimeout(300);
  await eighthFlip.click();
  await page.waitForTimeout(900);
  await flipSection.screenshot(shotOpts(fname('06-flip-tool-back')));
  log(`  → ${fname('06-flip-tool-back')}`);

  // Subsequent sections
  await captureSection(page, 'Nav Click Behavior', '07-nav-behavior');
  await captureSection(page, 'NavItems Disabled + Tooltip', '08-disabled-navitems');
  await captureSection(page, 'Feature Matrix', '09-feature-matrix');
  await captureSection(page, 'Showcase Completo — Tutte le Feature', '10-showcase-completo-badges');
  await captureSection(page, 'Table View — EntityTable', '11-entity-table');

  log('All screenshots captured. Closing browser.');
  await browser.close();

  const files = fs
    .readdirSync(OUT_DIR)
    .filter(f => f.endsWith(`.${EXT}`))
    .sort();
  const totalKB = files.reduce(
    (acc, f) => acc + fs.statSync(path.join(OUT_DIR, f)).size / 1024,
    0
  );
  log(`Captured ${files.length} files (total ${(totalKB / 1024).toFixed(1)} MB):`);
  files.forEach(f => {
    const size = fs.statSync(path.join(OUT_DIR, f)).size;
    log(`  ${f}  (${(size / 1024).toFixed(1)} KB)`);
  });
}

main().catch(err => {
  console.error('Script failed:', err);
  process.exit(1);
});
