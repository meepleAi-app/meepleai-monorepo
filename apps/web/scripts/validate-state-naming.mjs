#!/usr/bin/env node
/**
 * validate-state-naming.mjs — DS-17 Phase 1 §2071: enforce mockup state-variant naming.
 *
 * Scans `admin-mockups/design_files/**\/*.{html,jsx}` and fails when any file
 * matches `*-state-*.{html,jsx}` but violates the canonical
 * `<base>-state-NN-<label>.<ext>` pattern.
 *
 * Canonical NN → label mapping (fixed; new states require a CODEMOD pass + this script update):
 *   01 → default   (NEVER used in a filename — the bare `<base>.html` IS the default)
 *   02 → empty
 *   03 → loading
 *   04 → error
 *   05 → sse       (opt-in for streaming surfaces)
 *   06 → offline   (opt-in for PWA-relevant surfaces)
 *
 * Failure modes (each exits 1):
 *   - File like `*-state-empty.html` (missing `NN` prefix)
 *   - File like `*-state-99-typo.html` (NN outside catalog)
 *   - File like `*-state-02-loading.html` (NN mismatched with the canonical label)
 *   - File like `*-state-04.html` (missing `-<label>` tail)
 *   - File like `*-state-01-default.html` (`01-default` is forbidden — use bare `<base>`)
 *
 * Usage:
 *   node validate-state-naming.mjs            # exit 0 if all conform
 *   node validate-state-naming.mjs --verbose  # list every state file scanned + verdict
 *
 * Exit codes:
 *   0  all state files conform
 *   1  at least one violation
 *   2  invocation error
 *
 * Refs:
 *   Issue:    #2071 (DS-17 Phase 1)
 *   Umbrella: #2063 (DS-17 Mockup-to-App Fidelity)
 *   Spec:     docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md § Adzic CRIT-2
 *   Pattern:  admin-mockups/README.md § State variants (#2071, DS-17 Phase 1)
 */

import { resolve, dirname, relative, basename } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { globSync } from 'glob';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..');

// Exported for unit testing.
export const STATE_CATALOG = Object.freeze({
  '01': 'default',
  '02': 'empty',
  '03': 'loading',
  '04': 'error',
  '05': 'sse',
  '06': 'offline',
});

export const DEFAULT_GLOB = 'admin-mockups/design_files/**/*.{html,jsx}';

/**
 * Matches `<base>-state-<NN>-<label>` where NN is exactly 2 digits and
 * label is a kebab-case identifier. Anchored to the filename stem only.
 */
export const CANONICAL_STATE_REGEX = /^(.+?)-state-(\d{2})-([a-z][a-z0-9-]*)$/;

/**
 * Loose detector: any file whose stem contains `-state-` is considered
 * a state-variant file and must match `CANONICAL_STATE_REGEX`.
 */
export const STATE_VARIANT_DETECTOR = /-state-/;

/**
 * Classify a single filename. Returns one of:
 *   { ok: true, base, nn, label }     — conforms
 *   { ok: false, reason, suggestion } — does not conform
 *   null                              — not a state variant (skip)
 *
 * Filenames are expected without extension; pass `stem = basename(file, ext)`.
 */
export function classifyStateFilename(stem) {
  if (!STATE_VARIANT_DETECTOR.test(stem)) {
    return null;
  }

  const m = stem.match(CANONICAL_STATE_REGEX);
  if (!m) {
    return {
      ok: false,
      reason:
        'filename matches `-state-` but not `<base>-state-NN-<label>` (missing NN prefix or label)',
      suggestion: 'rename to `<base>-state-NN-<label>` (e.g. `<base>-state-02-empty`)',
    };
  }

  const [, base, nn, label] = m;
  const canonical = STATE_CATALOG[nn];

  if (!canonical) {
    return {
      ok: false,
      reason: `NN=${nn} is outside the canonical catalog (allowed: ${Object.keys(STATE_CATALOG).join(', ')})`,
      suggestion: 'use one of the canonical NN slots — see admin-mockups/README.md § State variants',
    };
  }

  if (nn === '01') {
    return {
      ok: false,
      reason:
        '01-default is forbidden — the bare `<base>.{html,jsx}` IS the default state',
      suggestion: `delete \`${stem}.{html,jsx}\` and keep only \`${base}.{html,jsx}\``,
    };
  }

  if (label !== canonical) {
    return {
      ok: false,
      reason: `label "${label}" does not match the canonical "${canonical}" for NN=${nn}`,
      suggestion: `rename to \`${base}-state-${nn}-${canonical}\``,
    };
  }

  return { ok: true, base, nn, label };
}

/**
 * Walk the glob and return `{ scanned, violations, conforming }` where:
 *   scanned     — total files matched by the glob (pre-classification)
 *   violations  — [{ file, reason, suggestion }] sorted by file
 *   conforming  — [{ file, base, nn, label }] sorted by file
 */
export function lintMockupStateFiles(globPattern, repoRoot) {
  const absoluteFiles = globSync(globPattern, {
    cwd: repoRoot,
    ignore: ['**/node_modules/**', '**/.git/**'],
    absolute: true,
    nodir: true,
  });

  const violations = [];
  const conforming = [];

  for (const abs of absoluteFiles) {
    const rel = relative(repoRoot, abs).replace(/\\/g, '/');
    const fileName = basename(abs);
    const stem = fileName.replace(/\.(html|jsx)$/i, '');

    const classification = classifyStateFilename(stem);
    if (classification === null) continue;

    if (classification.ok) {
      conforming.push({ file: rel, ...classification });
    } else {
      violations.push({ file: rel, reason: classification.reason, suggestion: classification.suggestion });
    }
  }

  violations.sort((a, b) => a.file.localeCompare(b.file));
  conforming.sort((a, b) => a.file.localeCompare(b.file));

  return { scanned: absoluteFiles.length, violations, conforming };
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const args = { verbose: false, help: false };
  for (const a of argv) {
    if (a === '--verbose' || a === '-v') args.verbose = true;
    else if (a === '--help' || a === '-h') args.help = true;
    else throw new Error(`Unknown argument: ${a}`);
  }
  return args;
}

function printHelp() {
  process.stdout.write(
    [
      'validate-state-naming.mjs — DS-17 Phase 1 mockup state-variant lint (#2071)',
      '',
      'Usage:',
      '  node validate-state-naming.mjs            # exit 0 if all conform',
      '  node validate-state-naming.mjs --verbose  # list every state file scanned',
      '',
      `Scope: ${DEFAULT_GLOB}`,
      'Pattern: <base>-state-NN-<label>.{html,jsx}',
      `Catalog: ${Object.entries(STATE_CATALOG).map(([nn, label]) => `${nn}=${label}`).join(', ')}`,
      '',
      'Exit codes: 0 ok | 1 violation found | 2 invocation error',
      '',
    ].join('\n')
  );
}

async function main() {
  let args;
  try {
    args = parseArgs(process.argv.slice(2));
  } catch (err) {
    process.stderr.write(`[lint:mockup-state-naming] ERROR: ${err.message}\n`);
    printHelp();
    process.exit(2);
  }

  if (args.help) {
    printHelp();
    process.exit(0);
  }

  const result = lintMockupStateFiles(DEFAULT_GLOB, REPO_ROOT);

  process.stdout.write(
    `[lint:mockup-state-naming] scanned ${result.scanned} files, ` +
      `${result.conforming.length} state variants conform, ` +
      `${result.violations.length} violations\n`
  );

  if (args.verbose) {
    for (const v of result.conforming) {
      process.stdout.write(`  OK  ${v.file}  → state-${v.nn}-${v.label}\n`);
    }
  }

  if (result.violations.length > 0) {
    process.stderr.write('\n[lint:mockup-state-naming] FAIL — naming violations:\n');
    for (const v of result.violations) {
      process.stderr.write(`  ${v.file}\n    reason: ${v.reason}\n    fix:    ${v.suggestion}\n`);
    }
    process.stderr.write(
      '\nSee admin-mockups/README.md § State variants for the canonical naming table.\n'
    );
    process.exit(1);
  }

  process.exit(0);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch(err => {
    process.stderr.write(`[lint:mockup-state-naming] UNEXPECTED: ${err.stack || err.message}\n`);
    process.exit(2);
  });
}
