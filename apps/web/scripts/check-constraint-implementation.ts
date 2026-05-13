#!/usr/bin/env tsx
/**
 * WS-E.2a — Constraint implementation check with confidence tiers.
 *
 * Consumes the extraction snapshot from WS-E.1
 * (`docs/for-developers/audits/mockup-smart-constraints.json`) and assigns
 * each constraint a confidence tier per AC-E.2 (rewritten):
 *
 *   verified            — explicit `@spec-ref <id>` comment in scoped code
 *   likely-implemented  — grep pattern (AC-E.5 table) matched in scoped path
 *   manual-review       — metric_type ∈ {ux-constraint, other} (no auto-grep)
 *   unknown             — neither @spec-ref nor pattern match (default)
 *
 * WS-E.2b workflow will consume these checks: auto-issue only for `unknown`
 * (≥30d old, dedup_key not in false-positive allowlist) or explicit
 * `<!-- spec-missing: <id> -->` annotation.
 *
 * Refs: #1071 (WS-E.2a), #1066 (umbrella), AC-E.2 + AC-E.5 + AC-E.7.
 */
import { readFileSync, readdirSync, writeFileSync, mkdirSync, statSync } from 'node:fs';
import { dirname, join, resolve, basename, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

import type { MetricType } from './extract-mockup-smart-constraints';

const HERE = dirname(fileURLToPath(import.meta.url));
const DEFAULT_REPO_ROOT = resolve(HERE, '../../..');
const DEFAULT_INPUT = resolve(
  HERE,
  '../../../docs/for-developers/audits/mockup-smart-constraints.json'
);
const DEFAULT_OUTPUT = resolve(
  HERE,
  '../../../docs/for-developers/audits/mockup-smart-implementation.json'
);

export type Tier = 'verified' | 'likely-implemented' | 'manual-review' | 'unknown' | 'missing';

export interface Constraint {
  mockup: string;
  id: string | null;
  text: string;
  metric_type: MetricType;
  target_value: { comparator: string; value: number; unit: string } | null;
  applies_to: string[];
}

/**
 * AC-E.5 implementation-marker map.
 *
 *  - `pathScopes`: relative globs (prefix-match, simple) that constrain WHERE
 *    we look. Out-of-scope matches are deliberately ignored to suppress
 *    obvious false positives (e.g. citation keyword in docs/).
 *  - `patterns`: regex run against file contents. Any-of semantics.
 *  - `manualOnly`: when true, evaluation short-circuits to `manual-review`.
 */
export interface PatternRule {
  pathScopes?: string[];
  patterns?: RegExp[];
  manualOnly?: boolean;
}

export const PATTERN_MAP: Record<MetricType, PatternRule> = {
  latency: {
    pathScopes: ['apps/web/src', 'apps/api/src'],
    patterns: [
      /\bhistogram[^.]*\.\s*[a-z]*latency/i,
      /\brecordLatency\b/,
      /\buseApiCall\b.*\btiming\b/i,
      /\bOpenTelemetry\b.*\bmeter\b/i,
    ],
  },
  performance: {
    pathScopes: ['apps/web/src'],
    patterns: [
      /\blighthouse\b.*\bbudget\b/i,
      /\bperf[a-z]*\.(?:test|spec)\b/i,
      /\bProfiler\b/,
      /\buseMemo\b.*\breason\b/i,
    ],
  },
  citation: {
    pathScopes: ['apps/web/src'],
    patterns: [/\bCitationChip\b/, /\bdata-citation\b/, /<Citation[A-Z]/, /\bcitation-source\b/],
  },
  confidence: {
    pathScopes: ['apps/web/src'],
    patterns: [
      /\bConfidenceIndicator\b/,
      /\bdata-confidence\b/,
      /\bconfidence-marker\b/,
      /\buncertainty-badge\b/,
    ],
  },
  coverage: {
    pathScopes: ['__tests__', 'e2e'],
    patterns: [
      /\bexpect\b[^)]*\btoBeGreaterThan\b[^)]*0\.\d/i,
      /\bexpect\b[^)]*\bcoverage\b[^)]*\d+/i,
    ],
  },
  accessibility: {
    pathScopes: ['__tests__', 'e2e/a11y', 'e2e'],
    patterns: [/\baxe\.run\b/, /\btoBeAccessible\b/, /@axe-core/, /\baxe-playwright\b/],
  },
  'ux-constraint': { manualOnly: true },
  other: { manualOnly: true },
};

export interface ImplementationCheck {
  constraint: Constraint;
  tier: Tier;
  evidence: Array<{ kind: 'spec-ref' | 'pattern'; file: string; line: number; snippet: string }>;
  dedup_key: string;
  evaluated_at: string;
}

export function computeDedupKey(c: Constraint): string {
  // id takes precedence over text — same id across text edits stays under one issue.
  // No id → text hash (text change = new issue, intentional re-evaluation per AC-E.7).
  const identityToken = c.id ?? `text:${c.text}`;
  const raw = `${c.mockup}||${identityToken}||${c.metric_type}`;
  return createHash('sha256').update(raw).digest('hex').slice(0, 16);
}

const TEXT_EXT_RE = /\.(ts|tsx|js|jsx|cjs|mjs|cs|md|yml|yaml|json|css)$/i;
const SKIP_DIRS = new Set([
  'node_modules',
  '.next',
  '.git',
  'dist',
  'build',
  'coverage',
  '.turbo',
  '.cache',
  '.claude',
]);

export interface CodebaseIndex {
  /** Map from canonical scope key (e.g. 'apps/web/src') to absolute file paths. */
  filesByScope: Map<string, Array<{ absPath: string; relPath: string }>>;
}

/**
 * Build an index of text files keyed by scope. We walk once per run and let
 * each constraint pick the slice of files relevant to its scope, vs walking
 * the whole tree N times.
 */
export function scanCodebase(repoRoot: string): CodebaseIndex {
  const filesByScope = new Map<string, Array<{ absPath: string; relPath: string }>>();
  if (!isDirectory(repoRoot)) return { filesByScope };

  const allScopes = new Set<string>();
  for (const rule of Object.values(PATTERN_MAP)) {
    for (const s of rule.pathScopes ?? []) allScopes.add(s);
  }
  for (const s of allScopes) filesByScope.set(s, []);

  function walk(dir: string): void {
    let entries: import('node:fs').Dirent[];
    try {
      entries = readdirSync(dir, { withFileTypes: true });
    } catch {
      return;
    }
    for (const ent of entries) {
      if (ent.isDirectory()) {
        if (SKIP_DIRS.has(ent.name)) continue;
        walk(join(dir, ent.name));
      } else if (ent.isFile() && TEXT_EXT_RE.test(ent.name)) {
        const absPath = join(dir, ent.name);
        const relPath = absPath
          .slice(repoRoot.length + 1)
          .split(sep)
          .join('/');
        // Bucket by every scope whose prefix matches.
        for (const scope of allScopes) {
          // Path-segment match: scope is contained anywhere along the relative path
          // (supports e.g. `__tests__` mid-path, `apps/web/src` prefix-style, etc.)
          if (relPath.split('/').some((_, i, arr) => arr.slice(i).join('/').startsWith(scope))) {
            filesByScope.get(scope)!.push({ absPath, relPath });
          }
        }
      }
    }
  }
  walk(repoRoot);
  return { filesByScope };
}

function isDirectory(p: string): boolean {
  try {
    return statSync(p).isDirectory();
  } catch {
    return false;
  }
}

export function evaluateConstraint(
  c: Constraint,
  index: CodebaseIndex,
  now: Date = new Date()
): ImplementationCheck {
  const evaluated_at = now.toISOString().replace(/\.\d+Z$/, 'Z');
  const dedup_key = computeDedupKey(c);
  const rule = PATTERN_MAP[c.metric_type];

  // Manual-review short-circuit (AC-E.5: ux-constraint, other).
  if (rule.manualOnly) {
    return { constraint: c, tier: 'manual-review', evidence: [], dedup_key, evaluated_at };
  }

  // Scan all files in this metric_type's scopes. Files that match multiple
  // scopes are still inspected once because we deduplicate by relPath below.
  const scopedFiles = new Map<string, { absPath: string; relPath: string }>();
  for (const scope of rule.pathScopes ?? []) {
    for (const entry of index.filesByScope.get(scope) ?? []) {
      scopedFiles.set(entry.relPath, entry);
    }
  }

  const specRefRe = c.id ? new RegExp(`@spec-ref\\s+${escapeRegex(c.id)}\\b`) : null;
  const evidence: ImplementationCheck['evidence'] = [];
  let specRefFound = false;

  for (const entry of scopedFiles.values()) {
    let content: string;
    try {
      content = readFileSync(entry.absPath, 'utf8');
    } catch {
      continue;
    }
    if (specRefRe) {
      const m = content.match(specRefRe);
      if (m) {
        const line = lineOf(content, m.index ?? 0);
        evidence.push({
          kind: 'spec-ref',
          file: entry.relPath,
          line,
          snippet: trimSnippet(content, m.index ?? 0),
        });
        specRefFound = true;
      }
    }
    for (const re of rule.patterns ?? []) {
      const m = content.match(re);
      if (m) {
        const line = lineOf(content, m.index ?? 0);
        evidence.push({
          kind: 'pattern',
          file: entry.relPath,
          line,
          snippet: trimSnippet(content, m.index ?? 0),
        });
      }
    }
  }

  if (specRefFound) {
    return { constraint: c, tier: 'verified', evidence, dedup_key, evaluated_at };
  }
  if (evidence.length > 0) {
    return { constraint: c, tier: 'likely-implemented', evidence, dedup_key, evaluated_at };
  }
  return { constraint: c, tier: 'unknown', evidence: [], dedup_key, evaluated_at };
}

function lineOf(content: string, idx: number): number {
  let line = 1;
  for (let i = 0; i < idx && i < content.length; i++) {
    if (content[i] === '\n') line += 1;
  }
  return line;
}

function trimSnippet(content: string, idx: number): string {
  const lineStart = content.lastIndexOf('\n', idx) + 1;
  const lineEnd = content.indexOf('\n', idx);
  const slice = content.slice(lineStart, lineEnd === -1 ? content.length : lineEnd);
  return slice.length > 160 ? slice.slice(0, 160) + '…' : slice;
}

function escapeRegex(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

export interface CheckReport {
  generatedAt: string;
  totalConstraints: number;
  byTier: Record<Tier, number>;
  checks: ImplementationCheck[];
}

export function runChecks(
  constraints: Constraint[],
  repoRoot: string,
  now: Date = new Date()
): CheckReport {
  const index = scanCodebase(repoRoot);
  const checks = constraints.map(c => evaluateConstraint(c, index, now));
  const byTier: Record<Tier, number> = {
    verified: 0,
    'likely-implemented': 0,
    'manual-review': 0,
    unknown: 0,
    missing: 0,
  };
  for (const ck of checks) byTier[ck.tier] += 1;
  return {
    generatedAt: now.toISOString().replace(/\.\d+Z$/, 'Z'),
    totalConstraints: checks.length,
    byTier,
    checks,
  };
}

const invokedAsCli =
  typeof process !== 'undefined' &&
  process.argv[1] &&
  fileURLToPath(import.meta.url) === resolve(process.argv[1]);

if (invokedAsCli) {
  const input =
    process.argv.find(a => a.startsWith('--input='))?.slice('--input='.length) ?? DEFAULT_INPUT;
  const output =
    process.argv.find(a => a.startsWith('--out='))?.slice('--out='.length) ?? DEFAULT_OUTPUT;
  const root =
    process.argv.find(a => a.startsWith('--root='))?.slice('--root='.length) ?? DEFAULT_REPO_ROOT;

  const extraction = JSON.parse(readFileSync(input, 'utf8')) as { constraints: Constraint[] };
  const report = runChecks(extraction.constraints, root);
  mkdirSync(dirname(output), { recursive: true });
  writeFileSync(output, JSON.stringify(report, null, 2) + '\n', 'utf8');

  console.log(`✓ Wrote ${output}`);
  console.log(`  ${report.totalConstraints} constraints evaluated.`);
  console.log(
    `  By tier: ` +
      Object.entries(report.byTier)
        .filter(([, v]) => v > 0)
        .map(([k, v]) => `${k}=${v}`)
        .join(' ')
  );
}
