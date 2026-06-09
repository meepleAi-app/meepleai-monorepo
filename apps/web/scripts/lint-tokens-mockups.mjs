#!/usr/bin/env node
/**
 * lint-tokens-mockups.mjs — legacy CSS token literal reporter for admin-mockups (DS-17-2)
 *
 * Scans mockup HTML/CSS/JSX files for forbidden legacy token references and
 * fails the build when the count exceeds a baseline threshold (whitelist
 * incremental — existing violations tolerated, new ones blocked).
 *
 * Forbidden token families:
 *   - var(--bg-base)
 *   - var(--gaming-*)
 *   - var(--nh-*)
 *   - var(--e-*)
 *
 * Whitelist-incremental rationale (plan §7 R3):
 *   The token bridge (DS-16 pending) still maps these names at runtime, so the
 *   existing mockups have not been migrated yet. We snapshot the current count
 *   as a hard ceiling so NEW violations are rejected; the ceiling drops once
 *   DS-16 migrates the literals upstream.
 *
 * Usage:
 *   node lint-tokens-mockups.mjs                       # inventory dump, exit 0
 *   node lint-tokens-mockups.mjs --strict --max-baseline 6   # CI gate
 *   node lint-tokens-mockups.mjs --scope "<glob>"      # override default scope
 *
 * Exit codes:
 *   0  inventory written OR strict run within baseline
 *   1  strict run exceeded baseline (NEW violation introduced)
 *   2  invocation error (bad flag, IO failure)
 *
 * Refs:
 *   Spec:     docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md
 *   Issue:    #2070 (DS-17-2)
 *   Umbrella: #2063 (DS-17 Mockup-to-App Fidelity)
 *   Plan:     docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md §4.1
 */

import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { globSync } from 'glob';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..');
const AUDIT_DIR = resolve(REPO_ROOT, 'audits');
const JSON_OUT = resolve(AUDIT_DIR, '2026-06-09-mockup-token-violations.json');
const MD_OUT = resolve(AUDIT_DIR, '2026-06-09-mockup-token-violations.md');

// ---------------------------------------------------------------------------
// Regex & defaults — exported for unit testing
// ---------------------------------------------------------------------------

/**
 * Matches forbidden legacy CSS variable literals inside `var(...)` calls.
 *
 * Forbidden families (1+ lowercase chars / digits / hyphens after the prefix):
 *   - `bg-base`     — single literal (no wildcard tail)
 *   - `gaming-*`    — e.g. `gaming-blue`, `gaming-accent-gradient`
 *   - `nh-*`        — e.g. `nh-bg-2`, `nh-text-primary`
 *   - `e-*`         — e.g. `e-agent-h`, `e-game-l`, `e-1` (per CLAUDE.md token bridge map)
 *
 * Canonical semantic tokens (--background, --foreground, --muted-foreground,
 * --card, --border, --primary, --secondary, --accent, --destructive, --shadow-*,
 * --transition, …) do NOT start with any of the forbidden prefixes, so they
 * are intentionally not matched.
 */
export const LEGACY_TOKEN_REGEX =
  /var\(--(bg-base|gaming-[a-z0-9][a-z0-9-]*|nh-[a-z0-9][a-z0-9-]*|e-[a-z0-9][a-z0-9-]*)\)/g;

export const DEFAULT_GLOB = 'admin-mockups/**/*.{html,jsx,css}';

export const DEFAULT_IGNORE = [
  '**/node_modules/**',
  '**/.next/**',
  '**/dist/**',
  '**/.git/**',
  '**/coverage/**',
];

// ---------------------------------------------------------------------------
// Core lint primitives
// ---------------------------------------------------------------------------

/**
 * Scan a single text blob for forbidden token literals.
 * Returns an array of `{ file, line, column, token, match }` records sorted by line/column.
 */
export function findViolationsInText(text, filePath) {
  if (!text || typeof text !== 'string') return [];
  const out = [];
  const lines = text.split('\n');
  for (let i = 0; i < lines.length; i++) {
    // Fresh regex per line — global flag is stateful across calls.
    const re = new RegExp(LEGACY_TOKEN_REGEX.source, LEGACY_TOKEN_REGEX.flags);
    for (const m of lines[i].matchAll(re)) {
      out.push({
        file: filePath,
        line: i + 1,
        column: (m.index ?? 0) + 1,
        token: m[1],
        match: m[0],
      });
    }
  }
  return out;
}

/**
 * Lint a glob of files relative to `repoRoot`.
 * Returns `{ fileCount, violations }`; `violations` is stable-sorted by file then line then column.
 */
export function lintFiles(globPattern, repoRoot, opts = {}) {
  const ignore = opts.ignore ?? DEFAULT_IGNORE;
  const absoluteFiles = globSync(globPattern, {
    cwd: repoRoot,
    ignore,
    absolute: true,
    nodir: true,
  });

  const allViolations = [];
  for (const abs of absoluteFiles) {
    const rel = relative(repoRoot, abs).replace(/\\/g, '/');
    const text = readFileSync(abs, 'utf-8');
    allViolations.push(...findViolationsInText(text, rel));
  }

  allViolations.sort((a, b) => {
    if (a.file !== b.file) return a.file < b.file ? -1 : 1;
    if (a.line !== b.line) return a.line - b.line;
    return a.column - b.column;
  });

  return { fileCount: absoluteFiles.length, violations: allViolations };
}

// ---------------------------------------------------------------------------
// Reporting
// ---------------------------------------------------------------------------

function groupBy(items, keyFn) {
  const out = {};
  for (const it of items) {
    const k = keyFn(it);
    out[k] = (out[k] ?? 0) + 1;
  }
  return out;
}

function buildJsonReport({ fileCount, violations }, baseline) {
  const byFile = groupBy(violations, (v) => v.file);
  const byFamily = groupBy(violations, (v) => v.token.split('-')[0]);
  return {
    generatedAt: new Date().toISOString(),
    scope: DEFAULT_GLOB,
    legacyFamilies: ['bg-base', 'gaming-*', 'nh-*', 'e-*'],
    totalViolations: violations.length,
    fileCount,
    filesAffected: Object.keys(byFile).length,
    baselineMaxViolations: baseline,
    byFile,
    byFamily,
    violations,
  };
}

function buildMdReport(report) {
  const lines = [
    '# Mockup Token Violations — Inventory (DS-17-2)',
    '',
    '| Field | Value |',
    '|---|---|',
    `| **Date** | ${report.generatedAt.slice(0, 10)} |`,
    `| **Generator** | \`pnpm lint:tokens:mockups\` (DS-17-2) |`,
    `| **Spec** | [\`2026-06-09-mockup-to-app-drift-spec-panel-review.md\`](../docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md) |`,
    `| **Scope** | \`${report.scope}\` |`,
    `| **Total violations** | ${report.totalViolations} |`,
    `| **Files scanned** | ${report.fileCount} |`,
    `| **Files affected** | ${report.filesAffected} |`,
    `| **Baseline ceiling** | ${report.baselineMaxViolations === null ? '_(none — inventory mode)_' : report.baselineMaxViolations} |`,
    '',
    '## Forbidden legacy families',
    '',
    'These names will be REMOVED in DS-16 (token bridge unwind). New mockup work must use the canonical semantic tokens (`--background`, `--foreground`, `--muted-foreground`, `--card`, `--border`, `--primary`, …).',
    '',
    '| Family | Pattern | Count |',
    '|---|---|---|',
  ];
  const familyOrder = [
    ['bg', 'bg-base', 'var(--bg-base)'],
    ['gaming', 'gaming-*', 'var(--gaming-*)'],
    ['nh', 'nh-*', 'var(--nh-*)'],
    ['e', 'e-*', 'var(--e-*)'],
  ];
  for (const [familyKey, displayName, pattern] of familyOrder) {
    lines.push(`| \`${displayName}\` | \`${pattern}\` | ${report.byFamily[familyKey] ?? 0} |`);
  }
  lines.push('', '## Violations by file', '', '| File | Count |', '|---|---|');
  const fileEntries = Object.entries(report.byFile).sort((a, b) => b[1] - a[1]);
  for (const [file, count] of fileEntries) {
    lines.push(`| \`${file}\` | ${count} |`);
  }
  if (fileEntries.length === 0) {
    lines.push('| _(none)_ | 0 |');
  }
  lines.push(
    '',
    '## CI gate semantics',
    '',
    '- Default (`pnpm lint:tokens:mockups`) = inventory only, exit 0.',
    '- Strict (`pnpm lint:tokens:mockups --strict --max-baseline N`) = exit 1 when count > N. CI passes N as a frozen ceiling; introducing a NEW violation fails the gate.',
    '- Whitelist is intentional (plan §7 R3): existing mockup literals carried over from the bridge era are tolerated until DS-16 unwinds the bridge upstream.',
    '',
    '## Refs',
    '',
    '- Sub-issue: [#2070](https://github.com/meepleAi-app/meepleai-monorepo/issues/2070)',
    '- Umbrella: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)',
    '- Plan: [`2026-06-09-ds-17-phase-1-implementation-plan.md`](../docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md) §4.1',
    '- Companion JSON: [`2026-06-09-mockup-token-violations.json`](./2026-06-09-mockup-token-violations.json)',
    ''
  );
  return lines.join('\n');
}

export function writeReports(report, outPaths) {
  const { jsonOut, mdOut } = outPaths;
  if (!existsSync(dirname(jsonOut))) mkdirSync(dirname(jsonOut), { recursive: true });
  if (!existsSync(dirname(mdOut))) mkdirSync(dirname(mdOut), { recursive: true });
  writeFileSync(jsonOut, JSON.stringify(report, null, 2) + '\n', 'utf-8');
  writeFileSync(mdOut, buildMdReport(report), 'utf-8');
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const args = { strict: false, maxBaseline: null, scope: DEFAULT_GLOB, help: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--strict') args.strict = true;
    else if (a === '--help' || a === '-h') args.help = true;
    else if (a === '--max-baseline') {
      const n = Number.parseInt(argv[++i], 10);
      if (Number.isNaN(n) || n < 0) {
        throw new Error(`--max-baseline requires a non-negative integer, got: ${argv[i]}`);
      }
      args.maxBaseline = n;
    } else if (a.startsWith('--max-baseline=')) {
      const n = Number.parseInt(a.slice('--max-baseline='.length), 10);
      if (Number.isNaN(n) || n < 0) {
        throw new Error(`--max-baseline requires a non-negative integer, got: ${a}`);
      }
      args.maxBaseline = n;
    } else if (a === '--scope') {
      args.scope = argv[++i];
    } else if (a.startsWith('--scope=')) {
      args.scope = a.slice('--scope='.length);
    } else {
      throw new Error(`Unknown argument: ${a}`);
    }
  }
  return args;
}

function printHelp() {
  process.stdout.write(
    [
      'lint-tokens-mockups.mjs — legacy CSS token literal reporter (DS-17-2)',
      '',
      'Usage:',
      '  node lint-tokens-mockups.mjs                          # inventory dump, exit 0',
      '  node lint-tokens-mockups.mjs --strict --max-baseline N  # CI gate',
      '  node lint-tokens-mockups.mjs --scope "<glob>"         # override default scope',
      '',
      `Default scope: ${DEFAULT_GLOB}`,
      'Forbidden families: var(--bg-base), var(--gaming-*), var(--nh-*), var(--e-*)',
      '',
      'Exit codes: 0 ok | 1 baseline exceeded | 2 invocation error',
      '',
    ].join('\n')
  );
}

async function main() {
  let args;
  try {
    args = parseArgs(process.argv.slice(2));
  } catch (err) {
    process.stderr.write(`[lint:tokens:mockups] ERROR: ${err.message}\n`);
    printHelp();
    process.exit(2);
  }

  if (args.help) {
    printHelp();
    process.exit(0);
  }

  if (args.strict && args.maxBaseline === null) {
    process.stderr.write(
      '[lint:tokens:mockups] ERROR: --strict requires --max-baseline N\n'
    );
    process.exit(2);
  }

  const result = lintFiles(args.scope, REPO_ROOT);
  const report = buildJsonReport(result, args.maxBaseline);
  writeReports(report, { jsonOut: JSON_OUT, mdOut: MD_OUT });

  const summary =
    `[lint:tokens:mockups] ${result.violations.length} violations across ${report.filesAffected} files ` +
    `(scanned ${result.fileCount} files, scope: ${args.scope})\n` +
    `  JSON: ${relative(REPO_ROOT, JSON_OUT)}\n` +
    `  MD:   ${relative(REPO_ROOT, MD_OUT)}\n`;
  process.stdout.write(summary);

  if (args.strict && result.violations.length > args.maxBaseline) {
    process.stderr.write(
      `[lint:tokens:mockups] FAIL: ${result.violations.length} violations exceed --max-baseline ${args.maxBaseline}\n` +
        '       A new legacy token literal was introduced. Either:\n' +
        '         1. Replace with a canonical semantic token (--background, --foreground, --card, --border, --primary, …)\n' +
        '         2. If intentional inventory regression, raise --max-baseline in CI workflow (rare)\n'
    );
    process.exit(1);
  }

  process.exit(0);
}

// CLI guard — only run main() when invoked directly (cross-platform Windows safe via pathToFileURL).
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((err) => {
    process.stderr.write(`[lint:tokens:mockups] UNEXPECTED: ${err.stack || err.message}\n`);
    process.exit(2);
  });
}
