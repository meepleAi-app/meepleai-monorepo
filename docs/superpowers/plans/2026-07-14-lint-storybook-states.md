# `lint:storybook-states` Coverage Gate — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a CI gate `lint:storybook-states` that verifies each page-mock's Storybook story actually implements the canonical UI states its `fidelity.json` declares.

**Architecture:** A Node ESM script clones `lint-tokens-mockups.mjs` (inventory + strict/baseline modes, JSON+MD audit reports, exit 0/1/2). It walks `MOCKUPS_INDEX.md` (via the reusable `parseMockupsIndex`) → mockup → `fidelity.json` (indexed by `mockup.source`) → `story_path` → states implemented (`detectStates`, hybrid: `mswForState()` heuristic + explicit `parameters.canonicalStates` override). Two violation classes: COVERAGE-GAP (no fidelity / no story — tolerated under `--max-baseline N`) and CONTRACT-VIOLATION (story omits a declared state — always blocking).

**Tech Stack:** Node ESM (`.mjs`), `glob`, Vitest (TypeScript tests in `apps/web/scripts/__tests__/`), GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-07-14-lint-storybook-states-design.md`

## Global Constraints

- Script is ESM `.mjs` at `apps/web/scripts/lint-storybook-states.mjs`; **all pure functions must be `export`ed** so the TypeScript test can import them.
- Tests are TypeScript at `apps/web/scripts/__tests__/lint-storybook-states.test.ts` (Vitest already collects `**/__tests__/**/*.{test,spec}.{ts,tsx}`).
- Canonical states considered: exactly `['default','empty','loading','error','sse']`. `offline`/`quota-soft`/`quota-hard`/unknown → discarded.
- Exit codes: `0` pass · `1` gate failed (strict) · `2` invocation error. `--strict` without `--max-baseline` → exit 2.
- Reuse `parseMockupsIndex` from `apps/web/scripts/mockup-annotations/inject-annotations.mjs` — do NOT reimplement index parsing.
- Reports written to `audits/2026-07-14-storybook-states-coverage.{json,md}`.
- Unit of classification/count = **page-mock entry** (1 story per mockup), not route.
- CONTRACT-VIOLATION is always blocking (outside baseline); COVERAGE-GAP is whitelist-incremental under `--max-baseline N`.
- stderr/stdout log prefix: `[lint:storybook-states]`.
- Commits: Conventional Commits, subject ≤ 72 chars, end body with `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- Branch: `feature/issue-2342-lint-storybook-states` (already created, parent `main-dev`).

---

## Task 1: Pure state functions (`CANONICAL_STATES`, `normalizeState`, `detectStates`)

**Files:**
- Create: `apps/web/scripts/lint-storybook-states.mjs`
- Test: `apps/web/scripts/__tests__/lint-storybook-states.test.ts`

**Interfaces:**
- Produces:
  - `CANONICAL_STATES: readonly string[]` = `['default','empty','loading','error','sse']`
  - `normalizeState(raw: unknown): string | null` — canonicalizes one raw token; `empty-*` → `empty`; non-canonical → `null`.
  - `detectStates(storySource: string): Set<string>` — hybrid: if source contains `parameters.canonicalStates: [...]` use it verbatim; else scan for state string-literals; normalize all.

- [ ] **Step 1: Write the failing test**

Create `apps/web/scripts/__tests__/lint-storybook-states.test.ts`:

```typescript
/**
 * lint-storybook-states.test.ts — unit tests for lint-storybook-states.mjs
 * (DEC-A5 canonical-state coverage gate, umbrella #2342)
 *
 * Run: pnpm vitest run scripts/__tests__/lint-storybook-states.test.ts
 */
import { describe, it, expect } from 'vitest';
import { CANONICAL_STATES, normalizeState, detectStates } from '../lint-storybook-states.mjs';

describe('CANONICAL_STATES', () => {
  it('is exactly the 5 DEC-A5 states in order', () => {
    expect([...CANONICAL_STATES]).toEqual(['default', 'empty', 'loading', 'error', 'sse']);
  });
});

describe('normalizeState', () => {
  it('passes the 5 canonical states through unchanged', () => {
    for (const s of ['default', 'empty', 'loading', 'error', 'sse']) {
      expect(normalizeState(s)).toBe(s);
    }
  });
  it('collapses empty-* variants to empty', () => {
    expect(normalizeState('empty-first-run')).toBe('empty');
    expect(normalizeState('empty-filtered')).toBe('empty');
    expect(normalizeState('empty-tab-agents')).toBe('empty');
  });
  it('discards non-canonical states', () => {
    expect(normalizeState('offline')).toBeNull();
    expect(normalizeState('quota-soft')).toBeNull();
    expect(normalizeState('quota-hard')).toBeNull();
    expect(normalizeState('segmenting')).toBeNull();
  });
  it('is case-insensitive and trims, discards non-strings', () => {
    expect(normalizeState('  Loading ')).toBe('loading');
    expect(normalizeState(42 as unknown as string)).toBeNull();
    expect(normalizeState('')).toBeNull();
  });
});

describe('detectStates — heuristic', () => {
  it('picks up mswForState() calls and normalizes empty variants', () => {
    const src = `
      export const A = { parameters: { msw: { handlers: mswForState('default') } } };
      export const B = { parameters: { msw: { handlers: mswForState('loading') } } };
      export const C = { parameters: { msw: { handlers: mswForState('error') } } };
      const options = ['default','empty-first-run','loading','error'];
    `;
    expect(detectStates(src)).toEqual(new Set(['default', 'empty', 'loading', 'error']));
  });
  it('does not treat a non-state string literal as a state', () => {
    const src = `HttpResponse.json({ error: 'server error' }, { status: 500 })`;
    expect(detectStates(src)).toEqual(new Set());
  });
});

describe('detectStates — explicit override wins', () => {
  it('uses parameters.canonicalStates verbatim, ignoring heuristic', () => {
    const src = `
      const meta = { parameters: { canonicalStates: ['default','loading','sse'] } };
      // no mswForState here, only phase names:
      const phases = ['idle','segmenting','translating'];
    `;
    expect(detectStates(src)).toEqual(new Set(['default', 'loading', 'sse']));
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run scripts/__tests__/lint-storybook-states.test.ts`
Expected: FAIL — cannot resolve `../lint-storybook-states.mjs` (file does not exist yet).

- [ ] **Step 3: Create the script with the pure functions**

Create `apps/web/scripts/lint-storybook-states.mjs`:

```javascript
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

/** Hybrid state detection: explicit override wins, else heuristic scan. */
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run scripts/__tests__/lint-storybook-states.test.ts`
Expected: PASS (all `describe` blocks green).

- [ ] **Step 5: Commit**

```bash
cd "D:/Repositories/meepleai-monorepo-frontend"
git add apps/web/scripts/lint-storybook-states.mjs apps/web/scripts/__tests__/lint-storybook-states.test.ts
git commit -m "feat(scripts): storybook-states detectStates + normalizeState (DEC-A5 #2342)"
```

---

## Task 2: Fidelity index + entry classification (`buildFidelityIndex`, `classifyMockupEntry`)

**Files:**
- Modify: `apps/web/scripts/lint-storybook-states.mjs`
- Test: `apps/web/scripts/__tests__/lint-storybook-states.test.ts`

**Interfaces:**
- Consumes: `normalizeState`, `detectStates`, `CANONICAL_STATES` (Task 1).
- Produces:
  - `buildFidelityIndex(fidelityRelPaths: string[], readFile: (rel: string) => string): Map<string, { fidelityPath: string, fidelity: object }>` — keyed by `mockup.source`. Malformed JSON is skipped.
  - `classifyMockupEntry(entry: { mockup: string, routes: string[] }, fidelityIndex, io: { exists: (rel: string) => boolean, readFile: (rel: string) => string }): { mockup, routes, mockupSource, verdict, reason?, declared?, detected?, missing? }` — `verdict ∈ {'covered','coverage-gap','contract-violation','skipped-obsolete'}`.

- [ ] **Step 1: Write the failing test**

Append to `apps/web/scripts/__tests__/lint-storybook-states.test.ts`:

```typescript
import { buildFidelityIndex, classifyMockupEntry } from '../lint-storybook-states.mjs';

// In-memory IO helpers for classification tests.
function makeIo(files: Record<string, string>) {
  return {
    exists: (rel: string) => Object.prototype.hasOwnProperty.call(files, rel),
    readFile: (rel: string) => {
      if (!(rel in files)) throw new Error(`ENOENT ${rel}`);
      return files[rel];
    },
  };
}

const fidelityOf = (source: string, statesCovered: string[], storyPath = '', intent = 'current') =>
  JSON.stringify({
    mockup: { source, states: statesCovered },
    acceptance: { states_covered: statesCovered, story_path: storyPath, design_intent: intent },
  });

describe('buildFidelityIndex', () => {
  it('indexes fidelity objects by mockup.source and skips malformed json', () => {
    const files = {
      'a.fidelity.json': fidelityOf('admin-mockups/design_files/x.html', ['default']),
      'b.fidelity.json': '{ not json',
    };
    const idx = buildFidelityIndex(Object.keys(files), (rel) => files[rel]);
    expect(idx.has('admin-mockups/design_files/x.html')).toBe(true);
    expect(idx.size).toBe(1);
  });
});

describe('classifyMockupEntry', () => {
  const entry = { mockup: 'x.html', routes: ['/x'] };
  const src = 'admin-mockups/design_files/x.html';

  it('coverage-gap: no fidelity for the mockup', () => {
    const idx = buildFidelityIndex([], () => '');
    const r = classifyMockupEntry(entry, idx, makeIo({}));
    expect(r.verdict).toBe('coverage-gap');
    expect(r.reason).toBe('no-fidelity');
  });

  it('coverage-gap: fidelity present but story_path empty', () => {
    const files = { 'f.fidelity.json': fidelityOf(src, ['default', 'loading'], '') };
    const idx = buildFidelityIndex(Object.keys(files), (rel) => files[rel]);
    const r = classifyMockupEntry(entry, idx, makeIo(files));
    expect(r.verdict).toBe('coverage-gap');
    expect(r.reason).toBe('no-story-path');
  });

  it('skipped-obsolete: design_intent forward-refactor-obsolete', () => {
    const files = {
      'f.fidelity.json': fidelityOf(src, ['default'], 'story.tsx', 'forward-refactor-obsolete'),
    };
    const idx = buildFidelityIndex(Object.keys(files), (rel) => files[rel]);
    const r = classifyMockupEntry(entry, idx, makeIo({ ...files, 'story.tsx': '' }));
    expect(r.verdict).toBe('skipped-obsolete');
  });

  it('covered: story implements every declared canonical state', () => {
    const story = `mswForState('default'); mswForState('loading'); mswForState('error');`;
    const files = {
      'f.fidelity.json': fidelityOf(src, ['default', 'loading', 'error'], 'story.tsx'),
      'story.tsx': story,
    };
    const idx = buildFidelityIndex(Object.keys(files), (rel) => files[rel]);
    const r = classifyMockupEntry(entry, idx, makeIo(files));
    expect(r.verdict).toBe('covered');
  });

  it('contract-violation: story omits a declared state, listing the missing ones', () => {
    const story = `mswForState('default');`; // declares loading+error but story only has default
    const files = {
      'f.fidelity.json': fidelityOf(src, ['default', 'loading', 'error'], 'story.tsx'),
      'story.tsx': story,
    };
    const idx = buildFidelityIndex(Object.keys(files), (rel) => files[rel]);
    const r = classifyMockupEntry(entry, idx, makeIo(files));
    expect(r.verdict).toBe('contract-violation');
    expect(r.missing.sort()).toEqual(['error', 'loading']);
  });

  it('ignores non-canonical declared states (offline) when comparing', () => {
    const story = `mswForState('default');`;
    const files = {
      'f.fidelity.json': fidelityOf(src, ['default', 'offline'], 'story.tsx'),
      'story.tsx': story,
    };
    const idx = buildFidelityIndex(Object.keys(files), (rel) => files[rel]);
    const r = classifyMockupEntry(entry, idx, makeIo(files));
    expect(r.verdict).toBe('covered'); // offline dropped, only default required & present
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run scripts/__tests__/lint-storybook-states.test.ts`
Expected: FAIL — `buildFidelityIndex`/`classifyMockupEntry` are not exported.

- [ ] **Step 3: Implement the two functions**

Append to `apps/web/scripts/lint-storybook-states.mjs` (after `detectStates`):

```javascript
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run scripts/__tests__/lint-storybook-states.test.ts`
Expected: PASS (all Task 1 + Task 2 blocks green).

- [ ] **Step 5: Commit**

```bash
cd "D:/Repositories/meepleai-monorepo-frontend"
git add apps/web/scripts/lint-storybook-states.mjs apps/web/scripts/__tests__/lint-storybook-states.test.ts
git commit -m "feat(scripts): storybook-states fidelity index + entry classify (#2342)"
```

---

## Task 3: Scan, report, CLI (`scanEntries`, `buildJsonReport`, `buildMdReport`, `parseArgs`, `main`) + npm script

**Files:**
- Modify: `apps/web/scripts/lint-storybook-states.mjs`
- Modify: `apps/web/package.json`
- Test: `apps/web/scripts/__tests__/lint-storybook-states.test.ts`

**Interfaces:**
- Consumes: `buildFidelityIndex`, `classifyMockupEntry`, `parseMockupsIndex`.
- Produces:
  - `scanEntries(indexMd: string, fidelityIndex, io): entry-verdict[]` — pure: parse index → classify each entry.
  - `buildJsonReport(results, baseline): object` — `{ generatedFrom, canonicalStates, totalMappableEntries, baselineMaxCoverageGaps, counts, coverageGaps[], contractViolations[] }`.
  - `buildMdReport(report): string`.
  - `parseArgs(argv): { strict, maxBaseline, verbose, help }`.
  - `main(): Promise<void>` — glob real files, write reports, exit per rules.

- [ ] **Step 1: Write the failing test**

Append to `apps/web/scripts/__tests__/lint-storybook-states.test.ts`:

```typescript
import { scanEntries, buildJsonReport, parseArgs } from '../lint-storybook-states.mjs';

const INDEX_MD = [
  '| File | Type | Mapped routes |',
  '| --- | --- | --- |',
  '| `x.html` | page-mock | `/x` |',
  '| `y.html` | page-mock | `/y` |',
  '| `comp.html` | component-mock | used globally |',
].join('\n');

describe('scanEntries', () => {
  it('classifies each page-mock entry and ignores component-mock rows', () => {
    const files: Record<string, string> = {
      'fx.fidelity.json': JSON.stringify({
        mockup: { source: 'admin-mockups/design_files/x.html', states: ['default', 'loading'] },
        acceptance: { states_covered: ['default', 'loading'], story_path: 'x.tsx', design_intent: 'current' },
      }),
      'x.tsx': `mswForState('default'); mswForState('loading');`,
    };
    const io = {
      exists: (rel: string) => rel in files,
      readFile: (rel: string) => files[rel],
    };
    const idx = buildFidelityIndex(Object.keys(files), (rel) => files[rel]);
    const results = scanEntries(INDEX_MD, idx, io);
    expect(results).toHaveLength(2); // x + y, comp excluded
    const x = results.find((r) => r.mockup === 'x.html');
    const y = results.find((r) => r.mockup === 'y.html');
    expect(x!.verdict).toBe('covered');
    expect(y!.verdict).toBe('coverage-gap'); // no fidelity for y
  });
});

describe('buildJsonReport', () => {
  it('computes counts and preserves the total invariant', () => {
    const results = [
      { mockup: 'a', routes: [], verdict: 'covered' },
      { mockup: 'b', routes: [], verdict: 'coverage-gap', reason: 'no-fidelity' },
      { mockup: 'c', routes: [], verdict: 'contract-violation', missing: ['error'] },
      { mockup: 'd', routes: [], verdict: 'skipped-obsolete' },
    ];
    const report = buildJsonReport(results, 5);
    expect(report.totalMappableEntries).toBe(4);
    expect(report.counts).toEqual({
      covered: 1,
      coverageGaps: 1,
      contractViolations: 1,
      skippedObsolete: 1,
    });
    expect(report.baselineMaxCoverageGaps).toBe(5);
    expect(report.coverageGaps).toHaveLength(1);
    expect(report.contractViolations).toHaveLength(1);
  });
});

describe('parseArgs', () => {
  it('parses strict + max-baseline', () => {
    expect(parseArgs(['--strict', '--max-baseline', '61'])).toMatchObject({ strict: true, maxBaseline: 61 });
  });
  it('supports --max-baseline=N and --verbose/-v', () => {
    expect(parseArgs(['--max-baseline=3', '-v'])).toMatchObject({ maxBaseline: 3, verbose: true });
  });
  it('throws on unknown arg and on negative baseline', () => {
    expect(() => parseArgs(['--nope'])).toThrow();
    expect(() => parseArgs(['--max-baseline', '-1'])).toThrow();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run scripts/__tests__/lint-storybook-states.test.ts`
Expected: FAIL — `scanEntries`/`buildJsonReport`/`parseArgs` not exported.

- [ ] **Step 3: Implement scan + report + CLI**

Append to `apps/web/scripts/lint-storybook-states.mjs`:

```javascript
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

  const fidelityFiles = globSync('**/*.fidelity.json', {
    cwd: REPO_ROOT,
    ignore: ['**/node_modules/**', '**/.next/**', '**/.claude/**', '**/dist/**', '**/coverage/**'],
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run scripts/__tests__/lint-storybook-states.test.ts`
Expected: PASS (all blocks green).

- [ ] **Step 5: Add the npm script**

In `apps/web/package.json`, add to `"scripts"` (next to `lint:mockup-state-naming`):

```json
"lint:storybook-states": "node scripts/lint-storybook-states.mjs",
```

- [ ] **Step 6: Commit**

```bash
cd "D:/Repositories/meepleai-monorepo-frontend"
git add apps/web/scripts/lint-storybook-states.mjs apps/web/scripts/__tests__/lint-storybook-states.test.ts apps/web/package.json
git commit -m "feat(scripts): storybook-states scan+report+CLI, add npm script (#2342)"
```

---

## Task 4: First inventory run + burn contract-violations to zero + freeze baseline

**Files:**
- Create: `audits/2026-07-14-storybook-states-coverage.{json,md}` (generated)
- Modify (as needed): story files with non-heuristic state patterns (e.g. `apps/web/src/components/features/gamebook/TranslateViewer.stories.tsx`) — add `parameters.canonicalStates`.

**Interfaces:**
- Consumes: the finished script (Tasks 1-3).

- [ ] **Step 1: Run inventory and read the real counts**

Run: `cd apps/web && pnpm lint:storybook-states`
Expected: writes `audits/2026-07-14-storybook-states-coverage.{json,md}` and prints a summary line. Note `contract=<N>` and `gaps=<M>`.

- [ ] **Step 2: Inspect every contract-violation**

Run: `cd apps/web && pnpm lint:storybook-states --verbose` and open `audits/2026-07-14-storybook-states-coverage.md`. For each contract-violation, open its `storyPath` and decide:
- **False positive** (story DOES cover the state but via a non-`mswForState` pattern, e.g. `TranslateViewer` `_initialPhase`) → go to Step 3a.
- **True gap** (story genuinely lacks the state) → go to Step 3b.

- [ ] **Step 3a: Add explicit override to false-positive stories**

In the story's `meta.parameters`, add the canonical states it truly covers. Example for `TranslateViewer.stories.tsx` (declared `['default','loading','sse']`):

```typescript
const meta: Meta<typeof TranslateViewer> = {
  // ...existing meta...
  parameters: {
    // ...existing parameters...
    canonicalStates: ['default', 'loading', 'sse'],
  },
};
```

Re-run `pnpm lint:storybook-states --verbose` and confirm that story is no longer a contract-violation.

- [ ] **Step 3b: Fix true gaps (align states_covered)**

If a story genuinely does not implement a declared state and adding it is out of scope now, edit that mockup's `fidelity.json`: remove the un-implemented state from BOTH `mockup.states` and `acceptance.states_covered` (they must stay set-equal for `lint:fidelity`). Re-run `pnpm lint:fidelity` to confirm the fidelity still validates, then re-run `pnpm lint:storybook-states --verbose`.

- [ ] **Step 4: Repeat Steps 2-3 until contract == 0**

Run: `cd apps/web && pnpm lint:storybook-states`
Expected: summary line shows `contract=0`. Record the `gaps=<M>` number — this is the baseline `N`.

- [ ] **Step 5: Verify the strict gate is green at the chosen baseline**

Run: `cd apps/web && pnpm lint:storybook-states --strict --max-baseline <M>` (use the exact `gaps` count from Step 4).
Expected: exit 0, no FAIL lines. Confirm with `echo $?` → `0`.

- [ ] **Step 6: Commit the baseline report + any bonifica edits**

```bash
cd "D:/Repositories/meepleai-monorepo-frontend"
git add audits/2026-07-14-storybook-states-coverage.json audits/2026-07-14-storybook-states-coverage.md
# also add any story/fidelity files edited in Steps 3a/3b, e.g.:
# git add apps/web/src/components/features/gamebook/TranslateViewer.stories.tsx
git commit -m "chore(scripts): freeze storybook-states baseline, zero contract-violations (#2342)"
```

---

## Task 5: CI wiring + `#2342` reconciliation

**Files:**
- Modify: `.github/workflows/ci.yml` (job `frontend-lint`)

**Interfaces:**
- Consumes: the `lint:storybook-states` npm script + frozen baseline `N` (Task 4).

- [ ] **Step 1: Add the blocking gate step + artifact upload**

In `.github/workflows/ci.yml`, inside the `frontend-lint` job, after the `Mockup annotation coverage gate (DS-17-1)` step, insert (replace `<N>` with the baseline from Task 4 Step 4):

```yaml
    # DEC-A5 (umbrella #2342): blocking gate — each page-mock story must
    # implement the canonical states its fidelity.json declares.
    # coverage-gaps whitelist-incremental (ratchet down); contract-violations always fail.
    - name: Storybook canonical-states coverage gate (DEC-A5 / #2342)
      run: pnpm lint:storybook-states --strict --max-baseline <N>

    - name: Upload storybook-states report
      if: always()
      uses: actions/upload-artifact@v7
      with:
        name: storybook-states-${{ github.run_number }}
        path: |
          audits/2026-07-14-storybook-states-coverage.json
          audits/2026-07-14-storybook-states-coverage.md
        retention-days: 14
```

- [ ] **Step 2: Sanity-check the workflow YAML**

Run: `cd "D:/Repositories/meepleai-monorepo-frontend" && python -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml')); print('yaml ok')"`
Expected: `yaml ok`.

- [ ] **Step 3: Commit CI wiring**

```bash
cd "D:/Repositories/meepleai-monorepo-frontend"
git add .github/workflows/ci.yml
git commit -m "ci(frontend): add blocking lint:storybook-states gate (DEC-A5 #2342)"
```

- [ ] **Step 4: Push branch and open PR to `main-dev`**

```bash
cd "D:/Repositories/meepleai-monorepo-frontend"
git push -u origin feature/issue-2342-lint-storybook-states
gh pr create --base main-dev --head feature/issue-2342-lint-storybook-states \
  --title "feat(scripts): lint:storybook-states canonical-state coverage gate (DEC-A5 #2342)" \
  --body "Implements the DEC-A5 deliverable of umbrella #2342: the never-built \`lint:storybook-states\` CI gate. Verifies each page-mock story implements the canonical states its fidelity.json declares. See docs/superpowers/specs/2026-07-14-lint-storybook-states-design.md.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

- [ ] **Step 5: Post reconciliation comment on #2342**

After the PR is open, post a comment on the umbrella reconciling the stale body (Tier 3 now CLOSED, DEC-A5 gate shipped):

```bash
gh issue comment 2342 --repo meepleAi-app/meepleai-monorepo --body "## 🔧 DEC-A5 gate shipped + Tier 3 reconciliation (2026-07-14)

**DEC-A5 deliverable built**: the never-existing \`lint:storybook-states\` CI gate is implemented (PR above). It walks page-mock → fidelity.json → story and fails on: a NEW coverage-gap above baseline, or ANY contract-violation (a story omitting a canonical state its \`states_covered\` declares). \`states_covered\` remains the N/A waiver — no new waiver mechanism.

**Tier 3 reconciliation**: the umbrella body still shows Tier 3 as \"cascade just opened\", but #2697 + all six gap-issues (#2698–#2703) are now CLOSED. Current tier state: Tier 0-3 ✅ CLOSED · Tier 4 🟡 (epic #2354 open) · Tier 5/6 🚧 unopened.

🤖 \`/sc:spec-panel\` → brainstorming → writing-plans, 2026-07-14"
```

---

## Self-Review

- **Spec coverage**: catena route→mockup→fidelity→story (Task 2/3) ✅ · two violation classes (Task 2 classify, Task 3 report/exit) ✅ · inventory+strict modes (Task 3) ✅ · hybrid detection + override (Task 1) ✅ · mappable via `parseMockupsIndex` (Task 3) ✅ · misura→bonifica→blocking rollout (Task 4) ✅ · CI blocking + artifact (Task 5) ✅ · `#2342` reconciliation (Task 5) ✅ · TranslateViewer override (Task 4 Step 3a) ✅.
- **Placeholder scan**: the only intentional placeholder is baseline `<N>`/`<M>`, resolved by the real inventory in Task 4 before use in Task 5 — not a plan gap.
- **Type consistency**: `verdict` strings (`covered`/`coverage-gap`/`contract-violation`/`skipped-obsolete`), `counts` keys (`covered`/`coverageGaps`/`contractViolations`/`skippedObsolete`), and function names (`normalizeState`/`detectStates`/`buildFidelityIndex`/`classifyMockupEntry`/`scanEntries`/`buildJsonReport`/`buildMdReport`/`parseArgs`) are used identically across tasks and tests.
