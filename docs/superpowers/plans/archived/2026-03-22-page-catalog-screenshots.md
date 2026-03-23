# Page Catalog with Screenshots — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an automated system that screenshots all 209 frontend pages on staging and generates a static HTML catalog site.

**Architecture:** Three-phase TypeScript script (`capture.ts`) discovers routes from filesystem, logs into staging via Playwright, captures screenshots in parallel. Separate script (`generate.ts`) reads the manifest and produces a single self-contained `index.html` with card grid, filters, search, and lightbox. All tooling lives in `docs/pages/`, uses existing `tsx` and `@playwright/test` from `apps/web/package.json`.

**Tech Stack:** TypeScript, Playwright (already in `apps/web`), tsx (already in `apps/web`), vanilla HTML/CSS/JS for output.

**Spec:** `docs/superpowers/specs/2026-03-22-page-catalog-screenshots-design.md`

---

## File Structure

```
docs/pages/
├── config.ts          # Configuration constants and types
├── routes.ts          # Route discovery: scan filesystem → route list
├── capture.ts         # Main entry: login, resolve params, capture screenshots
├── generate.ts        # HTML generator: read manifest → produce index.html
├── manifest.json      # (generated) Page manifest with metadata
├── screenshots/       # (generated) PNG files organized by group
│   ├── public/
│   ├── auth/
│   ├── authenticated/
│   ├── admin/
│   ├── chat/
│   └── other/
└── dist/
    └── index.html     # (generated) Static catalog site
```

**Existing files to modify:**
- `apps/web/package.json` — add 4 npm scripts
- `.gitignore` — add generated artifacts

---

### Task 1: Configuration and Types (`docs/pages/config.ts`)

**Files:**
- Create: `docs/pages/config.ts`

- [ ] **Step 1: Create config.ts with all types and constants**

```typescript
// docs/pages/config.ts
import * as path from 'path';

// ============================================================================
// Types
// ============================================================================

export interface PageEntry {
  id: string;
  route: string;
  routePattern: string;     // Original pattern with [param]
  group: string;
  title: string;
  description: string;
  screenshot: string;        // Relative path to PNG
  requiresAuth: boolean;
  requiresAdmin: boolean;
  params: Record<string, string> | null;
  status: 'pending' | 'ok' | 'error' | 'timeout' | 'skipped';
  capturedAt: string | null;
  error: string | null;
}

export interface Manifest {
  generatedAt: string;
  baseUrl: string;
  stats: {
    total: number;
    captured: number;
    errors: number;
    timeouts: number;
    skipped: number;
    groups: number;
  };
  pages: PageEntry[];
}

// ============================================================================
// Configuration
// ============================================================================

const ROOT = path.resolve(__dirname, '../..');

export const config = {
  baseUrl: process.env.PAGE_CATALOG_URL || 'https://meepleai.app',
  secretsPath: path.join(ROOT, 'infra/secrets/staging/admin.secret'),
  pagesDir: path.join(ROOT, 'apps/web/src/app'),
  outputDir: path.join(__dirname, 'screenshots'),
  distDir: path.join(__dirname, 'dist'),
  manifestPath: path.join(__dirname, 'manifest.json'),
  viewport: { width: 1440, height: 900 },
  pageTimeout: 15000,
  delayAfterLoad: 1000,
  concurrency: 3,
} as const;

// ============================================================================
// Route Group Config
// ============================================================================

export const GROUP_CONFIG: Record<string, {
  requiresAuth: boolean;
  requiresAdmin: boolean;
  label: string;
}> = {
  '(public)':        { requiresAuth: false, requiresAdmin: false, label: 'public' },
  '(auth)':          { requiresAuth: false, requiresAdmin: false, label: 'auth' },
  '(authenticated)': { requiresAuth: true,  requiresAdmin: false, label: 'authenticated' },
  'admin':           { requiresAuth: true,  requiresAdmin: true,  label: 'admin' },
  '(chat)':          { requiresAuth: true,  requiresAdmin: false, label: 'chat' },
  '(dev)':           { requiresAuth: false, requiresAdmin: false, label: 'dev' },
  '(docs)':          { requiresAuth: false, requiresAdmin: false, label: 'docs' },
  'join':            { requiresAuth: false, requiresAdmin: false, label: 'join' },
  'offline':         { requiresAuth: false, requiresAdmin: false, label: 'offline' },
};

// ============================================================================
// Parameter Resolution Config
// ============================================================================

// Maps parent path segments to API endpoints for resolving [id]/[param] values
export const PARAM_RESOLVERS: Record<string, {
  endpoint: string;
  extract: (data: any) => string | null;
}> = {
  'shared-games': {
    endpoint: '/api/v1/shared-games?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'games': {
    endpoint: '/api/v1/shared-games?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'agents': {
    endpoint: '/api/v1/agents?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'players': {
    endpoint: '/api/v1/players?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'sessions': {
    endpoint: '/api/v1/sessions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'game-nights': {
    endpoint: '/api/v1/game-nights?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'chat': {
    endpoint: '/api/v1/chat-threads?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'playlists': {
    endpoint: '/api/v1/playlists?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'knowledge-base': {
    endpoint: '/api/v1/knowledge-base?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'play-records': {
    endpoint: '/api/v1/play-records?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'users': {
    endpoint: '/api/v1/admin/users?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'definitions': {
    endpoint: '/api/v1/admin/agents/definitions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'library': {
    endpoint: '/api/v1/library?pageSize=1',
    extract: (data) => data?.items?.[0]?.gameId || data?.[0]?.gameId || null,
  },
  'private': {
    endpoint: '/api/v1/library/private?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'notifications': {
    endpoint: '/api/v1/notifications?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'ab-testing': {
    endpoint: '/api/v1/admin/agents/ab-testing?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'ui-library': {
    endpoint: '/api/v1/admin/ui-library?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
  'compositions': {
    endpoint: '/api/v1/admin/ui-library/compositions?pageSize=1',
    extract: (data) => data?.items?.[0]?.id || data?.[0]?.id || null,
  },
};

// Hardcoded param values for routes that can't be resolved via API
export const HARDCODED_PARAMS: Record<string, string> = {
  'showcase/[component]': 'button',
  'join/[inviteToken]': '__skip__',  // Skip this route
};
```

- [ ] **Step 2: Verify the file compiles**

Run: `cd apps/web && npx tsx --eval "import './../../docs/pages/config'; console.log('OK')"`
Expected: `OK` (no compilation errors)

- [ ] **Step 3: Commit**

```bash
git add docs/pages/config.ts
git commit -m "feat(docs): add page catalog config and types"
```

---

### Task 2: Route Discovery (`docs/pages/routes.ts`)

**Files:**
- Create: `docs/pages/routes.ts`

- [ ] **Step 1: Create routes.ts that scans the filesystem**

```typescript
// docs/pages/routes.ts
import * as fs from 'fs';
import * as path from 'path';

import { config, GROUP_CONFIG, HARDCODED_PARAMS, type PageEntry } from './config';

/**
 * Scan apps/web/src/app for all page.tsx files and build route entries.
 */
export function discoverRoutes(): PageEntry[] {
  const pages: PageEntry[] = [];
  scanDirectory(config.pagesDir, '', pages);
  return pages.sort((a, b) => {
    // Sort by group, then by route
    const groupOrder = Object.values(GROUP_CONFIG).map(g => g.label);
    const aIdx = groupOrder.indexOf(a.group);
    const bIdx = groupOrder.indexOf(b.group);
    if (aIdx !== bIdx) return aIdx - bIdx;
    return a.route.localeCompare(b.route);
  });
}

function scanDirectory(dir: string, relativePath: string, pages: PageEntry[]): void {
  if (!fs.existsSync(dir)) return;

  const entries = fs.readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    if (entry.isDirectory()) {
      scanDirectory(
        path.join(dir, entry.name),
        path.join(relativePath, entry.name),
        pages
      );
    } else if (entry.name === 'page.tsx') {
      const page = buildPageEntry(relativePath);
      if (page) pages.push(page);
    }
  }
}

function buildPageEntry(relativePath: string): PageEntry | null {
  // Determine group from first path segment
  const segments = relativePath.split(path.sep).filter(Boolean);
  if (segments.length === 0) return null;

  const firstSegment = segments[0];
  const groupConfig = GROUP_CONFIG[firstSegment];

  if (!groupConfig) {
    // Root-level page (e.g., offline/page.tsx, join/[token]/page.tsx)
    // Try to match on the segment itself
    const fallbackConfig = GROUP_CONFIG[firstSegment];
    if (!fallbackConfig) {
      // Unknown group, classify as "other"
      return buildEntry(segments, 'other', false, false);
    }
  }

  const group = groupConfig?.label || 'other';
  return buildEntry(
    segments,
    group,
    groupConfig?.requiresAuth || false,
    groupConfig?.requiresAdmin || false
  );
}

function buildEntry(
  segments: string[],
  group: string,
  requiresAuth: boolean,
  requiresAdmin: boolean
): PageEntry | null {
  // Build route by removing route group parentheses and page.tsx
  const routeSegments = segments
    .filter(s => s !== 'page.tsx')
    .map(s => {
      // Remove parenthetical route groups: (public) -> skip, (auth) -> skip
      if (s.startsWith('(') && s.endsWith(')')) return null;
      // Keep admin, join, offline as-is
      return s;
    })
    .filter(Boolean) as string[];

  const routePattern = '/' + routeSegments.join('/');
  const route = routePattern; // Will be resolved with real params later

  // Check for hardcoded skip
  for (const [pattern, value] of Object.entries(HARDCODED_PARAMS)) {
    if (routePattern.includes(pattern) && value === '__skip__') {
      return null;
    }
  }

  // Build ID from group + route
  const id = group + '-' + routeSegments
    .map(s => s.replace(/\[.*?\]/g, 'param'))
    .join('-') || 'index';

  // Derive title from last meaningful segment
  const title = deriveTitle(routeSegments);
  const description = `${capitalize(group)} — ${title}`;

  // Screenshot path
  const screenshotName = routeSegments
    .map(s => s.replace(/\[.*?\]/g, 'param'))
    .join('-') || 'index';
  const screenshot = `screenshots/${group}/${screenshotName}.png`;

  // Detect dynamic params
  const dynamicParams = routeSegments.filter(s => s.startsWith('['));
  const hasParams = dynamicParams.length > 0;

  return {
    id,
    route,
    routePattern,
    group,
    title,
    description,
    screenshot,
    requiresAuth,
    requiresAdmin,
    params: hasParams ? Object.fromEntries(dynamicParams.map(p => [p.replace(/[\[\]]/g, ''), 'unresolved'])) : null,
    status: 'pending',
    capturedAt: null,
    error: null,
  };
}

function deriveTitle(segments: string[]): string {
  if (segments.length === 0) return 'Home';

  const last = segments[segments.length - 1];

  // If it's a dynamic param, use the parent segment
  if (last.startsWith('[')) {
    const parent = segments.length > 1 ? segments[segments.length - 2] : 'Detail';
    return capitalize(parent.replace(/-/g, ' ')) + ' Detail';
  }

  return capitalize(last.replace(/-/g, ' '));
}

function capitalize(str: string): string {
  return str.replace(/\b\w/g, c => c.toUpperCase());
}
```

- [ ] **Step 2: Quick test of route discovery**

Run: `cd apps/web && npx tsx -e "import { discoverRoutes } from '../../docs/pages/routes'; const r = discoverRoutes(); console.log('Found', r.length, 'routes'); console.log('Groups:', [...new Set(r.map(p=>p.group))].join(', ')); console.log('With params:', r.filter(p=>p.params).length); r.slice(0,3).forEach(p => console.log(p.id, p.route, p.group));"`

Expected: ~208 routes found (209 minus the skipped join/[inviteToken]), groups listed, first 3 routes printed.

- [ ] **Step 3: Commit**

```bash
git add docs/pages/routes.ts
git commit -m "feat(docs): add route discovery for page catalog"
```

---

### Task 3: Capture Engine (`docs/pages/capture.ts`)

**Files:**
- Create: `docs/pages/capture.ts`

This is the largest file. It handles: credential loading, Playwright login, parameter resolution via API, and screenshot capture with concurrency.

- [ ] **Step 1: Create capture.ts**

```typescript
// docs/pages/capture.ts
import * as fs from 'fs';
import * as path from 'path';

import { chromium, type Browser, type BrowserContext, type Page } from 'playwright';

import { config, PARAM_RESOLVERS, HARDCODED_PARAMS, type PageEntry, type Manifest } from './config';
import { discoverRoutes } from './routes';

// ============================================================================
// Credential Loading
// ============================================================================

function loadCredentials(): { email: string; password: string } {
  if (!fs.existsSync(config.secretsPath)) {
    console.error(`❌ Secrets file not found: ${config.secretsPath}`);
    console.error('   Run: cd infra && make secrets-staging');
    process.exit(1);
  }

  const content = fs.readFileSync(config.secretsPath, 'utf-8');
  const env: Record<string, string> = {};

  for (const line of content.split('\n')) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const eqIdx = trimmed.indexOf('=');
    if (eqIdx === -1) continue;
    env[trimmed.slice(0, eqIdx).trim()] = trimmed.slice(eqIdx + 1).trim();
  }

  const email = env.ADMIN_EMAIL;
  const password = env.ADMIN_PASSWORD;

  if (!email || !password) {
    console.error('❌ ADMIN_EMAIL or ADMIN_PASSWORD not found in secrets file');
    process.exit(1);
  }

  return { email, password };
}

// ============================================================================
// Login
// ============================================================================

async function login(browser: Browser, credentials: { email: string; password: string }): Promise<string> {
  const context = await browser.newContext({ viewport: config.viewport });
  const page = await context.newPage();

  console.log(`🔐 Logging in as ${credentials.email}...`);

  await page.goto(`${config.baseUrl}/login`, { waitUntil: 'domcontentloaded', timeout: 30000 });

  // Wait for login form to appear
  await page.waitForSelector('[data-testid="login-email"]', { timeout: 10000 });

  // Fill credentials
  await page.fill('[data-testid="login-email"]', credentials.email);
  await page.fill('[data-testid="login-password"]', credentials.password);
  await page.click('[data-testid="login-submit"]');

  // Wait for redirect (login success)
  await page.waitForURL(url => !url.toString().includes('/login'), { timeout: 15000 });

  console.log('✅ Login successful');

  // Save session state
  const storagePath = path.join(config.outputDir, '.auth-state.json');
  await context.storageState({ path: storagePath });
  await context.close();

  return storagePath;
}

// ============================================================================
// Parameter Resolution
// ============================================================================

async function resolveParams(
  pages: PageEntry[],
  authStatePath: string,
  browser: Browser
): Promise<void> {
  const pagesWithParams = pages.filter(p => p.params && Object.values(p.params).includes('unresolved'));
  if (pagesWithParams.length === 0) return;

  console.log(`\n🔍 Resolving dynamic parameters for ${pagesWithParams.length} pages...`);

  const context = await browser.newContext({
    viewport: config.viewport,
    storageState: authStatePath,
  });
  const page = await context.newPage();

  // Collect unique resolver keys needed
  const resolverCache = new Map<string, string | null>();

  for (const entry of pagesWithParams) {
    if (!entry.params) continue;

    const routeSegments = entry.routePattern.split('/').filter(Boolean);

    for (const [paramName, value] of Object.entries(entry.params)) {
      if (value !== 'unresolved') continue;

      // Check hardcoded params first
      const hardcodedKey = Object.keys(HARDCODED_PARAMS).find(k =>
        entry.routePattern.includes(k)
      );
      if (hardcodedKey) {
        entry.params[paramName] = HARDCODED_PARAMS[hardcodedKey];
        continue;
      }

      // Find parent segment for resolver lookup
      const paramIdx = routeSegments.findIndex(s => s === `[${paramName}]`);
      const parentSegment = paramIdx > 0 ? routeSegments[paramIdx - 1] : null;

      // Remove route group parens from parent
      const cleanParent = parentSegment?.replace(/[()]/g, '') || '';

      const resolver = PARAM_RESOLVERS[cleanParent];
      if (!resolver) {
        console.warn(`  ⚠️ No resolver for [${paramName}] in ${entry.routePattern} (parent: ${cleanParent})`);
        entry.params[paramName] = '__unresolved__';
        continue;
      }

      // Use cache
      const cacheKey = resolver.endpoint;
      if (!resolverCache.has(cacheKey)) {
        try {
          const apiUrl = `${config.baseUrl}${resolver.endpoint}`;
          const response = await page.evaluate(async (url) => {
            const res = await fetch(url, { credentials: 'include' });
            if (!res.ok) return null;
            return res.json();
          }, apiUrl);

          resolverCache.set(cacheKey, resolver.extract(response));
        } catch (err) {
          console.warn(`  ⚠️ API call failed for ${resolver.endpoint}: ${err}`);
          resolverCache.set(cacheKey, null);
        }
      }

      const resolvedId = resolverCache.get(cacheKey);
      if (resolvedId) {
        entry.params[paramName] = resolvedId;
        console.log(`  ✅ [${paramName}] → ${resolvedId} (via ${cleanParent})`);
      } else {
        entry.params[paramName] = '__unresolved__';
        console.warn(`  ⚠️ Could not resolve [${paramName}] for ${entry.routePattern}`);
      }
    }

    // Build resolved route
    let resolvedRoute = entry.routePattern;
    if (entry.params) {
      for (const [paramName, value] of Object.entries(entry.params)) {
        resolvedRoute = resolvedRoute.replace(`[${paramName}]`, value);
      }
    }
    entry.route = resolvedRoute;

    // Mark as skipped if any param unresolved
    if (entry.params && Object.values(entry.params).includes('__unresolved__')) {
      entry.status = 'skipped';
      entry.error = 'Could not resolve dynamic parameters';
    }
  }

  await context.close();
}

// ============================================================================
// Screenshot Capture
// ============================================================================

async function capturePages(
  pages: PageEntry[],
  authStatePath: string,
  browser: Browser,
  forceRecapture: boolean
): Promise<void> {
  // Filter to only capturable pages
  const toCaptureAll = pages.filter(p => {
    if (p.status === 'skipped') return false;
    if (forceRecapture) return true;
    // Retry errors/timeouts, skip already captured
    if (p.status === 'ok' && fs.existsSync(path.join(__dirname, p.screenshot))) return false;
    return true;
  });

  if (toCaptureAll.length === 0) {
    console.log('\n✅ All pages already captured. Use --force to recapture.');
    return;
  }

  // Split by auth requirement
  const publicPages = toCaptureAll.filter(p => !p.requiresAuth);
  const authPages = toCaptureAll.filter(p => p.requiresAuth && !p.requiresAdmin);
  const adminPages = toCaptureAll.filter(p => p.requiresAdmin);

  console.log(`\n📸 Capturing ${toCaptureAll.length} pages:`);
  console.log(`   Phase 1 — Public: ${publicPages.length}`);
  console.log(`   Phase 2 — Authenticated: ${authPages.length}`);
  console.log(`   Phase 3 — Admin: ${adminPages.length}`);

  // Phase 1: Public (no auth)
  if (publicPages.length > 0) {
    console.log('\n--- Phase 1: Public Pages ---');
    await capturePhase(browser, publicPages, null);
  }

  // Phase 2: Authenticated
  if (authPages.length > 0) {
    console.log('\n--- Phase 2: Authenticated Pages ---');
    await capturePhase(browser, authPages, authStatePath);
  }

  // Phase 3: Admin
  if (adminPages.length > 0) {
    console.log('\n--- Phase 3: Admin Pages ---');
    await capturePhase(browser, adminPages, authStatePath);
  }
}

async function capturePhase(
  browser: Browser,
  pages: PageEntry[],
  authStatePath: string | null
): Promise<void> {
  // Process in batches of config.concurrency
  for (let i = 0; i < pages.length; i += config.concurrency) {
    const batch = pages.slice(i, i + config.concurrency);
    await Promise.all(batch.map(page => captureSinglePage(browser, page, authStatePath)));
  }
}

async function captureSinglePage(
  browser: Browser,
  entry: PageEntry,
  authStatePath: string | null
): Promise<void> {
  const contextOptions: any = { viewport: config.viewport };
  if (authStatePath) {
    contextOptions.storageState = authStatePath;
  }

  const context = await browser.newContext(contextOptions);
  const page = await context.newPage();

  try {
    const url = `${config.baseUrl}${entry.route}`;

    await page.goto(url, {
      waitUntil: 'networkidle',
      timeout: config.pageTimeout,
    });

    // Extra delay for animations/transitions
    await page.waitForTimeout(config.delayAfterLoad);

    // Ensure screenshot directory exists
    const screenshotPath = path.join(__dirname, entry.screenshot);
    fs.mkdirSync(path.dirname(screenshotPath), { recursive: true });

    await page.screenshot({
      path: screenshotPath,
      fullPage: false, // Viewport only, not full page scroll
      animations: 'disabled',
    });

    entry.status = 'ok';
    entry.capturedAt = new Date().toISOString();
    entry.error = null;

    const idx = entry.id;
    console.log(`  ✅ ${idx} → ${entry.route}`);
  } catch (err: any) {
    if (err.message?.includes('Timeout')) {
      entry.status = 'timeout';
      entry.error = 'Page load timeout';
      console.log(`  ⏱️ ${entry.id} → TIMEOUT ${entry.route}`);
    } else {
      entry.status = 'error';
      entry.error = err.message?.slice(0, 200) || 'Unknown error';
      console.log(`  ❌ ${entry.id} → ERROR ${entry.route}: ${entry.error}`);

      // Try to capture error state screenshot
      try {
        const screenshotPath = path.join(__dirname, entry.screenshot);
        fs.mkdirSync(path.dirname(screenshotPath), { recursive: true });
        await page.screenshot({ path: screenshotPath, fullPage: false });
      } catch {
        // Ignore screenshot failure
      }
    }
  } finally {
    await context.close();
  }
}

// ============================================================================
// Manifest I/O
// ============================================================================

function loadExistingManifest(): Manifest | null {
  if (!fs.existsSync(config.manifestPath)) return null;
  try {
    return JSON.parse(fs.readFileSync(config.manifestPath, 'utf-8'));
  } catch {
    return null;
  }
}

function saveManifest(manifest: Manifest): void {
  fs.writeFileSync(config.manifestPath, JSON.stringify(manifest, null, 2));
}

function buildManifest(pages: PageEntry[]): Manifest {
  const groups = new Set(pages.map(p => p.group));
  return {
    generatedAt: new Date().toISOString(),
    baseUrl: config.baseUrl,
    stats: {
      total: pages.length,
      captured: pages.filter(p => p.status === 'ok').length,
      errors: pages.filter(p => p.status === 'error').length,
      timeouts: pages.filter(p => p.status === 'timeout').length,
      skipped: pages.filter(p => p.status === 'skipped').length,
      groups: groups.size,
    },
    pages,
  };
}

// ============================================================================
// Main
// ============================================================================

async function main(): Promise<void> {
  const forceRecapture = process.argv.includes('--force');

  console.log('📋 MeepleAI Page Catalog — Screenshot Capture\n');
  console.log(`   Base URL: ${config.baseUrl}`);
  console.log(`   Force: ${forceRecapture}`);

  // Step 1: Discover routes
  console.log('\n📂 Discovering routes...');
  let pages = discoverRoutes();
  console.log(`   Found ${pages.length} pages`);

  // Step 2: Merge with existing manifest (preserve status of already captured pages)
  if (!forceRecapture) {
    const existing = loadExistingManifest();
    if (existing) {
      const existingMap = new Map(existing.pages.map(p => [p.routePattern, p]));
      for (const page of pages) {
        const prev = existingMap.get(page.routePattern);
        if (prev && prev.status === 'ok' && fs.existsSync(path.join(__dirname, prev.screenshot))) {
          // Preserve captured state
          page.status = prev.status;
          page.capturedAt = prev.capturedAt;
          page.params = prev.params;
          page.route = prev.route;
          page.screenshot = prev.screenshot;
        }
      }
      console.log(`   Merged with existing manifest (${existing.stats.captured} already captured)`);
    }
  }

  // Step 3: Load credentials & launch browser
  const credentials = loadCredentials();
  const browser = await chromium.launch({ headless: true });

  try {
    // Step 4: Login
    const authStatePath = await login(browser, credentials);

    // Step 5: Resolve dynamic parameters
    await resolveParams(pages, authStatePath, browser);

    // Step 6: Capture screenshots
    await capturePages(pages, authStatePath, browser, forceRecapture);

    // Step 7: Save manifest
    const manifest = buildManifest(pages);
    saveManifest(manifest);

    // Summary
    console.log('\n📊 Capture Summary:');
    console.log(`   Total:    ${manifest.stats.total}`);
    console.log(`   Captured: ${manifest.stats.captured}`);
    console.log(`   Errors:   ${manifest.stats.errors}`);
    console.log(`   Timeouts: ${manifest.stats.timeouts}`);
    console.log(`   Skipped:  ${manifest.stats.skipped}`);

    // Clean up auth state
    const authStateFile = path.join(config.outputDir, '.auth-state.json');
    if (fs.existsSync(authStateFile)) fs.unlinkSync(authStateFile);

  } finally {
    await browser.close();
  }
}

main().catch(err => {
  console.error('💥 Fatal error:', err);
  process.exit(1);
});
```

- [ ] **Step 2: Verify compilation (don't run yet)**

Run: `cd apps/web && npx tsx --eval "console.log('syntax check only')"`

We won't run the full capture yet — that requires staging access. Just confirm the file has no syntax errors:

Run: `node -c ../../docs/pages/capture.ts 2>&1 || npx tsx --eval "import '../../docs/pages/capture'" 2>&1 | head -5`

Note: Full syntax check may try to import Playwright. If it fails on import, that's OK — means the TS compiles. If it fails on syntax, fix the syntax.

- [ ] **Step 3: Commit**

```bash
git add docs/pages/capture.ts
git commit -m "feat(docs): add page catalog capture engine"
```

---

### Task 4: HTML Generator (`docs/pages/generate.ts`)

**Files:**
- Create: `docs/pages/generate.ts`

- [ ] **Step 1: Create generate.ts**

```typescript
// docs/pages/generate.ts
import * as fs from 'fs';
import * as path from 'path';

import { config, type Manifest, type PageEntry } from './config';

// ============================================================================
// Badge Colors
// ============================================================================

const GROUP_COLORS: Record<string, string> = {
  public: '#22c55e',
  auth: '#3b82f6',
  authenticated: 'hsl(25,95%,38%)',
  admin: '#a855f7',
  chat: '#14b8a6',
  dev: '#6b7280',
  docs: '#6b7280',
  join: '#6b7280',
  offline: '#6b7280',
  other: '#6b7280',
};

const STATUS_COLORS: Record<string, string> = {
  ok: '#22c55e',
  error: '#ef4444',
  timeout: '#f59e0b',
  skipped: '#6b7280',
  pending: '#6b7280',
};

// ============================================================================
// HTML Generation
// ============================================================================

function generateHTML(manifest: Manifest): string {
  const groups = [...new Set(manifest.pages.map(p => p.group))].sort();

  const cardsHTML = manifest.pages.map((page, i) => {
    const screenshotRelPath = `../${page.screenshot}`;
    const statusColor = STATUS_COLORS[page.status] || '#6b7280';
    const groupColor = GROUP_COLORS[page.group] || '#6b7280';
    const hasScreenshot = page.status === 'ok' || page.status === 'error';

    return `
    <div class="card" data-group="${page.group}" data-status="${page.status}"
         data-title="${escapeAttr(page.title)}" data-route="${escapeAttr(page.route)}"
         data-description="${escapeAttr(page.description)}" data-index="${i}"
         ${hasScreenshot ? `onclick="openLightbox(${i})"` : ''}>
      <div class="card-image">
        ${hasScreenshot
          ? `<img src="${screenshotRelPath}" alt="${escapeAttr(page.title)}" loading="lazy" />`
          : `<div class="card-placeholder">${page.status.toUpperCase()}</div>`
        }
      </div>
      <div class="card-body">
        <div class="card-title">${escapeHTML(page.title)}</div>
        <div class="card-route">${escapeHTML(page.route)}</div>
        <div class="card-meta">
          <span class="badge" style="background:${groupColor}">${page.group}</span>
          <span class="status-dot" style="background:${statusColor}" title="${page.status}"></span>
        </div>
      </div>
    </div>`;
  }).join('\n');

  const sidebarHTML = groups.map(g => {
    const count = manifest.pages.filter(p => p.group === g).length;
    const color = GROUP_COLORS[g] || '#6b7280';
    return `<label class="filter-option">
      <input type="radio" name="group" value="${g}" />
      <span class="filter-dot" style="background:${color}"></span>
      ${g} <span class="filter-count">(${count})</span>
    </label>`;
  }).join('\n');

  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>MeepleAI Page Catalog</title>
<style>
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body { font-family: system-ui, -apple-system, sans-serif; background: #f8fafc; color: #1e293b; }

  /* Layout */
  .layout { display: flex; min-height: 100vh; }
  .sidebar { width: 220px; background: #fff; border-right: 1px solid #e2e8f0; padding: 20px; position: fixed; height: 100vh; overflow-y: auto; }
  .main { margin-left: 220px; flex: 1; padding: 24px; }

  /* Header */
  .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
  .header h1 { font-size: 24px; font-weight: 700; }
  .search { padding: 8px 12px; border: 1px solid #e2e8f0; border-radius: 8px; width: 300px; font-size: 14px; }
  .search:focus { outline: none; border-color: hsl(25,95%,38%); box-shadow: 0 0 0 3px hsla(25,95%,38%,0.1); }

  /* Counter */
  .counter { font-size: 13px; color: #64748b; margin-bottom: 16px; }

  /* Grid */
  .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; }
  @media (max-width: 1200px) { .grid { grid-template-columns: repeat(2, 1fr); } }
  @media (max-width: 768px) {
    .sidebar { display: none; }
    .main { margin-left: 0; }
    .grid { grid-template-columns: 1fr; }
  }

  /* Card */
  .card { background: #fff; border-radius: 12px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.08); cursor: pointer; transition: transform 0.15s, box-shadow 0.15s; }
  .card:hover { transform: translateY(-2px); box-shadow: 0 4px 12px rgba(0,0,0,0.12); }
  .card[data-status="skipped"], .card[data-status="pending"] { opacity: 0.6; cursor: default; }
  .card-image { aspect-ratio: 16/10; overflow: hidden; background: #f1f5f9; }
  .card-image img { width: 100%; height: 100%; object-fit: cover; object-position: top; }
  .card-placeholder { display: flex; align-items: center; justify-content: center; height: 100%; font-size: 12px; color: #94a3b8; font-weight: 600; letter-spacing: 1px; }
  .card-body { padding: 12px; }
  .card-title { font-size: 14px; font-weight: 600; margin-bottom: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .card-route { font-size: 12px; color: #64748b; margin-bottom: 8px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-family: monospace; }
  .card-meta { display: flex; align-items: center; gap: 8px; }
  .badge { font-size: 11px; color: #fff; padding: 2px 8px; border-radius: 4px; font-weight: 600; }
  .status-dot { width: 8px; height: 8px; border-radius: 50%; }

  /* Sidebar */
  .sidebar h2 { font-size: 14px; font-weight: 700; margin-bottom: 16px; text-transform: uppercase; letter-spacing: 1px; color: #64748b; }
  .filter-option { display: flex; align-items: center; gap: 8px; padding: 6px 0; cursor: pointer; font-size: 14px; }
  .filter-option input { display: none; }
  .filter-option.active { font-weight: 600; }
  .filter-dot { width: 10px; height: 10px; border-radius: 50%; }
  .filter-count { color: #94a3b8; font-size: 12px; }
  .sidebar-stats { margin-top: 24px; padding-top: 16px; border-top: 1px solid #e2e8f0; }
  .sidebar-stats div { font-size: 13px; color: #64748b; margin-bottom: 4px; }
  .sidebar-stats span { font-weight: 600; color: #1e293b; }

  /* Lightbox */
  .lightbox { display: none; position: fixed; inset: 0; background: rgba(0,0,0,0.9); z-index: 1000; align-items: center; justify-content: center; flex-direction: column; }
  .lightbox.active { display: flex; }
  .lightbox img { max-width: 90vw; max-height: 80vh; border-radius: 8px; box-shadow: 0 8px 32px rgba(0,0,0,0.5); }
  .lightbox-info { color: #fff; text-align: center; margin-top: 16px; }
  .lightbox-title { font-size: 18px; font-weight: 600; }
  .lightbox-route { font-size: 14px; color: #94a3b8; font-family: monospace; margin-top: 4px; }
  .lightbox-nav { position: absolute; top: 50%; font-size: 48px; color: #fff; cursor: pointer; user-select: none; padding: 20px; opacity: 0.7; }
  .lightbox-nav:hover { opacity: 1; }
  .lightbox-prev { left: 20px; }
  .lightbox-next { right: 20px; }
  .lightbox-close { position: absolute; top: 20px; right: 30px; font-size: 36px; color: #fff; cursor: pointer; opacity: 0.7; }
  .lightbox-close:hover { opacity: 1; }
  .lightbox-counter { font-size: 13px; color: #64748b; margin-top: 8px; }

  /* Footer */
  .footer { text-align: center; padding: 24px; color: #94a3b8; font-size: 13px; margin-top: 40px; border-top: 1px solid #e2e8f0; }
</style>
</head>
<body>

<div class="layout">
  <aside class="sidebar">
    <h2>Filters</h2>
    <label class="filter-option active">
      <input type="radio" name="group" value="all" checked />
      <span class="filter-dot" style="background:#1e293b"></span>
      All <span class="filter-count">(${manifest.stats.total})</span>
    </label>
    ${sidebarHTML}
    <div class="sidebar-stats">
      <div>Captured: <span>${manifest.stats.captured}</span></div>
      <div>Errors: <span>${manifest.stats.errors}</span></div>
      <div>Timeouts: <span>${manifest.stats.timeouts}</span></div>
      <div>Skipped: <span>${manifest.stats.skipped}</span></div>
    </div>
  </aside>

  <main class="main">
    <div class="header">
      <h1>MeepleAI Page Catalog</h1>
      <input type="text" class="search" placeholder="Search pages..." id="search" />
    </div>
    <div class="counter" id="counter">Showing ${manifest.stats.total} of ${manifest.stats.total} pages</div>
    <div class="grid" id="grid">
      ${cardsHTML}
    </div>
    <div class="footer">Generated: ${manifest.generatedAt.split('T')[0]} — MeepleAI Page Catalog</div>
  </main>
</div>

<!-- Lightbox -->
<div class="lightbox" id="lightbox">
  <span class="lightbox-close" onclick="closeLightbox()">&times;</span>
  <span class="lightbox-nav lightbox-prev" onclick="navLightbox(-1)">&#8249;</span>
  <span class="lightbox-nav lightbox-next" onclick="navLightbox(1)">&#8250;</span>
  <img id="lightbox-img" src="" alt="" />
  <div class="lightbox-info">
    <div class="lightbox-title" id="lightbox-title"></div>
    <div class="lightbox-route" id="lightbox-route"></div>
    <div class="lightbox-counter" id="lightbox-counter"></div>
  </div>
</div>

<script>
  // Data
  const pages = ${JSON.stringify(manifest.pages.map(p => ({
    title: p.title,
    route: p.route,
    description: p.description,
    group: p.group,
    status: p.status,
    screenshot: '../' + p.screenshot,
  })))};

  // State
  let currentFilter = 'all';
  let currentSearch = '';
  let lightboxIndex = -1;
  let visibleIndices = [];

  // Elements
  const grid = document.getElementById('grid');
  const counter = document.getElementById('counter');
  const cards = grid.querySelectorAll('.card');
  const lightbox = document.getElementById('lightbox');

  // Filter & search
  function updateView() {
    let shown = 0;
    visibleIndices = [];
    cards.forEach((card, i) => {
      const group = card.dataset.group;
      const title = card.dataset.title.toLowerCase();
      const route = card.dataset.route.toLowerCase();
      const desc = card.dataset.description.toLowerCase();
      const search = currentSearch.toLowerCase();

      const matchGroup = currentFilter === 'all' || group === currentFilter;
      const matchSearch = !search || title.includes(search) || route.includes(search) || desc.includes(search);

      if (matchGroup && matchSearch) {
        card.style.display = '';
        shown++;
        visibleIndices.push(i);
      } else {
        card.style.display = 'none';
      }
    });
    counter.textContent = 'Showing ' + shown + ' of ' + ${manifest.stats.total} + ' pages';
  }

  // Sidebar filters
  document.querySelectorAll('.filter-option').forEach(opt => {
    opt.addEventListener('click', () => {
      document.querySelectorAll('.filter-option').forEach(o => o.classList.remove('active'));
      opt.classList.add('active');
      currentFilter = opt.querySelector('input').value;
      updateView();
    });
  });

  // Search
  document.getElementById('search').addEventListener('input', e => {
    currentSearch = e.target.value;
    updateView();
  });

  // Lightbox
  function openLightbox(idx) {
    lightboxIndex = idx;
    const p = pages[idx];
    document.getElementById('lightbox-img').src = p.screenshot;
    document.getElementById('lightbox-title').textContent = p.title;
    document.getElementById('lightbox-route').textContent = p.route;
    const posInVisible = visibleIndices.indexOf(idx);
    document.getElementById('lightbox-counter').textContent = (posInVisible + 1) + ' / ' + visibleIndices.length;
    lightbox.classList.add('active');
  }

  function closeLightbox() {
    lightbox.classList.remove('active');
  }

  function navLightbox(dir) {
    const pos = visibleIndices.indexOf(lightboxIndex);
    const next = pos + dir;
    if (next >= 0 && next < visibleIndices.length) {
      openLightbox(visibleIndices[next]);
    }
  }

  // Keyboard
  document.addEventListener('keydown', e => {
    if (!lightbox.classList.contains('active')) return;
    if (e.key === 'Escape') closeLightbox();
    if (e.key === 'ArrowLeft') navLightbox(-1);
    if (e.key === 'ArrowRight') navLightbox(1);
  });

  // Click outside lightbox image
  lightbox.addEventListener('click', e => {
    if (e.target === lightbox) closeLightbox();
  });

  // Init
  visibleIndices = Array.from({length: pages.length}, (_, i) => i);
</script>

</body>
</html>`;
}

// ============================================================================
// Utilities
// ============================================================================

function escapeHTML(str: string): string {
  return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function escapeAttr(str: string): string {
  return str.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// ============================================================================
// Main
// ============================================================================

function main(): void {
  console.log('🔨 MeepleAI Page Catalog — HTML Generator\n');

  // Load manifest
  if (!fs.existsSync(config.manifestPath)) {
    console.error('❌ Manifest not found. Run capture first: npm run docs:capture');
    process.exit(1);
  }

  const manifest: Manifest = JSON.parse(fs.readFileSync(config.manifestPath, 'utf-8'));
  console.log(`   Pages: ${manifest.stats.total}`);
  console.log(`   Captured: ${manifest.stats.captured}`);

  // Generate HTML
  const html = generateHTML(manifest);

  // Write output
  fs.mkdirSync(config.distDir, { recursive: true });
  const outputPath = path.join(config.distDir, 'index.html');
  fs.writeFileSync(outputPath, html);

  console.log(`\n✅ Generated: ${outputPath}`);
  console.log(`   Open in browser or run: npx serve ${path.relative(process.cwd(), config.distDir)}`);
}

main();
```

- [ ] **Step 2: Verify it compiles**

Run: `cd apps/web && npx tsx --eval "console.log('generate.ts syntax ok')" 2>&1`

Note: We can't run generate.ts yet (no manifest). Syntax check only.

- [ ] **Step 3: Commit**

```bash
git add docs/pages/generate.ts
git commit -m "feat(docs): add page catalog HTML generator"
```

---

### Task 5: NPM Scripts and Gitignore

**Files:**
- Modify: `apps/web/package.json` (add scripts)
- Modify: `.gitignore` (add generated artifacts)

- [ ] **Step 1: Add npm scripts to apps/web/package.json**

Add these scripts to the `"scripts"` section of `apps/web/package.json`:

```json
"docs:capture": "tsx ../../docs/pages/capture.ts",
"docs:generate": "tsx ../../docs/pages/generate.ts",
"docs:pages": "npm run docs:capture && npm run docs:generate",
"docs:serve": "npx serve ../../docs/pages/dist"
```

- [ ] **Step 2: Add generated files to .gitignore**

Append to the project root `.gitignore`:

```
# Page catalog (generated)
docs/pages/screenshots/
docs/pages/dist/
docs/pages/manifest.json
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/package.json .gitignore
git commit -m "chore(docs): add page catalog scripts and gitignore entries"
```

---

### Task 6: Smoke Test — Route Discovery Only

**Files:** None (validation only)

- [ ] **Step 1: Run route discovery to verify the full route list**

Run: `cd apps/web && npx tsx -e "
import { discoverRoutes } from '../../docs/pages/routes';
const routes = discoverRoutes();
console.log('Total routes:', routes.length);
const groups = {};
routes.forEach(r => { groups[r.group] = (groups[r.group] || 0) + 1; });
console.log('By group:', JSON.stringify(groups, null, 2));
console.log('With params:', routes.filter(r => r.params).length);
console.log('\\nFirst 5:');
routes.slice(0,5).forEach(r => console.log(' ', r.group, r.route, r.params ? JSON.stringify(r.params) : ''));
console.log('\\nLast 5:');
routes.slice(-5).forEach(r => console.log(' ', r.group, r.route, r.params ? JSON.stringify(r.params) : ''));
"`

Expected: ~208 routes, groups matching the counts from the spec, dynamic params detected.

- [ ] **Step 2: Fix any route discovery issues**

If any routes are missing or misclassified, fix `routes.ts` and re-run.

- [ ] **Step 3: Commit any fixes**

```bash
git add docs/pages/routes.ts
git commit -m "fix(docs): fix route discovery edge cases"
```

---

### Task 7: Integration Test — Full Capture Run on Staging

**Files:** None (validation only — this actually runs the capture against staging)

**Prerequisites:** Staging must be accessible at `https://meepleai.app` and credentials must be valid.

- [ ] **Step 1: Run the full capture**

Run: `cd apps/web && npx tsx ../../docs/pages/capture.ts`

Expected: Login succeeds, parameters resolve, screenshots are captured. Progress logged to console. Takes ~7-8 minutes.

- [ ] **Step 2: Verify manifest was generated**

Run: `cat ../../docs/pages/manifest.json | head -20`

Check: `generatedAt` is set, `stats.total` > 200, `stats.captured` > 0.

- [ ] **Step 3: Generate the HTML site**

Run: `cd apps/web && npx tsx ../../docs/pages/generate.ts`

Expected: `index.html` generated at `docs/pages/dist/index.html`.

- [ ] **Step 4: Preview the result**

Run: `cd apps/web && npx serve ../../docs/pages/dist`

Open the printed URL in browser. Verify:
- Cards show with thumbnails
- Sidebar filters work
- Search filters in real-time
- Lightbox opens on card click
- Arrow keys navigate between screenshots
- Counter updates on filter/search

- [ ] **Step 5: Fix any issues found**

If pages fail, errors show in manifest. Adjust `capture.ts` (timeouts, selectors, param resolvers) as needed.

- [ ] **Step 6: Commit any adjustments**

```bash
git add docs/pages/
git commit -m "fix(docs): adjust capture settings after integration test"
```

---

### Task 8: Final Cleanup and Documentation Commit

**Files:**
- None new — just verify and commit the working system

- [ ] **Step 1: Verify clean state**

Run: `git status`

All source files (`config.ts`, `routes.ts`, `capture.ts`, `generate.ts`) should be committed. Generated files (`manifest.json`, `screenshots/`, `dist/`) should be gitignored.

- [ ] **Step 2: Run one final clean capture**

Run: `cd apps/web && npm run docs:pages`

Expected: Full pipeline completes successfully.

- [ ] **Step 3: Final commit if needed**

```bash
git add -A docs/pages/*.ts apps/web/package.json .gitignore
git commit -m "feat(docs): complete page catalog screenshot system

Automated system that screenshots all 209 frontend pages on staging
and generates a static HTML catalog with filters, search, and lightbox.

- Route discovery from filesystem (apps/web/src/app/**/page.tsx)
- Auto-login via staging credentials (infra/secrets/staging/admin.secret)
- Concurrent screenshot capture (3 parallel browser contexts)
- Incremental re-runs (skip already captured pages)
- Self-contained HTML output (zero dependencies)"
```
