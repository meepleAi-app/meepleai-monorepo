# Mockup↔Live Compare Tool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Un dev-tool locale (`pnpm compare:mockups`) che genera una gallery HTML statica affiancando, per ogni coppia mockup↔route, lo screenshot del mockup e quello della route live reale, con slider drag-to-reveal — per una review di fedeltà **manuale** (nessuna baseline, nessun pixel-diff).

**Architecture:** 4 unità isolate. Un **manifest** dichiara le coppie `{mockupHtml, route, auth, mock}`. Una **capture spec** Playwright (riusa `playwright.config.ts` con `PLAYWRIGHT_AUTH_BYPASS`) screenshotta il mockup (servito da `http-server`) e la route live (auth-bypass + `page.route` mock), emettendo `captures.json` + PNG in una dir gitignored. Un **report builder** (funzione pura Node) trasforma i capture in `gallery.html` self-contained (PNG come data-URI, slider inline). Output NON è un gate CI.

**Tech Stack:** Playwright (`@playwright/test`), `http-server` (già dep `^14.1.1`), Node ESM (`.mjs`), Vitest (unit test del report builder), TypeScript.

## Global Constraints

- Runtime: Node ESM per lo script builder (`.mjs`, `import`/`export`).
- Il tool è un **dev-tool locale**, MAI un gate CI (immune al problema baseline win32↔linux di #2063). Nessuna baseline PNG committata.
- Output dir `apps/web/mockup-compare-output/` è **gitignored** (mai committare screenshot).
- Confronto **manuale** — il tool mostra, non giudica: niente pixel-diff, niente soglie.
- Auth E2E (host-agnostic): `seedMockRoleCookies(page, 'Admin'|'User')` (gate SSR di `proxy.ts`) **+** `mockAuthEndpoints(page, { role: 'admin'|'user' })` (client `/auth/me` + `/auth/session/status`, regex host-agnostica, `onboardingCompleted:true`) — entrambi da `apps/web/e2e/_helpers/seedAuthSession`. Webserver con `PLAYWRIGHT_AUTH_BYPASS=true`.
- 🔴 **URL host-agnostici**: l'app usa URL RELATIVI nel browser (`getApiBase()`→`''`, `apps/web/src/lib/api/core/httpClient.ts:51-55`) → richieste a `localhost:3000/api/v1/...` via proxy Next. I mock `page.route` DEVONO usare glob `**/api/v1/...`, MAI URL assoluti `localhost:8080` (non intercettano). Verificato dalla review avversariale del piano (2026-07-17).
- Il **mockup capture NON è offline**: molti page-mock caricano React/ReactDOM/Babel da `unpkg.com` e transpilano JSX in-browser → serve rete a unpkg + attesa del mount reale (`waitForFunction`, non timeout fisso).
- Nessun cap silenzioso: coppie del manifest non catturabili → `console.log` esplicito + record con `liveError`.
- Comandi eseguiti da `apps/web/`. `pnpm test` = Vitest; test file glob `**/__tests__/**/*.{test,spec}.{ts,tsx}`.
- Mockup HTML sorgente: `admin-mockups/design_files/` (repo root, cioè `../../admin-mockups/design_files/` rispetto a `apps/web/`).

---

### Task 1: Manifest + gitignore

**Files:**
- Modify: `.gitignore` (repo root) — aggiungi la output dir
- Create: `apps/web/e2e/mockup-compare/manifest.ts`
- Test: `apps/web/e2e/mockup-compare/__tests__/manifest.test.ts`

**Interfaces:**
- Produces: `MockupComparePair` interface + `PAIRS: readonly MockupComparePair[]` + `DESIGN_FILES_DIR` (path assoluto della dir mockup) + `OUTPUT_DIR` (path assoluto output).

- [ ] **Step 1: Aggiungi la output dir a `.gitignore`**

Aggiungi in fondo a `.gitignore` (repo root):
```
# Mockup↔live compare tool output (#2999) — screenshots, mai committare
apps/web/mockup-compare-output/
```

- [ ] **Step 2: Scrivi il test del manifest (RED)**

Create `apps/web/e2e/mockup-compare/__tests__/manifest.test.ts`:
```ts
import { existsSync } from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

import { PAIRS, DESIGN_FILES_DIR } from '../manifest';

describe('mockup-compare manifest', () => {
  it('has at least one pair', () => {
    expect(PAIRS.length).toBeGreaterThan(0);
  });

  it('has unique ids', () => {
    const ids = PAIRS.map(p => p.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('references mockup HTML files that exist on disk', () => {
    for (const pair of PAIRS) {
      const abs = path.join(DESIGN_FILES_DIR, pair.mockupHtml);
      expect(existsSync(abs), `${pair.id}: missing ${abs}`).toBe(true);
    }
  });
});
```

- [ ] **Step 3: Esegui il test — deve fallire (modulo mancante)**

Run: `pnpm vitest run e2e/mockup-compare/__tests__/manifest.test.ts`
Expected: FAIL con `Failed to resolve import "../manifest"`.

- [ ] **Step 4: Crea il manifest**

Create `apps/web/e2e/mockup-compare/manifest.ts`:
```ts
/**
 * Mockup↔live compare — pairing manifest (#2999).
 *
 * Ogni entry accoppia un page-mock HTML statico (admin-mockups/design_files)
 * con la route live reale. La capture spec screenshotta entrambi.
 * Estensione: aggiungi righe qui (+ un `mock` page.route se la route non
 * supporta il seam `?fixture=`).
 */
import path from 'node:path';

import type { Page } from '@playwright/test';

export interface MockupComparePair {
  /** Slug stabile (kebab-case) — usato nei nomi file e nella gallery. */
  readonly id: string;
  /** Titolo umano mostrato nella gallery. */
  readonly label: string;
  /** Nome file HTML dentro admin-mockups/design_files/. */
  readonly mockupHtml: string;
  /** Route live (path relativo, es. "/library/wishlist"). */
  readonly route: string;
  /** Ruolo auth per il bypass E2E. Default 'user'. */
  readonly auth?: 'user' | 'admin';
  /** Setup page.route opzionale per mockare le API della route. */
  readonly mock?: (page: Page) => Promise<void>;
  /** Viewport override. Default 1920x1080. */
  readonly viewport?: { readonly width: number; readonly height: number };
}

/** apps/web/e2e/mockup-compare → repo root → admin-mockups/design_files. */
export const DESIGN_FILES_DIR = path.resolve(
  process.cwd(),
  '..',
  '..',
  'admin-mockups',
  'design_files'
);

/** apps/web/mockup-compare-output (gitignored). */
export const OUTPUT_DIR = path.resolve(process.cwd(), 'mockup-compare-output');

/** Wishlist fixture — WishlistItemDto[] con gameName inline (no library map). */
const WISHLIST_FIXTURE = [
  {
    id: '11111111-1111-4111-8111-111111111111',
    userId: '99999999-9999-4999-8999-999999999999',
    gameId: '22222222-2222-4222-8222-222222222222',
    gameName: 'Terraforming Mars',
    priority: 'high',
    targetPrice: 45.0,
    notes: 'Aspetto un saldo sotto i 50€',
    addedAt: '2026-07-01T10:00:00.000Z',
    updatedAt: null,
    visibility: 'private',
  },
  {
    id: '33333333-3333-4333-8333-333333333333',
    userId: '99999999-9999-4999-8999-999999999999',
    gameId: '44444444-4444-4444-8444-444444444444',
    gameName: 'Wingspan',
    priority: 'medium',
    targetPrice: null,
    notes: null,
    addedAt: '2026-06-15T09:30:00.000Z',
    updatedAt: '2026-06-20T12:00:00.000Z',
    visibility: 'private',
  },
];

export const PAIRS: readonly MockupComparePair[] = [
  {
    id: 'library-wishlist',
    label: 'Library · Wishlist',
    mockupHtml: 'sp4-library-wishlist.html',
    route: '/library/wishlist',
    auth: 'user',
    // 🔴 Glob HOST-AGNOSTICI: l'app usa URL relativi nel browser
    // (getApiBase()→'' , httpClient.ts:51-55) → richieste a localhost:3000/api/...
    // via proxy Next. URL assoluti localhost:8080 NON intercetterebbero.
    mock: async (page) => {
      await page.route('**/api/v1/wishlist', (route) =>
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(WISHLIST_FIXTURE),
        })
      );
      await page.route('**/api/v1/wishlist/highlights', (route) =>
        route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
      );
      // La wishlist page usa useLibrary per la mappa gameId→title; i fixture
      // portano già gameName inline, quindi la library può essere vuota.
      await page.route('**/api/v1/library**', (route) =>
        route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
      );
    },
  },
];
```

- [ ] **Step 5: Esegui il test — deve passare**

Run: `pnpm vitest run e2e/mockup-compare/__tests__/manifest.test.ts`
Expected: PASS (3 test). Se "missing …sp4-library-wishlist.html", verifica `ls ../../admin-mockups/design_files/sp4-library-wishlist.html`.

- [ ] **Step 6: Commit**

```bash
git add .gitignore apps/web/e2e/mockup-compare/manifest.ts apps/web/e2e/mockup-compare/__tests__/manifest.test.ts
git commit -m "feat(compare): #2999 mockup↔live pairing manifest + wishlist pair"
```

---

### Task 2: Report builder — funzione pura `buildReportHtml`

**Files:**
- Create: `apps/web/scripts/mockup-compare/build-report.mjs`
- Test: `apps/web/scripts/mockup-compare/__tests__/build-report.test.ts`

**Interfaces:**
- Produces: `buildReportHtml(entries)` → `string` (HTML completo self-contained).
  - `entries`: `Array<{ id, label, route, viewport: {width,height}, mockupDataUri: string|null, liveDataUri: string|null, liveError?: string, designIntent?: string }>`.

- [ ] **Step 1: Scrivi il test (RED)**

Create `apps/web/scripts/mockup-compare/__tests__/build-report.test.ts`:
```ts
import { describe, expect, it } from 'vitest';

// @ts-expect-error — .mjs senza tipi, import runtime
import { buildReportHtml } from '../build-report.mjs';

const baseEntry = {
  id: 'library-wishlist',
  label: 'Library · Wishlist',
  route: '/library/wishlist',
  viewport: { width: 1920, height: 1080 },
  mockupDataUri: 'data:image/png;base64,MOCKUP',
  liveDataUri: 'data:image/png;base64,LIVE',
  designIntent: 'current',
};

describe('buildReportHtml', () => {
  it('produces a full self-contained HTML document', () => {
    const html = buildReportHtml([baseEntry]);
    expect(html).toContain('<!doctype html>');
    expect(html).toContain('Library · Wishlist');
    expect(html).toContain('/library/wishlist');
  });

  it('embeds both mockup and live images as data URIs', () => {
    const html = buildReportHtml([baseEntry]);
    expect(html).toContain('data:image/png;base64,MOCKUP');
    expect(html).toContain('data:image/png;base64,LIVE');
  });

  it('renders a slider control per pair', () => {
    const html = buildReportHtml([baseEntry]);
    expect(html).toContain('type="range"');
    expect(html).toContain('data-pair-id="library-wishlist"');
  });

  it('shows an error placeholder when live capture failed', () => {
    const html = buildReportHtml([
      { ...baseEntry, liveDataUri: null, liveError: 'Timeout 30000ms' },
    ]);
    expect(html).toContain('live capture failed');
    expect(html).toContain('Timeout 30000ms');
    expect(html).not.toContain('data:image/png;base64,LIVE');
  });

  it('shows an error placeholder when mockup capture failed', () => {
    const html = buildReportHtml([
      { ...baseEntry, mockupDataUri: null, mockupError: 'unpkg unreachable' },
    ]);
    expect(html).toContain('mockup capture failed');
    expect(html).toContain('unpkg unreachable');
    expect(html).not.toContain('data:image/png;base64,MOCKUP');
  });

  it('escapes HTML in labels and errors', () => {
    const html = buildReportHtml([
      { ...baseEntry, label: '<script>x</script>', liveError: '<b>bad</b>', liveDataUri: null },
    ]);
    expect(html).not.toContain('<script>x</script>');
    expect(html).toContain('&lt;script&gt;');
  });
});
```

- [ ] **Step 2: Esegui il test — deve fallire**

Run: `pnpm vitest run scripts/mockup-compare/__tests__/build-report.test.ts`
Expected: FAIL con `Failed to resolve import "../build-report.mjs"`.

- [ ] **Step 3: Implementa `build-report.mjs` (solo la funzione pura, per ora)**

Create `apps/web/scripts/mockup-compare/build-report.mjs`:
```js
/**
 * Report builder per il tool mockup↔live compare (#2999).
 * `buildReportHtml` è puro (nessun I/O) → unit-testabile.
 */

/** Escape minimale per testo iniettato in HTML. */
export function escapeHtml(s) {
  return String(s)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

function renderPair(e) {
  const vp = `${e.viewport.width}×${e.viewport.height}`;
  const intent = e.designIntent ? `<span class="intent">${escapeHtml(e.designIntent)}</span>` : '';
  const mockup = e.mockupDataUri
    ? `<img class="layer mockup" src="${e.mockupDataUri}" alt="mockup ${escapeHtml(e.id)}" />`
    : `<div class="live-error">⚠ mockup capture failed<br /><code>${escapeHtml(e.mockupError ?? 'unknown')}</code></div>`;
  const live = e.liveDataUri
    ? `<img class="layer live" src="${e.liveDataUri}" alt="live ${escapeHtml(e.id)}" />`
    : `<div class="live-error">⚠ live capture failed<br /><code>${escapeHtml(e.liveError ?? 'unknown')}</code></div>`;
  // Slider ha senso solo se ENTRAMBE le catture esistono.
  const slider = e.mockupDataUri && e.liveDataUri
    ? `<input type="range" min="0" max="100" value="50" class="slider"
         data-pair-id="${escapeHtml(e.id)}" aria-label="reveal live over mockup" />`
    : '';
  return `
  <section class="pair" data-pair="${escapeHtml(e.id)}">
    <header>
      <h2>${escapeHtml(e.label)} ${intent}</h2>
      <p class="meta"><code>${escapeHtml(e.route)}</code> · ${vp}</p>
    </header>
    <div class="compare" data-mode="overlay">
      <div class="stage">
        ${mockup}
        <div class="live-wrap" data-pair-id="${escapeHtml(e.id)}">${live}</div>
      </div>
      ${slider}
      <button class="toggle" data-pair-id="${escapeHtml(e.id)}">side-by-side ⇄ overlay</button>
    </div>
  </section>`;
}

export function buildReportHtml(entries) {
  const body = entries.map(renderPair).join('\n');
  const css = `
    :root { color-scheme: light dark; }
    body { margin: 0; font: 14px/1.5 system-ui, sans-serif; background: #f7f3ee; color: #1a1a1a; }
    header.top { padding: 16px 24px; border-bottom: 1px solid #ccc; }
    .pair { padding: 24px; border-bottom: 1px solid #ddd; }
    .pair h2 { margin: 0 0 4px; font-size: 16px; }
    .intent { font-size: 11px; padding: 2px 6px; border-radius: 6px; background: #e5dccb; margin-left: 8px; }
    .meta { margin: 0 0 12px; color: #666; }
    .stage { position: relative; max-width: 1200px; border: 1px solid #bbb; overflow: hidden; }
    .layer { display: block; width: 100%; height: auto; }
    .live-wrap { position: absolute; inset: 0; overflow: hidden; }
    .compare[data-mode="overlay"] .live-wrap { width: 50%; }
    .compare[data-mode="sbs"] .stage { display: none; }
    .compare[data-mode="sbs"] .sbs { display: flex; gap: 12px; }
    .sbs { display: none; }
    .sbs figure { flex: 1; margin: 0; }
    .sbs img { width: 100%; height: auto; border: 1px solid #bbb; }
    .slider { width: 100%; max-width: 1200px; margin: 8px 0; }
    .toggle { font: inherit; padding: 4px 10px; cursor: pointer; }
    .live-error { padding: 40px; text-align: center; color: #a00; background: #fdd; }
    @media (prefers-color-scheme: dark) { body { background: #1a1a1a; color: #eee; } }
  `;
  const js = `
    document.querySelectorAll('.slider').forEach(function (s) {
      s.addEventListener('input', function () {
        var wrap = document.querySelector('.live-wrap[data-pair-id="' + s.dataset.pairId + '"]');
        if (wrap) wrap.style.width = s.value + '%';
      });
    });
    document.querySelectorAll('.toggle').forEach(function (b) {
      b.addEventListener('click', function () {
        var c = b.closest('.compare');
        c.dataset.mode = c.dataset.mode === 'overlay' ? 'sbs' : 'overlay';
      });
    });
  `;
  return `<!doctype html>
<html lang="it"><head><meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Mockup↔Live Compare (#2999)</title>
<style>${css}</style></head>
<body>
<header class="top"><strong>Mockup↔Live Compare</strong> — ${entries.length} coppie · confronto manuale (#2999)</header>
${body}
<script>${js}</script>
</body></html>`;
}
```

> Nota: il template include l'hook `.sbs` per la vista side-by-side; la generazione del blocco `.sbs` è omessa qui per minimalità (overlay+slider è il default). Se vuoi anche il side-by-side reale, aggiungi in `renderPair` un `<div class="sbs">` con due `<figure>` (mockup/live) — coperto da un test aggiuntivo. YAGNI: non richiesto dal MVP.

- [ ] **Step 4: Esegui il test — deve passare**

Run: `pnpm vitest run scripts/mockup-compare/__tests__/build-report.test.ts`
Expected: PASS (6 test).

- [ ] **Step 5: Commit**

```bash
git add apps/web/scripts/mockup-compare/build-report.mjs apps/web/scripts/mockup-compare/__tests__/build-report.test.ts
git commit -m "feat(compare): #2999 pure report builder (buildReportHtml) + tests"
```

---

### Task 3: Report builder CLI (fs glue)

**Files:**
- Create: `apps/web/scripts/mockup-compare/generate.mjs`

**Interfaces:**
- Consumes: `buildReportHtml`, `escapeHtml` da `./build-report.mjs`; `OUTPUT_DIR` da `../../e2e/mockup-compare/manifest.ts` — **NO**: il manifest è TS; lo script `.mjs` non lo importa. Ridefinisci `OUTPUT_DIR` localmente (stesso valore: `apps/web/mockup-compare-output`).
- Legge `captures.json` scritto dalla capture spec (Task 5): `Array<{ id, label, route, viewport, mockupPng, livePng, liveError? }>` (path relativi a OUTPUT_DIR).
- Produces: `mockup-compare-output/gallery.html`; stampa il path assoluto; con `--open` tenta l'apertura best-effort.

- [ ] **Step 1: Implementa la CLI**

Create `apps/web/scripts/mockup-compare/generate.mjs`:
```js
#!/usr/bin/env node
/**
 * CLI glue del report builder (#2999): legge captures.json + i PNG,
 * li converte in data-URI, chiama buildReportHtml, scrive gallery.html.
 */
import { spawn } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { buildReportHtml } from './build-report.mjs';

const OUTPUT_DIR = path.resolve(process.cwd(), 'mockup-compare-output');
const CAPTURES = path.join(OUTPUT_DIR, 'captures.json');

function pngToDataUri(relPath) {
  if (!relPath) return null;
  const abs = path.join(OUTPUT_DIR, relPath);
  if (!existsSync(abs)) return null;
  return `data:image/png;base64,${readFileSync(abs).toString('base64')}`;
}

function readDesignIntent(id) {
  // fidelity companion opzionale: admin-mockups/design_files/<mockupBase>.fidelity.json
  return undefined; // MVP: intent non risolto; estensione futura.
}

function main() {
  if (!existsSync(CAPTURES)) {
    console.error(`[compare] captures.json non trovato in ${OUTPUT_DIR}. Esegui prima la capture spec.`);
    process.exit(1);
  }
  const captures = JSON.parse(readFileSync(CAPTURES, 'utf8'));
  const entries = captures.map((c) => ({
    id: c.id,
    label: c.label,
    route: c.route,
    viewport: c.viewport,
    mockupDataUri: pngToDataUri(c.mockupPng),
    mockupError: c.mockupError,
    liveDataUri: pngToDataUri(c.livePng),
    liveError: c.liveError,
    designIntent: readDesignIntent(c.id),
  }));
  const html = buildReportHtml(entries);
  const out = path.join(OUTPUT_DIR, 'gallery.html');
  writeFileSync(out, html, 'utf8');
  console.log(`[compare] gallery generata: ${out}`);
  console.log(`[compare] ${entries.length} coppie (${entries.filter((e) => e.liveError).length} live falliti)`);

  if (process.argv.includes('--open')) {
    const opener = process.platform === 'win32' ? 'start' : process.platform === 'darwin' ? 'open' : 'xdg-open';
    spawn(opener, [out], { shell: true, stdio: 'ignore', detached: true }).unref();
  }
}

main();
```

- [ ] **Step 2: Smoke test manuale della CLI su un captures fittizio**

```bash
cd apps/web
mkdir -p mockup-compare-output
printf '[{"id":"x","label":"X","route":"/x","viewport":{"width":100,"height":100},"mockupPng":null,"livePng":null,"liveError":"smoke"}]' > mockup-compare-output/captures.json
node scripts/mockup-compare/generate.mjs
```
Expected: stampa `gallery generata: …/gallery.html` e `1 coppie (1 live falliti)`; `mockup-compare-output/gallery.html` esiste e contiene `live capture failed`.

- [ ] **Step 3: Pulisci l'artefatto di smoke**

```bash
rm -rf apps/web/mockup-compare-output
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/scripts/mockup-compare/generate.mjs
git commit -m "feat(compare): #2999 report CLI (captures.json → gallery.html)"
```

---

### Task 4: Config Playwright dedicata

**Files:**
- Create: `apps/web/playwright.mockup-compare.config.ts`

**Interfaces:**
- Produces: config Playwright che avvia (a) il Next webServer con `PLAYWRIGHT_AUTH_BYPASS=true`, (b) `http-server` su `admin-mockups/design_files` porta 5175; `testDir` = `e2e/mockup-compare`, `testMatch` = `capture.spec.ts`. Espone la porta mockup via `process.env.MOCKUP_HTTP_PORT`.

- [ ] **Step 1: Crea la config**

Create `apps/web/playwright.mockup-compare.config.ts`:
```ts
/**
 * Config Playwright del tool mockup↔live compare (#2999).
 * NON è un gate CI — dev-tool locale. Avvia il Next app (auth-bypass) +
 * http-server per i mockup statici. La capture spec produce screenshot, non
 * assert.
 */
import { defineConfig, devices } from '@playwright/test';

const MOCKUP_PORT = 5175;

export default defineConfig({
  testDir: './e2e/mockup-compare',
  testMatch: 'capture.spec.ts',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: 'list',
  timeout: 60_000,
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'retain-on-failure',
    locale: 'it-IT',
    timezoneId: 'UTC',
    colorScheme: 'light',
  },
  projects: [
    {
      name: 'compare-desktop',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1920, height: 1080 } },
    },
  ],
  webServer: [
    {
      command:
        'node --max-old-space-size=8192 ./node_modules/next/dist/bin/next dev -p 3000',
      url: 'http://localhost:3000',
      reuseExistingServer: true,
      timeout: 180_000,
      env: { PLAYWRIGHT_AUTH_BYPASS: 'true' },
    },
    {
      command: `pnpm exec http-server ../../admin-mockups/design_files -p ${MOCKUP_PORT} -s --cors`,
      url: `http://127.0.0.1:${MOCKUP_PORT}`,
      reuseExistingServer: true,
      timeout: 30_000,
    },
  ],
});
```

- [ ] **Step 2: Verifica che la config parsi (typecheck)**

Run: `pnpm typecheck`
Expected: nessun errore TS.

- [ ] **Step 3: Commit**

```bash
git add apps/web/playwright.mockup-compare.config.ts
git commit -m "chore(compare): #2999 dedicated Playwright config (next auth-bypass + mockup http-server)"
```

---

### Task 5: Capture spec

**Files:**
- Create: `apps/web/e2e/mockup-compare/capture.spec.ts`

**Interfaces:**
- Consumes: `PAIRS`, `OUTPUT_DIR` da `./manifest`; `seedMockRoleCookies` da `../_helpers/seedAuthSession`.
- Produces: per ogni pair, `mockup-compare-output/<id>__mockup.png` + `<id>__live.png` (o `liveError`); e `mockup-compare-output/captures.json`.

- [ ] **Step 1: Implementa la capture spec**

Create `apps/web/e2e/mockup-compare/capture.spec.ts`:
```ts
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { test } from '@playwright/test';

import { mockAuthEndpoints, seedMockRoleCookies } from '../_helpers/seedAuthSession';

import { PAIRS, OUTPUT_DIR } from './manifest';

const MOCKUP_PORT = 5175;

interface CaptureRecord {
  id: string;
  label: string;
  route: string;
  viewport: { width: number; height: number };
  mockupPng: string | null;
  mockupError?: string;
  livePng: string | null;
  liveError?: string;
}

const records: CaptureRecord[] = [];

test.beforeAll(() => {
  mkdirSync(OUTPUT_DIR, { recursive: true });
});

test.afterAll(() => {
  writeFileSync(path.join(OUTPUT_DIR, 'captures.json'), JSON.stringify(records, null, 2), 'utf8');
  console.log(`[compare] captures.json scritto (${records.length} coppie)`);
});

for (const pair of PAIRS) {
  test(`capture ${pair.id}`, async ({ page }) => {
    const viewport = pair.viewport ?? { width: 1920, height: 1080 };
    await page.setViewportSize(viewport);
    const rec: CaptureRecord = {
      id: pair.id,
      label: pair.label,
      route: pair.route,
      viewport,
      mockupPng: null,
      livePng: null,
    };

    // 1) MOCKUP statico via http-server. NB: molti page-mock caricano
    // React/ReactDOM/Babel da unpkg.com e transpilano JSX in-browser → serve
    // RETE (unpkg) + attesa del MOUNT reale, non un timeout fisso.
    try {
      await page.goto(`http://127.0.0.1:${MOCKUP_PORT}/${pair.mockupHtml}`, {
        waitUntil: 'networkidle',
      });
      await page.waitForFunction(
        () => {
          const root = document.querySelector('#root');
          if (root) return root.childElementCount > 0;
          return document.body.childElementCount > 0;
        },
        { timeout: 15_000 }
      );
      const mockupFile = `${pair.id}__mockup.png`;
      await page.screenshot({ path: path.join(OUTPUT_DIR, mockupFile), fullPage: true });
      rec.mockupPng = mockupFile;
    } catch (err) {
      rec.mockupError = (err as Error).message;
      console.log(`[compare] ${pair.id}: mockup capture failed — ${rec.mockupError}`);
    }

    // 2) LIVE route reale. Auth = seed cookie (proxy SSR gate) + mockAuthEndpoints
    // (client /auth/me + /auth/session/status, regex host-agnostico). Dati via
    // pair.mock (glob host-agnostici). NESSUN mock auth hand-rolled.
    try {
      await seedMockRoleCookies(page, pair.auth === 'admin' ? 'Admin' : 'User');
      await mockAuthEndpoints(page, { role: pair.auth === 'admin' ? 'admin' : 'user' });
      if (pair.mock) await pair.mock(page);
      await page.goto(pair.route, { waitUntil: 'networkidle' });
      await page.waitForTimeout(1000);
      const liveFile = `${pair.id}__live.png`;
      await page.screenshot({ path: path.join(OUTPUT_DIR, liveFile), fullPage: true });
      rec.livePng = liveFile;
    } catch (err) {
      rec.liveError = (err as Error).message;
      console.log(`[compare] ${pair.id}: live capture failed — ${rec.liveError}`);
    }

    records.push(rec);
  });
}
```

- [ ] **Step 2: Esegui la capture spec sullo slice MVP**

Run: `cd apps/web && dotenv -e .env.test -- playwright test --config playwright.mockup-compare.config.ts`
Expected: 1 test `capture library-wishlist` PASS; log `captures.json scritto (1 coppie)`; esistono `mockup-compare-output/library-wishlist__mockup.png` e `library-wishlist__live.png`.

> Se il live fallisce (auth/route), il test resta verde (screenshot mockup ok, `liveError` valorizzato). Investiga il log: la route `/library/wishlist` richiede il seed cookie User + `/auth/me` mock; se mostra empty-state, verifica che il mock `**/api/v1/wishlist` intercetti (l'`apiBase` deve combaciare con quello che l'app chiama).

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/mockup-compare/capture.spec.ts
git commit -m "feat(compare): #2999 Playwright capture spec (mockup + live route screenshots)"
```

---

### Task 6: Script `compare:mockups` + smoke end-to-end + README

**Files:**
- Modify: `apps/web/package.json` (scripts)
- Create: `apps/web/e2e/mockup-compare/README.md`

**Interfaces:**
- Produces: `pnpm compare:mockups` (capture → generate) e `pnpm compare:mockups:open` (con `--open`).

- [ ] **Step 1: Aggiungi gli script a `apps/web/package.json`**

Nella sezione `"scripts"`, accanto a `test:storybook:snapshots`:
```json
"compare:mockups": "dotenv -e .env.test -- playwright test --config playwright.mockup-compare.config.ts && node scripts/mockup-compare/generate.mjs",
"compare:mockups:open": "dotenv -e .env.test -- playwright test --config playwright.mockup-compare.config.ts && node scripts/mockup-compare/generate.mjs --open"
```

- [ ] **Step 2: Smoke end-to-end**

Run: `cd apps/web && pnpm compare:mockups`
Expected: capture PASS → `gallery generata: …/mockup-compare-output/gallery.html` → apri il file nel browser: mostra la riga "Library · Wishlist" con mockup a sinistra, route live sotto lo slider; trascinando lo slider si rivela il live sul mockup.

- [ ] **Step 3: Scrivi il README del tool**

Create `apps/web/e2e/mockup-compare/README.md`:
```markdown
# Mockup↔Live Compare Tool (#2999)

Dev-tool locale per la review **manuale** di fedeltà mockup↔implementazione live.
Genera una gallery HTML statica che affianca lo screenshot del page-mock e quello
della route live reale, con slider drag-to-reveal.

**NON è un gate CI** (nessuna baseline, nessun pixel-diff → immune al problema
baseline win32↔linux che ha descopato la suite pixel Storybook, #2063).

## Uso

```bash
cd apps/web
pnpm compare:mockups        # capture + genera gallery.html (stampa il path)
pnpm compare:mockups:open   # idem + apre nel browser
```

Output (gitignored): `apps/web/mockup-compare-output/gallery.html` + PNG.

## Aggiungere una coppia

Aggiungi una riga a `e2e/mockup-compare/manifest.ts`:
- `mockupHtml`: nome file in `admin-mockups/design_files/`.
- `route`: path live.
- `auth`: `'user'` | `'admin'`.
- `mock` (opzionale): `page.route` per popolare i dati, **oppure** usa il seam
  built-in `?fixture=default` dove supportato (es. `useLibrary`).

## Come funziona

`manifest.ts` → `capture.spec.ts` (screenshot mockup via http-server :5175 +
route live via app :3000 auth-bypass + mock) → `captures.json` →
`scripts/mockup-compare/generate.mjs` → `gallery.html`.

## Note / limiti

- **Il mockup capture richiede rete** a `unpkg.com`: molti page-mock caricano
  React/ReactDOM/Babel da CDN e transpilano JSX in-browser. La capture attende
  il mount reale (`waitForFunction`); se unpkg è irraggiungibile la gallery
  mostra un placeholder "mockup capture failed" invece di uno screenshot vuoto.
- Alcuni page-mock `.html` sono standalone e possono divergere dalla sorgente
  che l'implementazione ha effettivamente tracciato (es. `sp4-library-wishlist`
  cita un `-ui.jsx`); un po' di drift è atteso e va giudicato manualmente.
```

- [ ] **Step 4: Verifica finale (typecheck + lint + report builder test)**

Run: `cd apps/web && pnpm typecheck && pnpm vitest run scripts/mockup-compare e2e/mockup-compare`
Expected: typecheck pulito; test manifest + build-report verdi.

- [ ] **Step 5: Commit**

```bash
git add apps/web/package.json apps/web/e2e/mockup-compare/README.md
git commit -m "feat(compare): #2999 compare:mockups script + tool README"
```

---

## Self-Review (compilato in fase di planning)

- **Spec coverage**: §4.1 manifest → Task 1 · §4.3 report builder → Task 2+3 · §4.4 template gallery → Task 2 · §4.2 capture spec → Task 5 · §5 config+script → Task 4+6 · §6 slice `/library/wishlist` → Task 1+5 · §7 error handling (liveError placeholder) → Task 2 (test) + Task 5 · §8 testing (report builder pure) → Task 2 · §9 gitignore → Task 1.
- **Placeholder scan**: nessun TBD; `readDesignIntent` è dichiaratamente MVP-stub che ritorna `undefined` (comportamento definito, non placeholder) — l'intent è un nice-to-have §9, non un requisito MVP.
- **Type consistency**: `MockupComparePair` (Task 1) usato in Task 5; `CaptureRecord` (Task 5) ↔ shape letto da `generate.mjs` (Task 3) ↔ `entries` di `buildReportHtml` (Task 2) — campi allineati (`id/label/route/viewport/mockupPng→mockupDataUri/livePng→liveDataUri/liveError`). `OUTPUT_DIR` definito in manifest (Task 1) e ridefinito localmente in `generate.mjs` (Task 3, nota esplicita: lo `.mjs` non importa il TS) — stesso valore `apps/web/mockup-compare-output`.

## Note di rischio per l'esecutore
- Il **live capture di `/library/wishlist`** è il punto più incerto: dipende dal fatto che `apiBase` nel mock combaci con l'endpoint reale chiamato dall'app e che il seed-cookie User sblocchi la route sotto `PLAYWRIGHT_AUTH_BYPASS`. Se il live mostra empty/redirect, NON è un blocco del tool: il record porta `liveError`/empty e la gallery lo evidenzia. Investiga con `--headed` o guardando `mockup-compare-output/library-wishlist__live.png`.
- Se `sp4-library-wishlist.html` fosse un mockup "shell" senza dati inline, lo screenshot mockup sarà comunque valido (è ciò che il designer confronta).
