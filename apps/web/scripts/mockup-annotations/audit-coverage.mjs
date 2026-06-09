#!/usr/bin/env node
/**
 * audit-coverage.mjs — report and gate @mockup annotation coverage (DS-17-1)
 *
 * Scans all Next.js page.tsx files and reports how many carry the
 * MOCKUP-ANNOTATION marker injected by inject-annotations.mjs. Doubles as a
 * CI gate: `--threshold N` fails the build when coverage drops below N%.
 *
 * Usage:
 *   node audit-coverage.mjs                       # inventory dump, exit 0
 *   node audit-coverage.mjs --threshold 80        # CI gate, exit 1 below 80%
 *   node audit-coverage.mjs --json                # JSON-only output to stdout
 *   node audit-coverage.mjs --scope "<glob>"      # override default route glob
 *
 * Exit codes:
 *   0  coverage report written (threshold met or no threshold set)
 *   1  coverage below threshold
 *   2  invocation error (bad flag, IO failure)
 *
 * Refs:
 *   Spec:     docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md
 *   Issue:    #2069 (DS-17-1)
 *   Umbrella: #2063
 *   Plan:     docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md §4.3
 */

import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { globSync } from 'glob';

import { parseMockupsIndex, routeToFilePath } from './inject-annotations.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..', '..');
const WEB_ROOT = resolve(REPO_ROOT, 'apps', 'web');
const MOCKUPS_INDEX = resolve(REPO_ROOT, 'admin-mockups', 'MOCKUPS_INDEX.md');
const AUDIT_DIR = resolve(REPO_ROOT, 'audits');
const JSON_OUT = resolve(AUDIT_DIR, '2026-06-09-mockup-annotation-coverage.json');
const MD_OUT = resolve(AUDIT_DIR, '2026-06-09-mockup-annotation-coverage.md');

/**
 * Marker string used by inject-annotations.mjs. Kept identical here so both
 * scripts agree on what "annotated" means. Changing it breaks coverage
 * detection on every legacy file.
 */
export const ANNOTATION_MARKER = 'MOCKUP-ANNOTATION';

const DEFAULT_GLOB = 'src/app/**/page.tsx';
const DEFAULT_IGNORE = ['**/node_modules/**', '**/.next/**', '**/dist/**', '**/__tests__/**'];

// ---------------------------------------------------------------------------
// Core: coverage collection
// ---------------------------------------------------------------------------

/**
 * Compute the "mappable" set of route files: those that appear in
 * `MOCKUPS_INDEX.md` as `page-mock` rows with at least one route that resolves
 * to a real `page.tsx` under `apps/web/src/app/`. This is the denominator
 * used when `--denominator mappable` is passed — it lets coverage measure
 * "annotated / annotatable" rather than "annotated / all routes".
 *
 * Returns a `Set<string>` of relative paths (forward-slash normalized) that
 * `collectCoverage` can use as a filter.
 */
export function mappableRouteSet(routeFiles, indexPath = MOCKUPS_INDEX) {
  if (!existsSync(indexPath)) return new Set();
  const md = readFileSync(indexPath, 'utf-8');
  const entries = parseMockupsIndex(md);
  const out = new Set();
  for (const e of entries) {
    for (const route of e.routes) {
      const rel = routeToFilePath(route, routeFiles);
      if (rel) out.add(rel);
    }
  }
  return out;
}

/**
 * Glob `globPattern` under `cwd`, classify each file as annotated/unannotated
 * based on whether it contains ANNOTATION_MARKER, and return the totals plus
 * a sorted list of uncovered paths.
 *
 * Options:
 *   - `ignore`: glob ignore patterns (default: node_modules + .next + dist + __tests__)
 *   - `denominator`: 'all' (default — count every globbed file) or 'mappable'
 *     (count only routes that have a `MOCKUPS_INDEX.md` mapping). The
 *     `mappable` mode is used by CI to compute a realistic ≥80% target
 *     against the annotatable-route subset rather than the full app tree.
 *
 * Result shape:
 *   {
 *     total: number,
 *     covered: number,
 *     uncovered: string[],   // relative to cwd, stable-sorted
 *     coverage: number,      // percentage 0..100 (0 when total is 0)
 *     denominator: 'all' | 'mappable',
 *     mappableTotal?: number, // total routes considered when denominator='mappable'
 *   }
 */
export function collectCoverage(globPattern, cwd, opts = {}) {
  const ignore = opts.ignore ?? DEFAULT_IGNORE;
  const denominator = opts.denominator ?? 'all';
  const files = globSync(globPattern, { cwd, ignore, absolute: true, nodir: true });

  let mappable = null;
  if (denominator === 'mappable') {
    const relFiles = files.map((abs) => relative(cwd, abs).replace(/\\/g, '/'));
    mappable = mappableRouteSet(relFiles);
  }

  let covered = 0;
  const uncovered = [];
  for (const abs of files) {
    const rel = relative(cwd, abs).replace(/\\/g, '/');
    if (denominator === 'mappable' && !mappable.has(rel)) continue;
    const text = readFileSync(abs, 'utf-8');
    if (text.includes(ANNOTATION_MARKER)) covered += 1;
    else uncovered.push(rel);
  }
  uncovered.sort();

  const total = denominator === 'mappable' ? mappable.size : files.length;
  const coverage = total === 0 ? 0 : (covered / total) * 100;
  return {
    total,
    covered,
    uncovered,
    coverage,
    denominator,
    ...(denominator === 'mappable' ? { mappableTotal: total } : {}),
  };
}

// ---------------------------------------------------------------------------
// Threshold evaluation
// ---------------------------------------------------------------------------

/**
 * Compare coverage against a threshold (percentage 0..100).
 *
 * Semantics:
 *   - total === 0 ⇒ always pass (no routes ⇒ nothing to cover)
 *   - coverage >= threshold ⇒ pass
 *   - otherwise ⇒ fail with `diff` = threshold − coverage (positive)
 */
export function evaluateThreshold(stats, threshold) {
  if (stats.total === 0) return { pass: true, diff: 0 };
  if (stats.coverage >= threshold) return { pass: true, diff: 0 };
  return { pass: false, diff: threshold - stats.coverage };
}

// ---------------------------------------------------------------------------
// Markdown formatter
// ---------------------------------------------------------------------------

function formatPct(n) {
  return Math.round(n * 100) / 100;
}

/**
 * Build a human-readable Markdown coverage report.
 *
 * Header reflects threshold result: "Below threshold" when failing,
 * "Threshold met" when passing, "Inventory only" when no threshold provided.
 */
export function formatCoverageMarkdown(stats, opts = {}) {
  const threshold = opts.threshold ?? null;
  const verdict =
    threshold === null
      ? 'inventory only (no threshold)'
      : stats.coverage >= threshold
        ? `threshold met (≥ ${threshold}%)`
        : `below threshold (< ${threshold}%) — pass marker absent`;

  const lines = [
    '# Mockup Annotation Coverage — DS-17-1',
    '',
    '| Field | Value |',
    '|---|---|',
    `| **Date** | ${new Date().toISOString().slice(0, 10)} |`,
    `| **Generator** | \`pnpm mockup-annotations:audit\` (DS-17-1) |`,
    `| **Spec** | [\`2026-06-09-mockup-to-app-drift-spec-panel-review.md\`](../docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md) |`,
    `| **Marker** | \`${ANNOTATION_MARKER}\` |`,
    `| **Denominator** | \`${stats.denominator ?? 'all'}\` |`,
    `| **Coverage** | ${formatPct(stats.coverage)}% — ${stats.covered} / ${stats.total} |`,
    `| **Status** | ${verdict} |`,
    '',
  ];

  if (stats.uncovered.length === 0) {
    lines.push('## Uncovered routes', '', '_(none — full coverage)_', '');
  } else {
    lines.push(
      '## Uncovered routes',
      '',
      'Routes missing the `@mockup` JSDoc block. Run `pnpm mockup-annotations:inject --apply` after extending MOCKUPS_INDEX.md.',
      '',
      '| File |',
      '|---|',
      ...stats.uncovered.map((f) => `| \`${f}\` |`),
      ''
    );
  }

  lines.push(
    '## Refs',
    '',
    '- Sub-issue: [#2069](https://github.com/meepleAi-app/meepleai-monorepo/issues/2069)',
    '- Umbrella:  [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)',
    '- Plan:      [`2026-06-09-ds-17-phase-1-implementation-plan.md`](../docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md) §4.3',
    '- Companion JSON: [`2026-06-09-mockup-annotation-coverage.json`](./2026-06-09-mockup-annotation-coverage.json)',
    ''
  );

  return lines.join('\n');
}

// ---------------------------------------------------------------------------
// Report writer
// ---------------------------------------------------------------------------

function writeReports(stats, opts) {
  if (!existsSync(dirname(JSON_OUT))) mkdirSync(dirname(JSON_OUT), { recursive: true });
  const json = {
    generatedAt: new Date().toISOString(),
    marker: ANNOTATION_MARKER,
    threshold: opts.threshold ?? null,
    denominator: stats.denominator ?? 'all',
    total: stats.total,
    covered: stats.covered,
    uncovered: stats.uncovered,
    coveragePct: formatPct(stats.coverage),
  };
  writeFileSync(JSON_OUT, JSON.stringify(json, null, 2) + '\n', 'utf-8');
  writeFileSync(MD_OUT, formatCoverageMarkdown(stats, opts), 'utf-8');
  return { jsonOut: JSON_OUT, mdOut: MD_OUT };
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const args = {
    threshold: null,
    scope: DEFAULT_GLOB,
    denominator: 'all',
    json: false,
    help: false,
  };
  const validDenominators = new Set(['all', 'mappable']);
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--json') args.json = true;
    else if (a === '--help' || a === '-h') args.help = true;
    else if (a === '--threshold') {
      const n = Number.parseFloat(argv[++i]);
      if (Number.isNaN(n) || n < 0 || n > 100) {
        throw new Error(`--threshold requires a number 0..100, got: ${argv[i]}`);
      }
      args.threshold = n;
    } else if (a.startsWith('--threshold=')) {
      const n = Number.parseFloat(a.slice('--threshold='.length));
      if (Number.isNaN(n) || n < 0 || n > 100) {
        throw new Error(`--threshold requires a number 0..100, got: ${a}`);
      }
      args.threshold = n;
    } else if (a === '--scope') {
      args.scope = argv[++i];
    } else if (a.startsWith('--scope=')) {
      args.scope = a.slice('--scope='.length);
    } else if (a === '--denominator') {
      const v = argv[++i];
      if (!validDenominators.has(v)) {
        throw new Error(`--denominator must be 'all' or 'mappable', got: ${v}`);
      }
      args.denominator = v;
    } else if (a.startsWith('--denominator=')) {
      const v = a.slice('--denominator='.length);
      if (!validDenominators.has(v)) {
        throw new Error(`--denominator must be 'all' or 'mappable', got: ${v}`);
      }
      args.denominator = v;
    } else {
      throw new Error(`Unknown argument: ${a}`);
    }
  }
  return args;
}

function printHelp() {
  process.stdout.write(
    [
      'audit-coverage.mjs — @mockup annotation coverage reporter (DS-17-1)',
      '',
      'Usage:',
      '  node audit-coverage.mjs                  # inventory dump, exit 0',
      '  node audit-coverage.mjs --threshold 80   # CI gate, exit 1 below 80%',
      '  node audit-coverage.mjs --json           # JSON to stdout (no audit files)',
      '  node audit-coverage.mjs --scope "<glob>" # override default route glob',
      '',
      `Default scope: ${DEFAULT_GLOB}`,
      `Marker: ${ANNOTATION_MARKER}`,
      '',
      'Exit codes: 0 ok | 1 below threshold | 2 invocation error',
      '',
    ].join('\n')
  );
}

async function main() {
  let args;
  try {
    args = parseArgs(process.argv.slice(2));
  } catch (err) {
    process.stderr.write(`[mockup-annotations:audit] ERROR: ${err.message}\n`);
    printHelp();
    process.exit(2);
  }

  if (args.help) {
    printHelp();
    process.exit(0);
  }

  const stats = collectCoverage(args.scope, WEB_ROOT, { denominator: args.denominator });
  const result = evaluateThreshold(stats, args.threshold ?? 0);

  if (args.json) {
    process.stdout.write(
      JSON.stringify(
        {
          marker: ANNOTATION_MARKER,
          threshold: args.threshold,
          ...stats,
          coverage: formatPct(stats.coverage),
          pass: result.pass,
        },
        null,
        2
      ) + '\n'
    );
  } else {
    const out = writeReports(stats, { threshold: args.threshold });
    process.stdout.write(
      `[mockup-annotations:audit] coverage ${formatPct(stats.coverage)}% (${stats.covered}/${stats.total}) [denominator=${stats.denominator}]\n` +
        `  JSON: ${relative(REPO_ROOT, out.jsonOut)}\n` +
        `  MD:   ${relative(REPO_ROOT, out.mdOut)}\n`
    );
  }

  if (args.threshold !== null && !result.pass) {
    process.stderr.write(
      `[mockup-annotations:audit] FAIL: coverage ${formatPct(stats.coverage)}% below threshold ${args.threshold}% (delta ${formatPct(result.diff)}%)\n` +
        '       Add @mockup annotations to the uncovered routes or lower --threshold.\n'
    );
    process.exit(1);
  }

  process.exit(0);
}

// CLI guard — cross-platform Windows safe via pathToFileURL.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((err) => {
    process.stderr.write(`[mockup-annotations:audit] UNEXPECTED: ${err.stack || err.message}\n`);
    process.exit(2);
  });
}
