/**
 * Page Catalog — Screenshot Capture Engine
 *
 * Discovers all Next.js pages, logs into staging via Playwright,
 * resolves dynamic route parameters via API calls, and captures
 * screenshots of all pages with concurrency support.
 *
 * Usage:
 *   npx tsx docs/pages/capture.ts [--force]
 */

import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';

import { chromium, type Browser, type BrowserContext } from 'playwright';

import { config, PARAM_RESOLVERS, HARDCODED_PARAMS, type PageEntry, type Manifest } from './config';
import { discoverRoutes } from './routes';

// ============================================================================
// Credential Loading
// ============================================================================

function loadCredentials(): { email: string; password: string } {
  if (!fs.existsSync(config.secretsPath)) {
    console.error(`\u274C Secrets file not found: ${config.secretsPath}`);
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
    console.error('\u274C ADMIN_EMAIL or ADMIN_PASSWORD not found in secrets file');
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

  console.log(`\uD83D\uDD10 Logging in as ${credentials.email}...`);

  await page.goto(`${config.baseUrl}/login`, { waitUntil: 'domcontentloaded', timeout: 30000 });

  // Wait for login form
  await page.waitForSelector('[data-testid="login-email"]', { timeout: 10000 });

  // Fill credentials
  await page.fill('[data-testid="login-email"]', credentials.email);
  await page.fill('[data-testid="login-password"]', credentials.password);
  await page.click('[data-testid="login-submit"]');

  // Wait for redirect (login success)
  await page.waitForURL(url => !url.toString().includes('/login'), { timeout: 15000 });

  console.log('\u2705 Login successful');

  // Save session state
  const storagePath = path.join(os.tmpdir(), '.meepleai-auth-state.json');
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
  const pagesWithParams = pages.filter(
    p => p.params && Object.values(p.params).includes('unresolved')
  );
  if (pagesWithParams.length === 0) return;

  console.log(`\n\uD83D\uDD0D Resolving dynamic parameters for ${pagesWithParams.length} pages...`);

  const context = await browser.newContext({
    viewport: config.viewport,
    storageState: authStatePath,
  });
  const page = await context.newPage();

  // Cache resolved values to avoid duplicate API calls
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
      if (hardcodedKey && HARDCODED_PARAMS[hardcodedKey] !== '__skip__') {
        entry.params[paramName] = HARDCODED_PARAMS[hardcodedKey];
        continue;
      }

      // Find parent segment for resolver lookup
      const paramIdx = routeSegments.findIndex(s => s === `[${paramName}]`);
      const parentSegment = paramIdx > 0 ? routeSegments[paramIdx - 1] : null;
      const cleanParent = parentSegment?.replace(/[()]/g, '') || '';

      const resolver = PARAM_RESOLVERS[cleanParent];
      if (!resolver) {
        console.warn(`  \u26A0\uFE0F No resolver for [${paramName}] in ${entry.routePattern} (parent: ${cleanParent})`);
        entry.params[paramName] = '__unresolved__';
        continue;
      }

      // Use cache
      const cacheKey = resolver.endpoint;
      if (!resolverCache.has(cacheKey)) {
        try {
          const apiUrl = `${config.baseUrl}${resolver.endpoint}`;
          const apiResponse = await context.request.get(apiUrl);
          if (apiResponse.ok()) {
            const responseData = await apiResponse.json();
            resolverCache.set(cacheKey, resolver.extract(responseData));
          } else {
            console.warn(`  \u26A0\uFE0F API returned ${apiResponse.status()} for ${resolver.endpoint}`);
            resolverCache.set(cacheKey, null);
          }
        } catch (err) {
          console.warn(`  \u26A0\uFE0F API call failed for ${resolver.endpoint}: ${err}`);
          resolverCache.set(cacheKey, null);
        }
      }

      const resolvedId = resolverCache.get(cacheKey);
      if (resolvedId) {
        entry.params[paramName] = resolvedId;
        console.log(`  \u2705 [${paramName}] \u2192 ${resolvedId} (via ${cleanParent})`);
      } else {
        entry.params[paramName] = '__unresolved__';
        console.warn(`  \u26A0\uFE0F Could not resolve [${paramName}] for ${entry.routePattern}`);
      }
    }

    // Build resolved route
    let resolvedRoute = entry.routePattern;
    if (entry.params) {
      for (const [paramName, paramValue] of Object.entries(entry.params)) {
        resolvedRoute = resolvedRoute.replace(`[${paramName}]`, paramValue);
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
  const toCapture = pages.filter(p => {
    if (p.status === 'skipped') return false;
    if (forceRecapture) return true;
    if (p.status === 'ok' && fs.existsSync(path.join(__dirname, p.screenshot))) return false;
    return true;
  });

  if (toCapture.length === 0) {
    console.log('\n\u2705 All pages already captured. Use --force to recapture.');
    return;
  }

  const publicPages = toCapture.filter(p => !p.requiresAuth);
  const authPages = toCapture.filter(p => p.requiresAuth && !p.requiresAdmin);
  const adminPages = toCapture.filter(p => p.requiresAdmin);

  console.log(`\n\uD83D\uDCF8 Capturing ${toCapture.length} pages:`);
  console.log(`   Phase 1 \u2014 Public: ${publicPages.length}`);
  console.log(`   Phase 2 \u2014 Authenticated: ${authPages.length}`);
  console.log(`   Phase 3 \u2014 Admin: ${adminPages.length}`);

  if (publicPages.length > 0) {
    console.log('\n--- Phase 1: Public Pages ---');
    await capturePhase(browser, publicPages, null);
  }

  if (authPages.length > 0) {
    console.log('\n--- Phase 2: Authenticated Pages ---');
    await capturePhase(browser, authPages, authStatePath);
  }

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
  for (let i = 0; i < pages.length; i += config.concurrency) {
    const batch = pages.slice(i, i + config.concurrency);
    await Promise.all(batch.map(entry => captureSinglePage(browser, entry, authStatePath)));
  }
}

async function captureSinglePage(
  browser: Browser,
  entry: PageEntry,
  authStatePath: string | null
): Promise<void> {
  const contextOptions: { viewport: typeof config.viewport; storageState?: string } = {
    viewport: config.viewport,
  };
  if (authStatePath) {
    contextOptions.storageState = authStatePath;
  }

  const context = await browser.newContext(contextOptions);
  const page = await context.newPage();

  try {
    const url = `${config.baseUrl}${entry.route}`;

    await page.goto(url, {
      waitUntil: entry.requiresAuth ? 'load' : 'networkidle',
      timeout: config.pageTimeout,
    });

    // Extra delay for animations/transitions
    await page.waitForTimeout(config.delayAfterLoad);

    // Ensure screenshot directory exists
    const screenshotPath = path.join(__dirname, entry.screenshot);
    fs.mkdirSync(path.dirname(screenshotPath), { recursive: true });

    await page.screenshot({
      path: screenshotPath,
      fullPage: false,
      animations: 'disabled',
    });

    entry.status = 'ok';
    entry.capturedAt = new Date().toISOString();
    entry.error = null;

    console.log(`  \u2705 ${entry.id} \u2192 ${entry.route}`);
  } catch (err: any) {
    const errMsg = err?.message || 'Unknown error';

    if (errMsg.includes('Timeout') || errMsg.includes('timeout')) {
      entry.status = 'timeout';
      entry.error = 'Page load timeout';
      console.log(`  \u23F1\uFE0F ${entry.id} \u2192 TIMEOUT ${entry.route}`);
    } else {
      entry.status = 'error';
      entry.error = errMsg.slice(0, 200);
      console.log(`  \u274C ${entry.id} \u2192 ERROR ${entry.route}: ${entry.error}`);
    }

    // Try to capture error state screenshot anyway
    try {
      const screenshotPath = path.join(__dirname, entry.screenshot);
      fs.mkdirSync(path.dirname(screenshotPath), { recursive: true });
      await page.screenshot({ path: screenshotPath, fullPage: false });
    } catch {
      // Ignore screenshot failure on error pages
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

  console.log('\uD83D\uDCCB MeepleAI Page Catalog \u2014 Screenshot Capture\n');
  console.log(`   Base URL: ${config.baseUrl}`);
  console.log(`   Force: ${forceRecapture}`);

  // Step 1: Discover routes
  console.log('\n\uD83D\uDCC2 Discovering routes...');
  const pages = discoverRoutes();
  console.log(`   Found ${pages.length} pages`);

  // Step 2: Merge with existing manifest (preserve already captured)
  if (!forceRecapture) {
    const existing = loadExistingManifest();
    if (existing) {
      const existingMap = new Map(existing.pages.map(p => [p.routePattern, p]));
      for (const page of pages) {
        const prev = existingMap.get(page.routePattern);
        if (prev && prev.status === 'ok' && fs.existsSync(path.join(__dirname, prev.screenshot))) {
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
    console.log('\n\uD83D\uDCCA Capture Summary:');
    console.log(`   Total:    ${manifest.stats.total}`);
    console.log(`   Captured: ${manifest.stats.captured}`);
    console.log(`   Errors:   ${manifest.stats.errors}`);
    console.log(`   Timeouts: ${manifest.stats.timeouts}`);
    console.log(`   Skipped:  ${manifest.stats.skipped}`);

    // Clean up auth state
    const authStateFile = path.join(os.tmpdir(), '.meepleai-auth-state.json');
    if (fs.existsSync(authStateFile)) fs.unlinkSync(authStateFile);

  } finally {
    await browser.close();
  }
}

main().catch(err => {
  console.error('\uD83D\uDCA5 Fatal error:', err);
  process.exit(1);
});
