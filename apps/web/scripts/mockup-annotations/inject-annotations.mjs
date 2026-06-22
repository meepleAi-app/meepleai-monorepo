#!/usr/bin/env node
/**
 * inject-annotations.mjs — inject @mockup JSDoc annotations into route page.tsx files (DS-17-1)
 *
 * Parses admin-mockups/MOCKUPS_INDEX.md, finds page-mock rows with mapped Next.js
 * routes, locates the corresponding apps/web/src/app/.../page.tsx, and prepends
 * a JSDoc block with `@mockup <path>` plus a `MOCKUP-ANNOTATION` marker that
 * acts as an idempotency guard.
 *
 * Designed to be re-run safely: files already containing the marker are
 * skipped. This script does NOT delete existing annotations — manually remove
 * the JSDoc block (preserving the rest of the file) if a route is unmapped.
 *
 * Usage:
 *   node inject-annotations.mjs                # dry-run: show what would change, exit 0
 *   node inject-annotations.mjs --apply        # actually write the files
 *   node inject-annotations.mjs --limit 30     # process at most N mockups (Top-N pilot)
 *   node inject-annotations.mjs --schema       # print parsed mockup → route → file table
 *
 * Exit codes:
 *   0  dry-run succeeded OR --apply wrote files with no errors
 *   1  could not parse MOCKUPS_INDEX.md OR no route files found
 *   2  invocation error (bad flag)
 *
 * Refs:
 *   Spec:     docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md
 *   Issue:    #2069 (DS-17-1)
 *   Umbrella: #2063 (DS-17 Mockup-to-App Fidelity)
 *   Plan:     docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md §4.3
 */

import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { globSync } from 'glob';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..', '..');
const WEB_ROOT = resolve(REPO_ROOT, 'apps', 'web');
const MOCKUPS_INDEX = resolve(REPO_ROOT, 'admin-mockups', 'MOCKUPS_INDEX.md');
const APP_ROUTES_GLOB = 'src/app/**/page.tsx';

/**
 * Marker string used to detect prior injection. Any file containing this token
 * is considered already annotated and is skipped by the injector. Must remain
 * stable across script versions — changing it invalidates idempotency.
 */
export const ANNOTATION_MARKER = 'MOCKUP-ANNOTATION';

// ---------------------------------------------------------------------------
// MOCKUPS_INDEX.md parser
// ---------------------------------------------------------------------------

const TABLE_ROW = /^\|\s*`([^`]+)`\s*\|\s*([\w-]+)\s*\|\s*(.+?)\s*\|\s*$/;

/**
 * Strip trailing parenthetical annotations from a route token.
 *   "/games/[id]/faqs (reuse)" → "/games/[id]/faqs"
 *   "/upload (partial)" → "/upload"
 */
function cleanRoute(raw) {
  return raw
    .replace(/\([^)]*\)/g, '')
    .replace(/`/g, '')
    .trim();
}

/**
 * Parse MOCKUPS_INDEX.md markdown body. Returns one entry per page-mock row
 * with at least one extractable Next.js route.
 *
 * Entry shape: `{ mockup: 'sp4-dashboard.html', type: 'page-mock', routes: ['/dashboard'] }`
 */
export function parseMockupsIndex(md) {
  const entries = [];
  for (const line of md.split(/\r?\n/)) {
    const m = TABLE_ROW.exec(line);
    if (!m) continue;
    const [, mockup, type, routesCell] = m;
    if (type !== 'page-mock') continue;

    const routes = routesCell
      .split(',')
      .map((s) => cleanRoute(s))
      .filter((s) => s.startsWith('/'));
    if (routes.length === 0) continue;

    entries.push({ mockup, type, routes });
  }
  return entries;
}

// ---------------------------------------------------------------------------
// Route → file path matching
// ---------------------------------------------------------------------------

/**
 * Compute the expected file-path tail for a given Next.js route.
 *   "/dashboard"      → "/dashboard/page.tsx"
 *   "/games/[id]"     → "/games/[id]/page.tsx"
 *   "/"               → "/page.tsx" (root)
 */
function routeTail(route) {
  if (route === '/') return '/page.tsx';
  return `${route.replace(/\/+$/, '')}/page.tsx`;
}

/**
 * Count segments in a route (used to prefer exact matches over deeper ones).
 *   "/library"          → 1
 *   "/library/[gameId]" → 2
 */
function segmentCount(route) {
  return route.split('/').filter(Boolean).length;
}

/**
 * Find the file in `routeFiles` whose path ends with the tail derived from
 * `route`. Among multiple matches, prefer the one whose segment count after
 * `src/app/` matches the route segment count (handles route groups like
 * `(authenticated)`, which add no segment to the URL but do appear in the
 * file path).
 *
 * Returns the matching file path string, or null when nothing matches.
 */
export function routeToFilePath(route, routeFiles) {
  const tail = routeTail(route);
  const routeSegments = segmentCount(route);

  const candidates = routeFiles.filter((f) => f.endsWith(tail));
  if (candidates.length === 0) return null;
  if (candidates.length === 1) return candidates[0];

  // Multiple candidates — prefer the one with matching real-route segment count.
  // A file like `src/app/(authenticated)/library/page.tsx` has 2 segments after
  // `src/app/` (1 group + 1 route), and the route `/library` has 1 segment.
  // Route groups in parentheses don't count toward URL depth.
  const scored = candidates.map((f) => {
    const afterApp = f.split('src/app/')[1] || '';
    const parts = afterApp.split('/').filter(Boolean);
    const urlParts = parts.filter((p) => !p.startsWith('('));
    const urlDepth = urlParts.length - 1; // minus the `page.tsx` leaf
    return { file: f, depth: urlDepth };
  });
  const exact = scored.find((s) => s.depth === routeSegments);
  return (exact ?? scored[0]).file;
}

// ---------------------------------------------------------------------------
// Annotation block + injector
// ---------------------------------------------------------------------------

/**
 * Build the JSDoc block to prepend to a route file. Must contain
 * ANNOTATION_MARKER for the idempotency guard.
 */
export function buildAnnotationBlock(mockupPath) {
  return [
    '/**',
    ` * @mockup ${mockupPath}`,
    ' *',
    ` * ${ANNOTATION_MARKER} marker — do not edit by hand.`,
    ' * Regenerated by: pnpm mockup-annotations:inject',
    ' * Audit:          pnpm mockup-annotations:audit',
    ' *',
    ' * Ref: DS-17-1 #2069, umbrella #2063.',
    ' */',
    '',
  ].join('\n');
}

/**
 * True if the file already contains the annotation marker. Used by the
 * injector to skip already-processed files.
 */
export function shouldSkipFile(content) {
  return typeof content === 'string' && content.includes(ANNOTATION_MARKER);
}

/**
 * Prepend the annotation block to `content`. No-op (returns input unchanged)
 * when `shouldSkipFile(content)` is true — this is what guarantees idempotency.
 */
export function injectAnnotation(content, mockupPath) {
  if (shouldSkipFile(content)) return content;
  return buildAnnotationBlock(mockupPath) + content;
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const args = { apply: false, limit: null, schema: false, help: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--apply') args.apply = true;
    else if (a === '--schema') args.schema = true;
    else if (a === '--help' || a === '-h') args.help = true;
    else if (a === '--limit') {
      const n = Number.parseInt(argv[++i], 10);
      if (Number.isNaN(n) || n <= 0) {
        throw new Error(`--limit requires a positive integer, got: ${argv[i]}`);
      }
      args.limit = n;
    } else if (a.startsWith('--limit=')) {
      const n = Number.parseInt(a.slice('--limit='.length), 10);
      if (Number.isNaN(n) || n <= 0) {
        throw new Error(`--limit requires a positive integer, got: ${a}`);
      }
      args.limit = n;
    } else {
      throw new Error(`Unknown argument: ${a}`);
    }
  }
  return args;
}

function printHelp() {
  process.stdout.write(
    [
      'inject-annotations.mjs — inject @mockup JSDoc into route page.tsx (DS-17-1)',
      '',
      'Usage:',
      '  node inject-annotations.mjs                # dry-run',
      '  node inject-annotations.mjs --apply        # write files',
      '  node inject-annotations.mjs --limit 30     # process at most N mockups',
      '  node inject-annotations.mjs --schema       # print mockup→route→file table',
      '',
      'Idempotent: files containing MOCKUP-ANNOTATION are skipped.',
      'Exit codes: 0 ok | 1 parse/glob failure | 2 invocation error',
      '',
    ].join('\n')
  );
}

function collectPlan(args) {
  if (!existsSync(MOCKUPS_INDEX)) {
    throw new Error(`MOCKUPS_INDEX.md not found at ${MOCKUPS_INDEX}`);
  }
  const md = readFileSync(MOCKUPS_INDEX, 'utf-8');
  const entries = parseMockupsIndex(md);

  const routeFiles = globSync(APP_ROUTES_GLOB, {
    cwd: WEB_ROOT,
    nodir: true,
  }).map((p) => p.replace(/\\/g, '/'));
  if (routeFiles.length === 0) {
    throw new Error(`No route files found under ${WEB_ROOT}/${APP_ROUTES_GLOB}`);
  }

  const plan = [];
  for (const e of entries) {
    for (const route of e.routes) {
      const rel = routeToFilePath(route, routeFiles);
      if (!rel) continue;
      plan.push({
        mockup: e.mockup,
        route,
        relFile: rel,
        absFile: resolve(WEB_ROOT, rel),
      });
    }
  }

  // De-duplicate (mockup, file) pairs (multi-route mockups don't double-inject).
  const seen = new Set();
  const deduped = [];
  for (const p of plan) {
    const key = `${p.mockup}::${p.relFile}`;
    if (seen.has(key)) continue;
    seen.add(key);
    deduped.push(p);
  }

  if (args.limit !== null) return deduped.slice(0, args.limit);
  return deduped;
}

async function main() {
  let args;
  try {
    args = parseArgs(process.argv.slice(2));
  } catch (err) {
    process.stderr.write(`[mockup-annotations:inject] ERROR: ${err.message}\n`);
    printHelp();
    process.exit(2);
  }

  if (args.help) {
    printHelp();
    process.exit(0);
  }

  let plan;
  try {
    plan = collectPlan(args);
  } catch (err) {
    process.stderr.write(`[mockup-annotations:inject] FAIL: ${err.message}\n`);
    process.exit(1);
  }

  if (args.schema) {
    process.stdout.write(JSON.stringify(plan, null, 2) + '\n');
    process.exit(0);
  }

  let injected = 0;
  let skipped = 0;
  for (const p of plan) {
    const content = readFileSync(p.absFile, 'utf-8');
    if (shouldSkipFile(content)) {
      skipped += 1;
      process.stdout.write(`  SKIP  ${p.relFile} (already annotated)\n`);
      continue;
    }
    const updated = injectAnnotation(content, `admin-mockups/design_files/${p.mockup}`);
    if (args.apply) {
      writeFileSync(p.absFile, updated, 'utf-8');
      injected += 1;
      process.stdout.write(`  WRITE ${p.relFile} → ${p.mockup}\n`);
    } else {
      injected += 1;
      process.stdout.write(`  DRY   ${p.relFile} → ${p.mockup}\n`);
    }
  }

  const mode = args.apply ? 'APPLY' : 'DRY-RUN';
  process.stdout.write(
    `\n[mockup-annotations:inject] ${mode}: ${injected} new, ${skipped} skipped, ${plan.length} total candidates\n`
  );
  if (!args.apply && injected > 0) {
    process.stdout.write('  Re-run with --apply to write the files.\n');
  }
  process.exit(0);
}

// CLI guard — cross-platform Windows safe via pathToFileURL.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((err) => {
    process.stderr.write(`[mockup-annotations:inject] UNEXPECTED: ${err.stack || err.message}\n`);
    process.exit(2);
  });
}
