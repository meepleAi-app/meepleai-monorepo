/**
 * Re-audit script — desktop/tablet/mobile screenshots con SW reload e cache clear
 * Run from apps/web: node retake.mjs
 */
import { chromium } from '@playwright/test';
import { writeFileSync, mkdirSync } from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const BASE = 'http://localhost:3000';
const OUT = path.join(__dirname, '../../screenshots');

const VIEWPORTS = [
  { name: 'desktop', width: 1280, height: 800 },
  { name: 'tablet',  width: 768,  height: 1024 },
  { name: 'mobile',  width: 390,  height: 844 },
];

const PAGES = [
  { slug: 'dashboard',          url: '/dashboard' },
  { slug: 'agents',             url: '/agents' },
  { slug: 'play-records',       url: '/play-records' },
  { slug: 'play-records-stats', url: '/play-records?tab=stats' },
  { slug: 'game-nights',        url: '/game-nights' },
  { slug: 'discover',           url: '/discover' },
];

// Noise to exclude from "real" error count
const NOISE = ['[MSW]','webpack','HMR','Fast Refresh','_N_E','__webpack',
  'hot-update','Warning:','Download the React','[Fast','net::ERR_ABORTED'];

const report = [];

for (const vp of VIEWPORTS) {
  mkdirSync(path.join(OUT, vp.name), { recursive: true });
}

const browser = await chromium.launch({ headless: true });

for (const vp of VIEWPORTS) {
  // Fresh context for each viewport — no persistent storage
  const context = await browser.newContext({
    viewport: { width: vp.width, height: vp.height },
  });
  const page = await context.newPage();
  const allErrors = [];
  page.on('console', msg => { if (msg.type() === 'error') allErrors.push(msg.text()); });
  page.on('pageerror', err => allErrors.push(`PAGE ERROR: ${err.message}`));

  for (const pg of PAGES) {
    allErrors.length = 0;

    // First load — registers MSW service worker
    try {
      await page.goto(`${BASE}${pg.url}`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    } catch { /* ignore timeout on first load */ }
    await page.waitForTimeout(2000);

    // Clear ALL caches and unregister non-MSW service workers
    await page.evaluate(async () => {
      // Clear Cache API (HTTP cache entries)
      if ('caches' in window) {
        const keys = await caches.keys();
        await Promise.all(keys.map(key => caches.delete(key)));
      }
      // Unregister any stale service workers (non-MSW)
      if ('serviceWorker' in navigator) {
        const registrations = await navigator.serviceWorker.getRegistrations();
        for (const reg of registrations) {
          const url = reg.active?.scriptURL || reg.installing?.scriptURL || reg.waiting?.scriptURL || '';
          if (!url.includes('mockServiceWorker')) {
            await reg.unregister();
          }
        }
      }
    }).catch(() => {});

    await page.waitForTimeout(500);

    // Reload — SW is now active, requests get intercepted with fresh handlers
    try {
      await page.reload({ waitUntil: 'networkidle', timeout: 20000 });
    } catch {
      await page.reload({ waitUntil: 'domcontentloaded', timeout: 15000 });
    }
    await page.waitForTimeout(2500);

    const finalUrl = page.url();
    const filename = path.join(OUT, vp.name, `${pg.slug}.png`);
    await page.screenshot({ path: filename });

    const realErrors = allErrors.filter(e => !NOISE.some(n => e.includes(n)));

    report.push({
      viewport: vp.name,
      page: pg.slug,
      url: pg.url,
      finalUrl,
      redirected: !finalUrl.replace(BASE,'').startsWith(pg.url.split('?')[0]),
      errors: realErrors,
      screenshot: `${vp.name}/${pg.slug}.png`,
    });

    console.log(`[${vp.name}] ${pg.slug} → ${finalUrl.replace(BASE,'')} (${realErrors.length} errors)`);
    if (realErrors.length > 0) realErrors.slice(0,3).forEach(e => console.log(`    ⚠ ${e.slice(0,120)}`));
  }

  await context.close();
}

await browser.close();

writeFileSync(path.join(OUT, 'retake-report.json'), JSON.stringify(report, null, 2));

// Build markdown
const rows = (vp) => report
  .filter(r => r.viewport === vp)
  .map(r => `| ${r.page} | ${r.finalUrl.replace(BASE,'')} | ${r.redirected?'↩️':'✅'} | ${r.errors.length===0?'✅ 0':`❌ ${r.errors.length}`} | [png](${r.screenshot}) |`);

const errDetail = report.filter(r => r.errors.length > 0).map(r =>
  `### [${r.viewport}] ${r.page}\n${r.errors.slice(0,5).map(e=>`- \`${e.slice(0,180)}\``).join('\n')}\n`
).join('\n');

const md = `# Re-audit risultati — ${new Date().toISOString().slice(0,10)}

## Desktop (1280×800)

| Pagina | URL finale | Redirect | Errori | Screenshot |
|--------|-----------|----------|--------|-----------|
${rows('desktop').join('\n')}

## Tablet (768×1024)

| Pagina | URL finale | Redirect | Errori | Screenshot |
|--------|-----------|----------|--------|-----------|
${rows('tablet').join('\n')}

## Mobile (390×844)

| Pagina | URL finale | Redirect | Errori | Screenshot |
|--------|-----------|----------|--------|-----------|
${rows('mobile').join('\n')}

## Dettaglio errori

${errDetail || '_Nessun errore._'}
`;

writeFileSync(path.join(OUT, 'retake-results.md'), md);
console.log('\n✅ Done. Saved to screenshots/retake-results.md');
