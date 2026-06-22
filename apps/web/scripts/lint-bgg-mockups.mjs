#!/usr/bin/env node
/**
 * lint-bgg-mockups.mjs — user-side BGG copy reporter for mockups + FE source (DS-17 §2151)
 *
 * Defense-in-depth complement to:
 *   - `local/no-bgg-host` ESLint rule (HOSTNAME-level, FE source only)
 *   - `pnpm lint:bgg` (HOSTNAME-level, broader file scope incl. seed manifests)
 *
 * This script is BROADER than both: it flags the *user-facing strings*
 * "BGG", "BoardGameGeek", "boardgamegeek" (case-insensitive) wherever a
 * user could see them. It's the policy-level gate for ADR-059 §2: even
 * mentioning BGG to end users is forbidden unless explicitly justified.
 *
 * Scope:
 *   - admin-mockups/design_files/**\/*.{html,jsx}
 *   - apps/web/src/**\/*.{ts,tsx,js,jsx}
 *
 * Allowlist semantics (any one is enough to pass):
 *   1. Mockup sibling `<base>.fidelity.json` has `design_intent: "forward-refactor-obsolete"`
 *      (the mockup is documented as legacy by design — see ADR-059).
 *   2. File path matches an admin scope (admin server-to-server BGG is legit per ADR-059 §2).
 *   3. The line itself, the line immediately above it, or anywhere in the
 *      enclosing 3-line window has a `BGG-ALLOWED:` justification marker.
 *      Accepted comment forms (HTML, block-comment, line-comment):
 *         <!-- BGG-ALLOWED: server-to-server admin OAuth -->
 *         // BGG-ALLOWED: test asserts the BGG host blocklist behavior
 *      Block-comment form (using fence to avoid breaking this JSDoc):
 *         block-comment with BGG-ALLOWED: justification text inside
 *   4. File path is on the well-known legitimate list (types/bgg.ts, bggClient.ts,
 *      bgg-tsv.ts, cover-utils.ts) — these implement the BGG ban itself.
 *   5. Test/story files are excluded (assertions about BGG don't surface to users).
 *
 * Whitelist-incremental rationale (mirrors DS-17-2 #2070 lint-tokens-mockups.mjs):
 *   Many mockups carry residual BGG copy from before the cleanup waves
 *   (#2166, #2208, #2214, #2220). Rather than block on a one-shot full
 *   sweep, we snapshot the current count as a hard ceiling. NEW BGG copy
 *   is rejected by CI; existing copy is tracked until later cleanup rounds
 *   bring the baseline down.
 *
 * Usage:
 *   node lint-bgg-mockups.mjs                            # inventory, exit 0
 *   node lint-bgg-mockups.mjs --strict --max-baseline N  # CI gate
 *   node lint-bgg-mockups.mjs --verbose                  # print each hit
 *   node lint-bgg-mockups.mjs --help
 *
 * Exit codes:
 *   0  inventory ok OR strict run within baseline
 *   1  strict run exceeded baseline (NEW BGG copy introduced)
 *   2  invocation error (bad flag, IO failure)
 *
 * Refs:
 *   Issue:    #2151 (DS-17 BGG mockup lint gate)
 *   Pattern:  apps/web/scripts/lint-tokens-mockups.mjs (DS-17-2 #2070)
 *   ADR:      docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md
 *   Parent:   #2123 (BGG ToS user-side asset ban — 3 plane enforcement)
 */

import { readFileSync, writeFileSync, mkdirSync, existsSync, statSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { globSync } from 'glob';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..');
const AUDIT_DIR = resolve(REPO_ROOT, 'audits');
const JSON_OUT = resolve(AUDIT_DIR, '2026-06-14-bgg-mockup-violations.json');
const MD_OUT = resolve(AUDIT_DIR, '2026-06-14-bgg-mockup-violations.md');

// ---------------------------------------------------------------------------
// Regex & defaults — exported for unit testing
// ---------------------------------------------------------------------------

/**
 * Matches user-facing BGG strings. Word-boundary anchors prevent false
 * positives on identifiers like "blogger" that happen to contain "bgg" but
 * also intentionally allow no-space contexts like "BoardGameGeek?" or
 * "BGG-rating" common in copy text.
 */
export const BGG_USER_COPY_REGEX = /\bBGG\b|\bBoardGameGeek\b|\bboardgamegeek\b/gi;

/**
 * Line-level justification marker. The token name avoids the literal
 * "BGG" outside the marker so we don't false-flag the comment itself.
 */
export const BGG_ALLOWED_MARKER_REGEX = /BGG-ALLOWED:/;

export const MOCKUP_GLOB = 'admin-mockups/design_files/**/*.{html,jsx}';
export const FE_SOURCE_GLOB = 'apps/web/src/**/*.{ts,tsx,js,jsx}';

export const DEFAULT_IGNORE = [
  '**/node_modules/**',
  '**/.next/**',
  '**/dist/**',
  '**/.git/**',
  '**/coverage/**',
  // Test files: BGG references assert the BGG ban itself.
  '**/__tests__/**',
  '**/*.test.ts',
  '**/*.test.tsx',
  '**/*.test.js',
  '**/*.test.jsx',
  '**/*.spec.ts',
  '**/*.spec.tsx',
  '**/*.spec.js',
  '**/*.spec.jsx',
  // Storybook stories: not user-facing runtime.
  '**/*.stories.ts',
  '**/*.stories.tsx',
  '**/*.stories.js',
  '**/*.stories.jsx',
  '**/*.story.ts',
  '**/*.story.tsx',
  '**/*.story.js',
  '**/*.story.jsx',
];

/**
 * File paths or directory prefixes that are admin scope per ADR-059 §2
 * (server-to-server BGG is legitimate for admin curators).
 */
export const ADMIN_SCOPE_RELS = [
  // FE admin dashboards
  'apps/web/src/app/admin/',
  'apps/web/src/app/api/',
  'apps/web/src/components/admin/',
  // Admin-side server config / route configs
  'apps/web/src/config/admin-navigation.ts',
];

/**
 * Files implementing the BGG ban itself or legitimate BGG-aware data types.
 * These reference BGG by name in a non-user-facing way.
 */
export const WELL_KNOWN_LEGITIMATE_RELS = [
  'apps/web/src/types/bgg.ts',
  'apps/web/src/types/collection.ts',
  'apps/web/src/lib/api/clients/bggClient.ts',
  'apps/web/src/lib/parsers/bgg-tsv.ts',
  'apps/web/src/lib/games/cover-utils.ts',
  'apps/web/src/lib/images/safe-loader.ts',
  // Public next.config.js host blocklist literals + ADR docstrings
  'apps/web/next.config.js',
];

// ---------------------------------------------------------------------------
// Fidelity classification
// ---------------------------------------------------------------------------

/**
 * Read the sibling `<base>.fidelity.json` for a mockup file and return its
 * `design_intent` value (one of "current" | "forward-refactor" |
 * "forward-refactor-obsolete" | null). Returns null on any error.
 *
 * The lookup is best-effort: missing files, malformed JSON, or absent
 * `design_intent` keys all resolve to null (= treat as `current`).
 */
export function readDesignIntent(mockupAbsPath, fs = { readFileSync, statSync }) {
  // Strip extension: foo.html → foo, foo.jsx → foo, foo.state-02-empty.html → foo.state-02-empty
  const m = mockupAbsPath.match(/^(.*)\.(html|jsx)$/i);
  if (!m) return null;
  const base = m[1];
  // Multi-state mockups share the canonical mockup's fidelity file.
  // Strip a `-state-NN-<label>` tail so e.g. `sp4-library-desktop-state-02-empty`
  // reads from `sp4-library-desktop.fidelity.json`.
  const canonical = base.replace(/-state-\d{2}-[a-z0-9-]+$/i, '');
  const fidelityPath = `${canonical}.fidelity.json`;
  try {
    fs.statSync(fidelityPath);
  } catch {
    return null;
  }
  let raw;
  try {
    raw = fs.readFileSync(fidelityPath, 'utf-8');
  } catch {
    return null;
  }
  try {
    const obj = JSON.parse(raw);
    return typeof obj.design_intent === 'string' ? obj.design_intent : null;
  } catch {
    return null;
  }
}

// ---------------------------------------------------------------------------
// Path classification
// ---------------------------------------------------------------------------

export function isAdminScope(repoRelPath) {
  const norm = repoRelPath.replace(/\\/g, '/');
  return ADMIN_SCOPE_RELS.some(prefix => norm === prefix || norm.startsWith(prefix));
}

export function isWellKnownLegitimate(repoRelPath) {
  const norm = repoRelPath.replace(/\\/g, '/');
  return WELL_KNOWN_LEGITIMATE_RELS.includes(norm);
}

// ---------------------------------------------------------------------------
// Core lint primitives
// ---------------------------------------------------------------------------

/**
 * Returns true when the line at index `lineIdx` (0-based) sits inside a
 * `BGG-ALLOWED:` justification window. We accept the marker on the same
 * line OR on any of the 2 preceding lines (block-comment patterns:
 * `/* BGG-ALLOWED: …` followed by a JSX/HTML usage line).
 */
export function hasAllowedJustification(lines, lineIdx) {
  const start = Math.max(0, lineIdx - 2);
  for (let i = start; i <= lineIdx; i++) {
    if (BGG_ALLOWED_MARKER_REGEX.test(lines[i] ?? '')) return true;
  }
  return false;
}

/**
 * Scan a single text blob for BGG copy. Returns violations not covered by
 * a `BGG-ALLOWED:` window. Each record: `{ file, line, column, match }`.
 */
export function findViolationsInText(text, filePath) {
  if (!text || typeof text !== 'string') return [];
  const out = [];
  const lines = text.split('\n');
  for (let i = 0; i < lines.length; i++) {
    const re = new RegExp(BGG_USER_COPY_REGEX.source, BGG_USER_COPY_REGEX.flags);
    let matchHit = false;
    const lineHits = [];
    for (const m of lines[i].matchAll(re)) {
      matchHit = true;
      lineHits.push({
        file: filePath,
        line: i + 1,
        column: (m.index ?? 0) + 1,
        match: m[0],
      });
    }
    if (matchHit && !hasAllowedJustification(lines, i)) {
      out.push(...lineHits);
    }
  }
  return out;
}

/**
 * Lint a glob of files relative to `repoRoot`. Applies the full allowlist
 * pipeline (path scope, well-known list, design_intent file-level skip,
 * per-line justification marker).
 *
 * Returns `{ fileCount, violations, skipped }` where:
 *   - fileCount: total matched by the glob (pre-allowlist)
 *   - violations: surviving hits (post-allowlist), sorted file/line/column
 *   - skipped: counts by allowlist reason for the audit report
 */
export function lintFiles(globPattern, repoRoot, opts = {}) {
  const ignore = opts.ignore ?? DEFAULT_IGNORE;
  const isMockup = opts.isMockup ?? false; // when true, apply design_intent skip
  const absoluteFiles = globSync(globPattern, {
    cwd: repoRoot,
    ignore,
    absolute: true,
    nodir: true,
  });

  const allViolations = [];
  const skipped = {
    admin_scope: 0,
    well_known_legitimate: 0,
    design_intent_obsolete: 0,
  };

  for (const abs of absoluteFiles) {
    const rel = relative(repoRoot, abs).replace(/\\/g, '/');

    if (!isMockup) {
      if (isAdminScope(rel)) {
        skipped.admin_scope++;
        continue;
      }
      if (isWellKnownLegitimate(rel)) {
        skipped.well_known_legitimate++;
        continue;
      }
    } else {
      const intent = readDesignIntent(abs);
      if (intent === 'forward-refactor-obsolete') {
        skipped.design_intent_obsolete++;
        continue;
      }
    }

    let text;
    try {
      text = readFileSync(abs, 'utf-8');
    } catch {
      continue;
    }
    allViolations.push(...findViolationsInText(text, rel));
  }

  allViolations.sort((a, b) => {
    if (a.file !== b.file) return a.file < b.file ? -1 : 1;
    if (a.line !== b.line) return a.line - b.line;
    return a.column - b.column;
  });

  return { fileCount: absoluteFiles.length, violations: allViolations, skipped };
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

function buildJsonReport(mockupResult, feResult, baseline) {
  const combined = [...mockupResult.violations, ...feResult.violations];
  const byFile = groupBy(combined, v => v.file);
  return {
    generatedAt: new Date().toISOString(),
    scopes: {
      mockups: MOCKUP_GLOB,
      feSource: FE_SOURCE_GLOB,
    },
    pattern: BGG_USER_COPY_REGEX.source,
    totalViolations: combined.length,
    mockup: {
      fileCount: mockupResult.fileCount,
      violations: mockupResult.violations.length,
      filesAffected: new Set(mockupResult.violations.map(v => v.file)).size,
      skipped: mockupResult.skipped,
    },
    feSource: {
      fileCount: feResult.fileCount,
      violations: feResult.violations.length,
      filesAffected: new Set(feResult.violations.map(v => v.file)).size,
      skipped: feResult.skipped,
    },
    baselineMaxViolations: baseline,
    byFile,
    violations: combined,
  };
}

function buildMdReport(report) {
  const lines = [
    '# BGG Mockup + FE Source Violations — Inventory (DS-17 §2151)',
    '',
    '| Field | Value |',
    '|---|---|',
    `| **Date** | ${report.generatedAt.slice(0, 10)} |`,
    `| **Generator** | \`pnpm lint:bgg-mockups\` (DS-17 §2151) |`,
    `| **ADR** | [\`adr-059-catalog-seed-legal-posture\`](../docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md) |`,
    `| **Pattern** | \`${report.pattern}\` |`,
    `| **Total violations** | ${report.totalViolations} |`,
    `| **Baseline ceiling** | ${report.baselineMaxViolations === null ? '_(none — inventory mode)_' : report.baselineMaxViolations} |`,
    '',
    '## Scope summary',
    '',
    '| Scope | Files scanned | Violations | Files affected | Skipped (admin) | Skipped (well-known) | Skipped (obsolete fidelity) |',
    '|---|---|---|---|---|---|---|',
    `| Mockups (\`${report.scopes.mockups}\`) | ${report.mockup.fileCount} | ${report.mockup.violations} | ${report.mockup.filesAffected} | _(n/a)_ | _(n/a)_ | ${report.mockup.skipped.design_intent_obsolete} |`,
    `| FE source (\`${report.scopes.feSource}\`) | ${report.feSource.fileCount} | ${report.feSource.violations} | ${report.feSource.filesAffected} | ${report.feSource.skipped.admin_scope} | ${report.feSource.skipped.well_known_legitimate} | _(n/a)_ |`,
    '',
    '## Violations by file',
    '',
    '| File | Count |',
    '|---|---|',
  ];
  const fileEntries = Object.entries(report.byFile).sort((a, b) => b[1] - a[1]);
  for (const [file, count] of fileEntries) {
    lines.push(`| \`${file}\` | ${count} |`);
  }
  if (fileEntries.length === 0) {
    lines.push('| _(none)_ | 0 |');
  }
  lines.push(
    '',
    '## How to suppress a legitimate occurrence',
    '',
    '1. **Mockup is intentionally obsolete** — set `design_intent: "forward-refactor-obsolete"` in the sibling `<base>.fidelity.json` (reference #1903 ADR).',
    '2. **Admin server-to-server BGG** (per ADR-059 §2) — move the file under `apps/web/src/app/admin/`, `apps/web/src/components/admin/`, or `apps/web/src/app/api/`. Already-known admin paths pass automatically.',
    '3. **Line-level justification** — add `BGG-ALLOWED: <reason>` as a comment on the offending line, or within the 2 preceding lines:',
    '   ```',
    '   /* BGG-ALLOWED: legitimate parser for legacy BGG export TSV */',
    '   const BGG_TSV_HEADER = "boardgamegeek collection export";',
    '   ```',
    '',
    '## CI gate semantics',
    '',
    '- Default (`pnpm lint:bgg-mockups`) = inventory only, exit 0.',
    '- Strict (`pnpm lint:bgg-mockups --strict --max-baseline N`) = exit 1 when count > N. CI passes N as a frozen ceiling; introducing a NEW BGG copy fails the gate.',
    '- Whitelist is intentional: residual BGG copy in `current` mockups is tracked until later cleanup waves bring the ceiling down. NEW copy is rejected.',
    '',
    '## Refs',
    '',
    '- Issue: [#2151](https://github.com/meepleAi-app/meepleai-monorepo/issues/2151)',
    '- ADR: [`adr-059-catalog-seed-legal-posture.md`](../docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md)',
    '- Pattern reference: [`lint-tokens-mockups.mjs`](../apps/web/scripts/lint-tokens-mockups.mjs) (DS-17-2 #2070)',
    '- Parent BGG ban: [#2123](https://github.com/meepleAi-app/meepleai-monorepo/issues/2123) (3-plane enforcement)',
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
  const args = { strict: false, maxBaseline: null, verbose: false, help: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--strict') args.strict = true;
    else if (a === '--verbose' || a === '-v') args.verbose = true;
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
    } else {
      throw new Error(`Unknown argument: ${a}`);
    }
  }
  return args;
}

function printHelp() {
  process.stdout.write(
    [
      'lint-bgg-mockups.mjs — user-side BGG copy reporter (DS-17 §2151)',
      '',
      'Usage:',
      '  node lint-bgg-mockups.mjs                            # inventory dump, exit 0',
      '  node lint-bgg-mockups.mjs --strict --max-baseline N  # CI gate',
      '  node lint-bgg-mockups.mjs --verbose                  # print each hit',
      '',
      'Scopes:',
      `  - ${MOCKUP_GLOB}`,
      `  - ${FE_SOURCE_GLOB} (admin paths + well-known legit files auto-skipped)`,
      'Pattern: /(BGG|BoardGameGeek|boardgamegeek)/gi (word-boundary anchored)',
      'Allowlist: design_intent="forward-refactor-obsolete" OR admin path OR well-known legit file OR `BGG-ALLOWED:` marker (line or up to 2 preceding lines)',
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
    process.stderr.write(`[lint:bgg-mockups] ERROR: ${err.message}\n`);
    printHelp();
    process.exit(2);
  }

  if (args.help) {
    printHelp();
    process.exit(0);
  }

  if (args.strict && args.maxBaseline === null) {
    process.stderr.write('[lint:bgg-mockups] ERROR: --strict requires --max-baseline N\n');
    process.exit(2);
  }

  const mockupResult = lintFiles(MOCKUP_GLOB, REPO_ROOT, { isMockup: true });
  const feResult = lintFiles(FE_SOURCE_GLOB, REPO_ROOT, { isMockup: false });
  const report = buildJsonReport(mockupResult, feResult, args.maxBaseline);
  writeReports(report, { jsonOut: JSON_OUT, mdOut: MD_OUT });

  const total = mockupResult.violations.length + feResult.violations.length;
  const summary =
    `[lint:bgg-mockups] ${total} violations: ${mockupResult.violations.length} mockup + ${feResult.violations.length} FE source\n` +
    `  Mockup skipped (forward-refactor-obsolete): ${mockupResult.skipped.design_intent_obsolete}\n` +
    `  FE skipped (admin: ${feResult.skipped.admin_scope}, well-known: ${feResult.skipped.well_known_legitimate})\n` +
    `  JSON: ${relative(REPO_ROOT, JSON_OUT)}\n` +
    `  MD:   ${relative(REPO_ROOT, MD_OUT)}\n`;
  process.stdout.write(summary);

  if (args.verbose) {
    const all = [...mockupResult.violations, ...feResult.violations];
    for (const v of all) {
      process.stdout.write(`  ${v.file}:${v.line}:${v.column}: ${v.match}\n`);
    }
  }

  if (args.strict && total > args.maxBaseline) {
    process.stderr.write(
      `[lint:bgg-mockups] FAIL: ${total} violations exceed --max-baseline ${args.maxBaseline}\n` +
        '       A new BGG copy was introduced in user-facing surface. Either:\n' +
        '         1. Remove the BGG reference (preferred — replace with neutral copy)\n' +
        '         2. Move the file to an admin path (apps/web/src/app/admin, /components/admin, /app/api)\n' +
        '         3. For mockups: set design_intent="forward-refactor-obsolete" in <base>.fidelity.json\n' +
        '         4. Add a line-level `BGG-ALLOWED: <reason>` justification comment\n' +
        '       See docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md\n'
    );
    process.exit(1);
  }

  process.exit(0);
}

// CLI guard — only run main() when invoked directly (cross-platform Windows safe via pathToFileURL).
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch(err => {
    process.stderr.write(`[lint:bgg-mockups] UNEXPECTED: ${err.stack || err.message}\n`);
    process.exit(2);
  });
}
