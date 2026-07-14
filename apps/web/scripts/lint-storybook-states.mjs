/**
 * lint-storybook-states.mjs — canonical-state coverage gate (DEC-A5, umbrella #2342)
 *
 * Walks MOCKUPS_INDEX page-mock entries → fidelity.json (by mockup.source) →
 * story_path → states implemented, and classifies each entry:
 *   - covered            : story implements every declared canonical state
 *   - coverage-gap       : no fidelity, or fidelity without story_path (whitelist-incremental)
 *   - contract-violation : story omits a declared canonical state (always blocking)
 *   - skipped-obsolete   : fidelity design_intent === 'forward-refactor-obsolete'
 *
 * Modes: inventory (default, exit 0) | strict (--strict --max-baseline N).
 * Refs: docs/superpowers/specs/2026-07-14-lint-storybook-states-design.md
 */
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { globSync } from 'glob';
import { parseMockupsIndex } from './mockup-annotations/inject-annotations.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..');
const MOCKUPS_INDEX = resolve(REPO_ROOT, 'admin-mockups', 'MOCKUPS_INDEX.md');
const AUDIT_DIR = resolve(REPO_ROOT, 'audits');
const JSON_OUT = resolve(AUDIT_DIR, '2026-07-14-storybook-states-coverage.json');
const MD_OUT = resolve(AUDIT_DIR, '2026-07-14-storybook-states-coverage.md');

export const CANONICAL_STATES = Object.freeze(['default', 'empty', 'loading', 'error', 'sse']);

/** Canonicalize one raw state token. empty-* → empty. Non-canonical → null. */
export function normalizeState(raw) {
  if (typeof raw !== 'string') return null;
  const s = raw.trim().toLowerCase();
  if (s === 'default' || s === 'loading' || s === 'error' || s === 'sse') return s;
  if (s === 'empty' || s.startsWith('empty-')) return 'empty';
  return null;
}

const OVERRIDE_RE = /canonicalStates\s*:\s*\[([^\]]*)\]/;
const STATE_LITERAL_RE = /['"`](default|empty[\w-]*|loading|error|sse|offline|quota-(?:soft|hard))['"`]/g;

/**
 * Hybrid state detection: explicit override wins, else heuristic scan.
 * Heuristic is intentionally "satisfies-only": a false-positive match can only
 * ever SATISFY a declared state, never invent a contract-violation — so it
 * fails safe for the blocking direction. Use `parameters.canonicalStates` to
 * override for stories the heuristic can't read.
 */
export function detectStates(storySource) {
  const set = new Set();
  const override = OVERRIDE_RE.exec(storySource);
  if (override) {
    const quoted = override[1].match(/['"`]([^'"`]+)['"`]/g) || [];
    for (const q of quoted) {
      const norm = normalizeState(q.slice(1, -1));
      if (norm) set.add(norm);
    }
    return set;
  }
  STATE_LITERAL_RE.lastIndex = 0;
  let m;
  while ((m = STATE_LITERAL_RE.exec(storySource)) !== null) {
    const norm = normalizeState(m[1]);
    if (norm) set.add(norm);
  }
  return set;
}

/** Build Map<mockupSource, {fidelityPath, fidelity}>. Malformed JSON skipped. */
export function buildFidelityIndex(fidelityRelPaths, readFile) {
  const bySource = new Map();
  for (const rel of fidelityRelPaths) {
    let obj;
    try {
      obj = JSON.parse(readFile(rel));
    } catch {
      continue;
    }
    const source = obj && obj.mockup && obj.mockup.source;
    if (typeof source === 'string' && source.length > 0) {
      bySource.set(source, { fidelityPath: rel, fidelity: obj });
    }
  }
  return bySource;
}

/** Classify one page-mock entry against its fidelity + story. */
export function classifyMockupEntry(entry, fidelityIndex, io) {
  const mockupSource = `admin-mockups/design_files/${entry.mockup}`;
  const base = { mockup: entry.mockup, routes: entry.routes, mockupSource };

  const hit = fidelityIndex.get(mockupSource);
  if (!hit) return { ...base, verdict: 'coverage-gap', reason: 'no-fidelity' };

  const acceptance = hit.fidelity.acceptance || {};
  if (acceptance.design_intent === 'forward-refactor-obsolete') {
    return { ...base, verdict: 'skipped-obsolete' };
  }

  const storyPath = acceptance.story_path;
  if (!storyPath) return { ...base, verdict: 'coverage-gap', reason: 'no-story-path' };
  if (!io.exists(storyPath)) return { ...base, verdict: 'coverage-gap', reason: 'story-missing' };

  const declared = [
    ...new Set((acceptance.states_covered || []).map(normalizeState).filter(Boolean)),
  ].filter((s) => CANONICAL_STATES.includes(s));

  const detected = detectStates(io.readFile(storyPath));
  const missing = declared.filter((s) => !detected.has(s));

  if (missing.length > 0) {
    return {
      ...base,
      verdict: 'contract-violation',
      storyPath,
      declared,
      detected: [...detected],
      missing,
    };
  }
  return { ...base, verdict: 'covered', storyPath, declared, detected: [...detected] };
}

/** Pure: parse index markdown, classify each page-mock entry. */
export function scanEntries(indexMd, fidelityIndex, io) {
  const entries = parseMockupsIndex(indexMd);
  return entries.map((e) => classifyMockupEntry(e, fidelityIndex, io));
}

export function buildJsonReport(results, baseline) {
  const counts = { covered: 0, coverageGaps: 0, contractViolations: 0, skippedObsolete: 0 };
  const coverageGaps = [];
  const contractViolations = [];
  for (const r of results) {
    if (r.verdict === 'covered') counts.covered += 1;
    else if (r.verdict === 'coverage-gap') {
      counts.coverageGaps += 1;
      coverageGaps.push({ mockup: r.mockup, routes: r.routes, reason: r.reason });
    } else if (r.verdict === 'contract-violation') {
      counts.contractViolations += 1;
      contractViolations.push({
        mockup: r.mockup, routes: r.routes, storyPath: r.storyPath,
        declared: r.declared, detected: r.detected, missing: r.missing,
      });
    } else if (r.verdict === 'skipped-obsolete') counts.skippedObsolete += 1;
  }
  return {
    generatedAt: new Date().toISOString(),
    generatedFrom: 'admin-mockups/MOCKUPS_INDEX.md',
    canonicalStates: [...CANONICAL_STATES],
    totalMappableEntries: results.length,
    baselineMaxCoverageGaps: baseline,
    counts,
    coverageGaps,
    contractViolations,
  };
}

export function buildMdReport(report) {
  const { counts, canonicalStates } = report;
  const lines = [];
  lines.push('# Storybook canonical-state coverage (DEC-A5 / #2342)', '');
  lines.push(`Generated: ${report.generatedAt}`);
  lines.push(`Source: \`${report.generatedFrom}\` · Canonical states: ${canonicalStates.join(', ')}`, '');
  lines.push('| Metric | Count |', '| --- | --- |');
  lines.push(`| Total page-mock entries | ${report.totalMappableEntries} |`);
  lines.push(`| Covered | ${counts.covered} |`);
  lines.push(`| Coverage gaps (baseline ${report.baselineMaxCoverageGaps ?? 'n/a'}) | ${counts.coverageGaps} |`);
  lines.push(`| Contract violations (always blocking) | ${counts.contractViolations} |`);
  lines.push(`| Skipped (obsolete) | ${counts.skippedObsolete} |`, '');
  if (report.contractViolations.length) {
    lines.push('## Contract violations (must be 0)', '');
    lines.push('| Mockup | Story | Declared | Detected | Missing |', '| --- | --- | --- | --- | --- |');
    for (const v of report.contractViolations) {
      lines.push(`| \`${v.mockup}\` | \`${v.storyPath}\` | ${v.declared.join(', ')} | ${v.detected.join(', ')} | **${v.missing.join(', ')}** |`);
    }
    lines.push('');
  }
  if (report.coverageGaps.length) {
    lines.push('## Coverage gaps (whitelist-incremental, ratchet down)', '');
    lines.push('| Mockup | Routes | Reason |', '| --- | --- | --- |');
    for (const g of report.coverageGaps) {
      lines.push(`| \`${g.mockup}\` | ${g.routes.join(', ')} | ${g.reason} |`);
    }
    lines.push('');
  }
  lines.push('## Gate semantics', '');
  lines.push('- **contract-violation**: story omits a state its fidelity declares → **always fails** (fix story or align `states_covered`).');
  lines.push('- **coverage-gap**: mockup with no fidelity/story → tolerated under `--max-baseline N`; a NEW gap fails. Migrate a page → lower `N` (ratchet-down).');
  return lines.join('\n') + '\n';
}

export function parseArgs(argv) {
  const args = { strict: false, maxBaseline: null, verbose: false, help: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--strict') args.strict = true;
    else if (a === '--verbose' || a === '-v') args.verbose = true;
    else if (a === '--help' || a === '-h') args.help = true;
    else if (a === '--max-baseline') {
      const n = Number.parseInt(argv[++i], 10);
      if (Number.isNaN(n) || n < 0) throw new Error(`--max-baseline requires a non-negative integer, got: ${argv[i]}`);
      args.maxBaseline = n;
    } else if (a.startsWith('--max-baseline=')) {
      const n = Number.parseInt(a.slice('--max-baseline='.length), 10);
      if (Number.isNaN(n) || n < 0) throw new Error(`--max-baseline requires a non-negative integer, got: ${a}`);
      args.maxBaseline = n;
    } else {
      throw new Error(`Unknown argument: ${a}`);
    }
  }
  return args;
}

function printHelp() {
  process.stdout.write(
    'Usage: node scripts/lint-storybook-states.mjs [--strict --max-baseline N] [--verbose] [--help]\n' +
      '  (no flags)   inventory: write audit reports, exit 0\n' +
      '  --strict     fail (exit 1) if coverageGaps > --max-baseline OR contractViolations > 0\n'
  );
}

async function main() {
  let args;
  try {
    args = parseArgs(process.argv.slice(2));
  } catch (err) {
    process.stderr.write(`[lint:storybook-states] ERROR: ${err.message}\n`);
    printHelp();
    process.exit(2);
  }
  if (args.help) {
    printHelp();
    process.exit(0);
  }
  if (args.strict && args.maxBaseline === null) {
    process.stderr.write('[lint:storybook-states] ERROR: --strict requires --max-baseline N\n');
    process.exit(2);
  }

  // Exclude docs/for-developers/frontend/templates/examples: these duplicate
  // mockup.source values from admin-mockups/design_files/ canonical fidelity
  // files but with divergent content (e.g. story_path). glob traversal order
  // is not guaranteed across platforms, so without this exclusion the
  // last-writer-wins index in buildFidelityIndex is non-deterministic between
  // platforms (e.g. Windows dev box vs Linux CI). The design_files copy is
  // always authoritative.
  const fidelityFiles = globSync('**/*.fidelity.json', {
    cwd: REPO_ROOT,
    ignore: [
      '**/node_modules/**',
      '**/.next/**',
      '**/.claude/**',
      '**/dist/**',
      '**/coverage/**',
      '**/templates/examples/**',
    ],
    nodir: true,
  });
  const readRel = (rel) => readFileSync(resolve(REPO_ROOT, rel), 'utf-8');
  const io = { exists: (rel) => existsSync(resolve(REPO_ROOT, rel)), readFile: readRel };
  const fidelityIndex = buildFidelityIndex(fidelityFiles, readRel);

  const indexMd = readFileSync(MOCKUPS_INDEX, 'utf-8');
  const results = scanEntries(indexMd, fidelityIndex, io);
  const report = buildJsonReport(results, args.maxBaseline);

  writeFileSync(JSON_OUT, JSON.stringify(report, null, 2) + '\n', 'utf-8');
  writeFileSync(MD_OUT, buildMdReport(report), 'utf-8');

  const c = report.counts;
  process.stdout.write(
    `[lint:storybook-states] entries=${report.totalMappableEntries} covered=${c.covered} ` +
      `gaps=${c.coverageGaps} contract=${c.contractViolations} skipped=${c.skippedObsolete}\n` +
      `  JSON: ${relative(REPO_ROOT, JSON_OUT)}\n  MD:   ${relative(REPO_ROOT, MD_OUT)}\n`
  );
  if (args.verbose) {
    for (const v of report.contractViolations) {
      process.stdout.write(`  contract-violation ${v.mockup} missing: ${v.missing.join(', ')}\n`);
    }
  }

  if (args.strict) {
    const failGaps = c.coverageGaps > args.maxBaseline;
    const failContract = c.contractViolations > 0;
    if (failGaps || failContract) {
      if (failContract) {
        process.stderr.write(
          `[lint:storybook-states] FAIL: ${c.contractViolations} contract-violation(s). ` +
            'A story omits a canonical state its fidelity declares. Fix the story, add ' +
            'parameters.canonicalStates, or align states_covered.\n'
        );
      }
      if (failGaps) {
        process.stderr.write(
          `[lint:storybook-states] FAIL: ${c.coverageGaps} coverage-gaps exceed --max-baseline ${args.maxBaseline}. ` +
            'A new page-mock lacks a Storybook story/fidelity. Add one or raise the baseline (rare).\n'
        );
      }
      process.exit(1);
    }
  }
  process.exit(0);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((err) => {
    process.stderr.write(`[lint:storybook-states] UNEXPECTED: ${err.stack || err.message}\n`);
    process.exit(2);
  });
}
