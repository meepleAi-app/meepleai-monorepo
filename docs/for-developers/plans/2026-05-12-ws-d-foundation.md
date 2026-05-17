# WS-D Foundation — Mockup State Coverage Matrix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Foundation deliverables for WS-D: HTML mockup parser, `state-matrix.json` inventory, `state-override.ts` helper, CI gate workflow, adoption doc.

**Architecture:** Standalone TypeScript script (`extract-mockup-states.ts`) using jsdom walks `admin-mockups/design_files/*.html` and emits deterministic `state-matrix.json`. CI workflow runs the script in `--check` mode on PR. React helper `state-override.ts` (three-layer API) provides typed `?state=` URL override gated by `IS_VISUAL_TEST_BUILD` env flag.

**Tech Stack:** TypeScript, jsdom ^29.1.1, tsx ^4.21.0, Vitest, GitHub Actions, Next.js 16 `next/navigation`.

**Spec:** [`docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md`](../specs/2026-05-12-ws-d-state-coverage-design.md)
**Branch:** `feature/issue-1070-mockup-conformity-states`
**Umbrella:** #1066 · **Tracking:** #1070

---

## Task 1: Parser core — single-line `Stati:` extraction (TDD)

**Files:**
- Create: `apps/web/scripts/extract-mockup-states.ts`
- Create: `apps/web/scripts/__tests__/extract-mockup-states.test.ts`

- [ ] **Step 1.1: Write the failing test (single-line · separator)**

```ts
// apps/web/scripts/__tests__/extract-mockup-states.test.ts
import { describe, it, expect } from 'vitest';
import { extractStatesFromComment } from '../extract-mockup-states';

describe('extractStatesFromComment', () => {
  it('parses single-line Stati with · separator', () => {
    const comment = `
      Mockup: nanolith-runthrough-game-detail
      Route:  /library/{gameId}
      Stati:  default · loading · error · not-found
      Persona: Aaron`;
    expect(extractStatesFromComment(comment)).toEqual([
      'default',
      'loading',
      'error',
      'not-found',
    ]);
  });
});
```

- [ ] **Step 1.2: Run test, verify FAIL**

Run from `apps/web/`:
```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: FAIL with "Cannot find module '../extract-mockup-states'" or "is not a function".

- [ ] **Step 1.3: Write minimal implementation**

```ts
// apps/web/scripts/extract-mockup-states.ts
/**
 * WS-D Foundation: parser estrae stati canonici dichiarati nei commenti
 * HTML dei mockup (`admin-mockups/design_files/*.html`).
 *
 * Spec: docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md
 * Issue: #1070 (umbrella #1066)
 */

/**
 * Extract state names from a single comment text block.
 * Supports two declaration styles:
 *  - Inline: `Stati:  default · loading · error · not-found`
 *  - Multi-line: `Stati:` on first line, indented `<name> (description)` lines after
 *
 * Returns empty array if no `Stati:` line found.
 */
export function extractStatesFromComment(commentText: string): string[] {
  // Match "Stati:" + everything until next field header or end of comment
  const match = commentText.match(/Stati:\s*([\s\S]+?)(?=\n\s*(?:Route|Persona|Scope|Mockup|Vincoli|Sorgente|Phase|CSS|Note)[:\s]|$)/);
  if (!match) return [];

  const block = match[1];
  const firstLine = block.split('\n')[0].trim();

  // Inline form: contains a known separator on first line
  if (/[·|,]/.test(firstLine)) {
    return firstLine
      .split(/[·|,]/)
      .map(s => s.trim())
      .filter(Boolean);
  }

  // Multi-line form: each non-blank line's first token is a state name
  return block
    .split('\n')
    .map(l => l.trim())
    .filter(Boolean)
    .map(line => line.split(/\s+/)[0])
    .filter(Boolean);
}
```

- [ ] **Step 1.4: Run test, verify PASS**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: 1 test passed.

- [ ] **Step 1.5: Commit**

```bash
git add apps/web/scripts/extract-mockup-states.ts apps/web/scripts/__tests__/extract-mockup-states.test.ts
git commit -m "feat(state-coverage): parse single-line Stati declarations (refs #1070)"
```

---

## Task 2: Parser — multi-line `Stati:` extraction (TDD)

**Files:**
- Modify: `apps/web/scripts/__tests__/extract-mockup-states.test.ts`
- (Implementation already supports multi-line from Task 1; this task verifies)

- [ ] **Step 2.1: Add failing test for multi-line declaration**

Append to `extract-mockup-states.test.ts`:

```ts
  it('parses multi-line Stati with indented continuation', () => {
    const comment = `
      Mockup: sp4-game-chat-tab
      Stati:
        default            (Aaron query "azione carta uccello + 2 cibo" su Wingspan,
                           risposta IT + citation Manuale p.12 + confidence alta verde)
        confidence-bassa   (edge "mazzo finisce a metà partita", disclaimer
                           "non sono certo" + suggerimento BGG)
        out-of-context     (utente chiede regole di Tainted Grail mentre è su Wingspan,
                           agente declina + propone switch gioco/agente)
        loading            (typing indicator + meta latency live, budget P95 ≤ 10s da Q6)
      Persona: Aaron`;
    expect(extractStatesFromComment(comment)).toEqual([
      'default',
      'confidence-bassa',
      'out-of-context',
      'loading',
    ]);
  });

  it('returns empty array when no Stati line present', () => {
    const comment = `Mockup: foo\nRoute: /bar`;
    expect(extractStatesFromComment(comment)).toEqual([]);
  });
```

- [ ] **Step 2.2: Run tests, verify all pass**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: 3 tests passed (multi-line parsing works via the fallback branch added in Task 1).

If multi-line fails: the first non-blank line after `Stati:` is empty (continuation lines have descriptions). Fix in `extract-mockup-states.ts`:
- Filter out lines that don't start with a word character (skips description continuation lines that begin with parenthesis or are pure whitespace).

```ts
  return block
    .split('\n')
    .map(l => l.trim())
    .filter(Boolean)
    .filter(line => /^[a-zA-Z]/.test(line)) // skip description continuations starting with `(`
    .map(line => line.split(/\s+/)[0])
    .filter(Boolean);
```

- [ ] **Step 2.3: Re-run tests, verify PASS**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: 3 tests passed.

- [ ] **Step 2.4: Commit**

```bash
git add apps/web/scripts/extract-mockup-states.ts apps/web/scripts/__tests__/extract-mockup-states.test.ts
git commit -m "feat(state-coverage): parse multi-line Stati declarations (refs #1070)"
```

---

## Task 3: Parser — extract `Route:` from comment (TDD)

**Files:**
- Modify: `apps/web/scripts/extract-mockup-states.ts`
- Modify: `apps/web/scripts/__tests__/extract-mockup-states.test.ts`

- [ ] **Step 3.1: Add failing test**

Append:

```ts
import { extractRouteFromComment } from '../extract-mockup-states';

describe('extractRouteFromComment', () => {
  it('extracts Route: from comment', () => {
    const comment = `
      Mockup: foo
      Route:  /library/{gameId}
      Stati: default`;
    expect(extractRouteFromComment(comment)).toBe('/library/{gameId}');
  });

  it('returns null when no Route declared', () => {
    const comment = `Mockup: foo\nStati: default`;
    expect(extractRouteFromComment(comment)).toBeNull();
  });
});
```

- [ ] **Step 3.2: Run, verify FAIL**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: FAIL "extractRouteFromComment is not a function".

- [ ] **Step 3.3: Implement**

Append to `extract-mockup-states.ts`:

```ts
/**
 * Extract `Route:` value from comment text.
 * Returns null if not declared.
 */
export function extractRouteFromComment(commentText: string): string | null {
  const match = commentText.match(/Route:\s*(\S+)/);
  return match ? match[1] : null;
}
```

- [ ] **Step 3.4: Run, verify PASS**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: 5 tests passed.

- [ ] **Step 3.5: Commit**

```bash
git add apps/web/scripts/extract-mockup-states.ts apps/web/scripts/__tests__/extract-mockup-states.test.ts
git commit -m "feat(state-coverage): parse Route metadata from mockup comments (refs #1070)"
```

---

## Task 4: Parser — process single HTML file via jsdom (TDD)

**Files:**
- Modify: `apps/web/scripts/extract-mockup-states.ts`
- Modify: `apps/web/scripts/__tests__/extract-mockup-states.test.ts`

- [ ] **Step 4.1: Add failing test for full file parse**

Append:

```ts
import { parseMockupFile } from '../extract-mockup-states';

describe('parseMockupFile', () => {
  it('extracts MockupStateEntry from HTML string', () => {
    const html = `<!doctype html>
<html>
<head><title>test</title></head>
<!--
  Mockup: test-mockup
  Route:  /test/{id}
  Stati:  default · loading · error
  Persona: Aaron
-->
<body></body>
</html>`;
    const entry = parseMockupFile('admin-mockups/design_files/test-mockup.html', html);
    expect(entry).toEqual({
      mockup_path: 'admin-mockups/design_files/test-mockup.html',
      route: '/test/{id}',
      declared_states: ['default', 'loading', 'error'],
      covered_states: [],
      missing: ['default', 'loading', 'error'],
      enforced: false,
    });
  });

  it('returns entry with empty declared_states when no Stati line', () => {
    const html = `<!--\n  Mockup: foo\n-->`;
    const entry = parseMockupFile('admin-mockups/design_files/foo.html', html);
    expect(entry.declared_states).toEqual([]);
    expect(entry.missing).toEqual([]);
  });
});
```

- [ ] **Step 4.2: Run, verify FAIL**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: FAIL "parseMockupFile is not a function".

- [ ] **Step 4.3: Implement**

Add type + function to `extract-mockup-states.ts`:

```ts
import { JSDOM } from 'jsdom';

export interface MockupStateEntry {
  mockup_path: string;
  route: string | null;
  declared_states: string[];
  covered_states: string[];
  missing: string[];
  enforced: boolean;
}

/**
 * Walk DOM, collect all COMMENT_NODE text concatenated.
 * jsdom: comment nodes have nodeType === 8 (Node.COMMENT_NODE).
 */
function collectAllCommentsText(html: string): string {
  const dom = new JSDOM(html);
  const doc = dom.window.document;
  const COMMENT_NODE = 8;
  const walker = doc.createNodeIterator(doc, dom.window.NodeFilter.SHOW_COMMENT);
  const parts: string[] = [];
  let node: Node | null;
  while ((node = walker.nextNode())) {
    if (node.nodeType === COMMENT_NODE) {
      parts.push(node.nodeValue ?? '');
    }
  }
  return parts.join('\n');
}

/**
 * Parse a single mockup HTML file into a MockupStateEntry.
 * Preserves declared_states in source order; covered_states starts empty,
 * enforced starts false (bootstrap-then-enforce per spec §2).
 */
export function parseMockupFile(mockupPath: string, html: string): MockupStateEntry {
  const commentsText = collectAllCommentsText(html);
  const declared = extractStatesFromComment(commentsText);
  const route = extractRouteFromComment(commentsText);
  return {
    mockup_path: mockupPath,
    route,
    declared_states: declared,
    covered_states: [],
    missing: declared.slice(), // declared - covered (all declared at bootstrap)
    enforced: false,
  };
}
```

- [ ] **Step 4.4: Run, verify PASS**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: 7 tests passed.

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/scripts/extract-mockup-states.ts apps/web/scripts/__tests__/extract-mockup-states.test.ts
git commit -m "feat(state-coverage): parse single mockup file via jsdom (refs #1070)"
```

---

## Task 5: Matrix builder — merge with existing matrix.json preserving curated fields (TDD)

**Files:**
- Modify: `apps/web/scripts/extract-mockup-states.ts`
- Modify: `apps/web/scripts/__tests__/extract-mockup-states.test.ts`

- [ ] **Step 5.1: Add failing test**

Append:

```ts
import { mergeMatrix } from '../extract-mockup-states';
import type { StateMatrix } from '../extract-mockup-states';

describe('mergeMatrix', () => {
  it('preserves covered_states and enforced from existing matrix', () => {
    const existing: StateMatrix = {
      generated_at: '2026-05-01T00:00:00.000Z',
      total_mockups: 1,
      enforced_count: 1,
      entries: [
        {
          mockup_path: 'admin-mockups/design_files/foo.html',
          route: '/foo',
          declared_states: ['a', 'b'],
          covered_states: ['a'],
          missing: ['b'],
          enforced: true,
        },
      ],
    };
    const fresh = [
      {
        mockup_path: 'admin-mockups/design_files/foo.html',
        route: '/foo',
        declared_states: ['a', 'b', 'c'], // new state added
        covered_states: [],
        missing: ['a', 'b', 'c'],
        enforced: false,
      },
    ];
    const merged = mergeMatrix(existing, fresh);
    expect(merged.entries[0].covered_states).toEqual(['a']);
    expect(merged.entries[0].enforced).toBe(true);
    expect(merged.entries[0].declared_states).toEqual(['a', 'b', 'c']);
    expect(merged.entries[0].missing).toEqual(['b', 'c']); // declared - covered
  });

  it('drops covered states no longer in declared', () => {
    const existing: StateMatrix = {
      generated_at: '2026-05-01T00:00:00.000Z',
      total_mockups: 1,
      enforced_count: 0,
      entries: [
        {
          mockup_path: 'admin-mockups/design_files/foo.html',
          route: '/foo',
          declared_states: ['a', 'b'],
          covered_states: ['a', 'b', 'obsolete'],
          missing: [],
          enforced: false,
        },
      ],
    };
    const fresh = [
      {
        mockup_path: 'admin-mockups/design_files/foo.html',
        route: '/foo',
        declared_states: ['a'], // 'b' removed by mockup edit
        covered_states: [],
        missing: ['a'],
        enforced: false,
      },
    ];
    const merged = mergeMatrix(existing, fresh);
    expect(merged.entries[0].covered_states).toEqual(['a']);
  });

  it('sorts entries alphabetically by mockup_path', () => {
    const existing: StateMatrix = {
      generated_at: '2026-05-01T00:00:00.000Z',
      total_mockups: 0,
      enforced_count: 0,
      entries: [],
    };
    const fresh = [
      { mockup_path: 'admin-mockups/design_files/zeta.html', route: null, declared_states: [], covered_states: [], missing: [], enforced: false },
      { mockup_path: 'admin-mockups/design_files/alpha.html', route: null, declared_states: [], covered_states: [], missing: [], enforced: false },
    ];
    const merged = mergeMatrix(existing, fresh);
    expect(merged.entries.map(e => e.mockup_path)).toEqual([
      'admin-mockups/design_files/alpha.html',
      'admin-mockups/design_files/zeta.html',
    ]);
  });
});
```

- [ ] **Step 5.2: Run, verify FAIL**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: FAIL "mergeMatrix is not a function".

- [ ] **Step 5.3: Implement**

Append to `extract-mockup-states.ts`:

```ts
export interface StateMatrix {
  generated_at: string;
  total_mockups: number;
  enforced_count: number;
  entries: MockupStateEntry[];
}

/**
 * Merge fresh-parsed entries into existing matrix, preserving manually-curated
 * fields (`covered_states`, `enforced`). Filters covered_states down to those
 * still in declared_states (drops obsolete coverage).
 *
 * Output sorted alphabetically by mockup_path for deterministic diffs.
 */
export function mergeMatrix(existing: StateMatrix, fresh: MockupStateEntry[]): StateMatrix {
  const byPath = new Map<string, MockupStateEntry>();
  for (const e of existing.entries) {
    byPath.set(e.mockup_path, e);
  }

  const merged: MockupStateEntry[] = fresh.map(freshEntry => {
    const prior = byPath.get(freshEntry.mockup_path);
    if (!prior) return freshEntry;
    const declared = freshEntry.declared_states;
    const covered = prior.covered_states.filter(s => declared.includes(s));
    const missing = declared.filter(s => !covered.includes(s));
    return {
      ...freshEntry,
      covered_states: covered,
      missing,
      enforced: prior.enforced,
    };
  });

  merged.sort((a, b) => a.mockup_path.localeCompare(b.mockup_path));

  return {
    generated_at: new Date().toISOString(),
    total_mockups: merged.length,
    enforced_count: merged.filter(e => e.enforced).length,
    entries: merged,
  };
}
```

- [ ] **Step 5.4: Run, verify PASS**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: 10 tests passed.

- [ ] **Step 5.5: Commit**

```bash
git add apps/web/scripts/extract-mockup-states.ts apps/web/scripts/__tests__/extract-mockup-states.test.ts
git commit -m "feat(state-coverage): merge fresh entries preserving curated fields (refs #1070)"
```

---

## Task 6: CLI — `--check`, `--write`, `--enforced-only` modes (TDD)

**Files:**
- Modify: `apps/web/scripts/extract-mockup-states.ts`
- Modify: `apps/web/scripts/__tests__/extract-mockup-states.test.ts`

- [ ] **Step 6.1: Add failing tests for CLI orchestrator**

Append:

```ts
import { runCli } from '../extract-mockup-states';
import { mkdtempSync, writeFileSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

describe('runCli', () => {
  let tmp: string;
  let mockupsDir: string;
  let matrixPath: string;

  beforeEach(() => {
    tmp = mkdtempSync(join(tmpdir(), 'state-coverage-test-'));
    mockupsDir = join(tmp, 'admin-mockups', 'design_files');
    require('node:fs').mkdirSync(mockupsDir, { recursive: true });
    matrixPath = join(tmp, 'matrix.json');
    writeFileSync(
      join(mockupsDir, 'foo.html'),
      `<!--\n  Mockup: foo\n  Stati: default · loading\n-->`,
    );
  });

  afterEach(() => {
    rmSync(tmp, { recursive: true, force: true });
  });

  it('--write creates matrix.json with parsed entries', async () => {
    const exitCode = await runCli({
      mode: 'write',
      mockupsDir,
      matrixPath,
      mockupsPathPrefix: 'admin-mockups/design_files',
    });
    expect(exitCode).toBe(0);
    const matrix = JSON.parse(readFileSync(matrixPath, 'utf-8'));
    expect(matrix.total_mockups).toBe(1);
    expect(matrix.entries[0].declared_states).toEqual(['default', 'loading']);
  });

  it('--check exits 0 when matrix.json in sync', async () => {
    await runCli({ mode: 'write', mockupsDir, matrixPath, mockupsPathPrefix: 'admin-mockups/design_files' });
    const exitCode = await runCli({ mode: 'check', mockupsDir, matrixPath, mockupsPathPrefix: 'admin-mockups/design_files' });
    expect(exitCode).toBe(0);
  });

  it('--check exits 1 when mockup changed but matrix.json stale', async () => {
    await runCli({ mode: 'write', mockupsDir, matrixPath, mockupsPathPrefix: 'admin-mockups/design_files' });
    // mutate mockup
    writeFileSync(
      join(mockupsDir, 'foo.html'),
      `<!--\n  Mockup: foo\n  Stati: default · loading · error\n-->`,
    );
    const exitCode = await runCli({ mode: 'check', mockupsDir, matrixPath, mockupsPathPrefix: 'admin-mockups/design_files' });
    expect(exitCode).toBe(1);
  });

  it('--enforced-only exits 0 when no enforced entries have missing', async () => {
    await runCli({ mode: 'write', mockupsDir, matrixPath, mockupsPathPrefix: 'admin-mockups/design_files' });
    const exitCode = await runCli({ mode: 'enforced-only', mockupsDir, matrixPath, mockupsPathPrefix: 'admin-mockups/design_files' });
    expect(exitCode).toBe(0); // bootstrap: all enforced=false
  });

  it('--enforced-only exits 1 when enforced entry has missing states', async () => {
    await runCli({ mode: 'write', mockupsDir, matrixPath, mockupsPathPrefix: 'admin-mockups/design_files' });
    // manually flip enforced=true
    const matrix = JSON.parse(readFileSync(matrixPath, 'utf-8'));
    matrix.entries[0].enforced = true;
    matrix.enforced_count = 1;
    writeFileSync(matrixPath, JSON.stringify(matrix, null, 2));
    const exitCode = await runCli({ mode: 'enforced-only', mockupsDir, matrixPath, mockupsPathPrefix: 'admin-mockups/design_files' });
    expect(exitCode).toBe(1); // covered=[] but enforced=true → missing.length > 0
  });
});
```

Add `beforeEach` + `afterEach` imports at top of test file:

```ts
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
```

- [ ] **Step 6.2: Run, verify FAIL**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: FAIL "runCli is not a function".

- [ ] **Step 6.3: Implement**

Append to `extract-mockup-states.ts`:

```ts
import { readdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { join, relative } from 'node:path';

export type CliMode = 'check' | 'write' | 'enforced-only';

export interface CliOptions {
  mode: CliMode;
  mockupsDir: string;
  matrixPath: string;
  /** Path prefix written into matrix entries (e.g. 'admin-mockups/design_files'). */
  mockupsPathPrefix: string;
}

/**
 * Glob mockup HTML files and parse each into a MockupStateEntry.
 */
function collectAllMockupEntries(opts: CliOptions): MockupStateEntry[] {
  const files = readdirSync(opts.mockupsDir).filter(f => f.endsWith('.html'));
  return files.map(filename => {
    const absPath = join(opts.mockupsDir, filename);
    const html = readFileSync(absPath, 'utf-8');
    const repoRelative = `${opts.mockupsPathPrefix}/${filename}`;
    return parseMockupFile(repoRelative, html);
  });
}

function loadExistingMatrix(matrixPath: string): StateMatrix {
  if (!existsSync(matrixPath)) {
    return { generated_at: '', total_mockups: 0, enforced_count: 0, entries: [] };
  }
  return JSON.parse(readFileSync(matrixPath, 'utf-8'));
}

/**
 * Stringify matrix omitting `generated_at` for diff comparison (ignore mtime noise).
 */
function stringifyForDiff(matrix: StateMatrix): string {
  const copy = { ...matrix, generated_at: '' };
  return JSON.stringify(copy, null, 2);
}

/**
 * CLI orchestrator. Returns exit code (0 OK, 1 fail).
 */
export async function runCli(opts: CliOptions): Promise<number> {
  const fresh = collectAllMockupEntries(opts);
  const existing = loadExistingMatrix(opts.matrixPath);
  const merged = mergeMatrix(existing, fresh);

  switch (opts.mode) {
    case 'write': {
      writeFileSync(opts.matrixPath, JSON.stringify(merged, null, 2) + '\n', 'utf-8');
      return 0;
    }
    case 'check': {
      const existingForDiff = stringifyForDiff(existing);
      const mergedForDiff = stringifyForDiff(merged);
      if (existingForDiff === mergedForDiff) return 0;
      console.error('state-matrix.json out of sync with admin-mockups/design_files.');
      console.error('Run: pnpm tsx apps/web/scripts/extract-mockup-states.ts --write');
      return 1;
    }
    case 'enforced-only': {
      const violations = merged.entries.filter(e => e.enforced && e.missing.length > 0);
      if (violations.length === 0) return 0;
      console.error(`Found ${violations.length} enforced entries with missing states:`);
      for (const v of violations) {
        console.error(`  ${v.mockup_path}: missing ${JSON.stringify(v.missing)}`);
      }
      return 1;
    }
  }
}

// CLI entry point — only runs when invoked directly via `pnpm tsx ...`
const isMain = import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith('extract-mockup-states.ts');

if (isMain) {
  const args = process.argv.slice(2);
  let mode: CliMode = 'check';
  if (args.includes('--write')) mode = 'write';
  else if (args.includes('--enforced-only')) mode = 'enforced-only';
  else if (args.includes('--check')) mode = 'check';

  const repoRoot = join(import.meta.dirname ?? '.', '..', '..', '..');
  runCli({
    mode,
    mockupsDir: join(repoRoot, 'admin-mockups', 'design_files'),
    matrixPath: join(repoRoot, 'apps', 'web', 'e2e', 'state-coverage', 'state-matrix.json'),
    mockupsPathPrefix: 'admin-mockups/design_files',
  }).then(code => process.exit(code));
}
```

- [ ] **Step 6.4: Run, verify PASS**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: 15 tests passed.

- [ ] **Step 6.5: Commit**

```bash
git add apps/web/scripts/extract-mockup-states.ts apps/web/scripts/__tests__/extract-mockup-states.test.ts
git commit -m "feat(state-coverage): CLI modes --check/--write/--enforced-only (refs #1070)"
```

---

## Task 7: Bootstrap `state-matrix.json` — run parser on real mockups

**Files:**
- Create: `apps/web/e2e/state-coverage/state-matrix.json`
- Create: `apps/web/e2e/state-coverage/.gitkeep` (only if no other file ensures dir exists)

- [ ] **Step 7.1: Run parser in `--write` mode**

From repo root:

```bash
cd apps/web
pnpm tsx scripts/extract-mockup-states.ts --write
```
Expected: no errors. File `apps/web/e2e/state-coverage/state-matrix.json` created (or updated) with ~60+ entries.

- [ ] **Step 7.2: Inspect bootstrap output**

```bash
head -40 e2e/state-coverage/state-matrix.json
```

Verify shape:
- `generated_at` is current ISO timestamp
- `total_mockups` matches `admin-mockups/design_files/*.html` count
- `enforced_count` is `0`
- `entries[]` sorted alphabetically by `mockup_path`
- At least one entry with route `/library/{gameId}` and `declared_states: ["default", "loading", "error", "not-found"]`

```bash
ls admin-mockups/design_files/*.html | wc -l
```
Expected: equal to `total_mockups` in matrix.json.

- [ ] **Step 7.3: Verify `--check` mode is green after bootstrap**

```bash
cd apps/web && pnpm tsx scripts/extract-mockup-states.ts --check
echo "Exit code: $?"
```
Expected: Exit code 0.

- [ ] **Step 7.4: Commit bootstrap matrix**

```bash
cd ../..
git add apps/web/e2e/state-coverage/state-matrix.json
git commit -m "chore(state-coverage): bootstrap state-matrix.json from current mockups (refs #1070)"
```

---

## Task 8: JSON Schema sidecar for IDE validation

**Files:**
- Create: `apps/web/e2e/state-coverage/state-matrix.schema.json`

- [ ] **Step 8.1: Write the schema file**

```json
{
  "$schema": "https://json-schema.org/draft-07/schema",
  "$id": "https://meepleai.app/schemas/state-matrix.schema.json",
  "title": "State Coverage Matrix",
  "description": "WS-D Mockup state coverage tracking. Issue #1070 (umbrella #1066).",
  "type": "object",
  "required": ["generated_at", "total_mockups", "enforced_count", "entries"],
  "properties": {
    "$schema": { "type": "string" },
    "generated_at": {
      "type": "string",
      "format": "date-time",
      "description": "ISO 8601 timestamp. Bumped only when content effectively differs."
    },
    "total_mockups": { "type": "integer", "minimum": 0 },
    "enforced_count": { "type": "integer", "minimum": 0 },
    "entries": {
      "type": "array",
      "items": { "$ref": "#/definitions/MockupStateEntry" }
    }
  },
  "definitions": {
    "MockupStateEntry": {
      "type": "object",
      "required": ["mockup_path", "route", "declared_states", "covered_states", "missing", "enforced"],
      "properties": {
        "mockup_path": {
          "type": "string",
          "pattern": "^admin-mockups/design_files/[\\w-]+\\.html$"
        },
        "route": {
          "anyOf": [{ "type": "string" }, { "type": "null" }]
        },
        "declared_states": {
          "type": "array",
          "items": { "type": "string", "minLength": 1 },
          "description": "States declared in mockup `Stati:` comment line. Order preserves author declaration order."
        },
        "covered_states": {
          "type": "array",
          "items": { "type": "string", "minLength": 1 },
          "description": "States covered by Playwright tests. Manually curated; preserved across regenerations."
        },
        "missing": {
          "type": "array",
          "items": { "type": "string", "minLength": 1 },
          "description": "declared_states - covered_states. Computed."
        },
        "enforced": {
          "type": "boolean",
          "description": "If true, CI gate fails on missing.length > 0. Bootstrap: all false."
        }
      }
    }
  }
}
```

- [ ] **Step 8.2: Add `$schema` link to matrix.json**

Edit `apps/web/e2e/state-coverage/state-matrix.json`, add as first key:

```json
{
  "$schema": "./state-matrix.schema.json",
  "generated_at": "...",
  ...
}
```

Then update the parser to preserve this on `--write`. Edit `extract-mockup-states.ts` `runCli` case `'write'`:

```ts
    case 'write': {
      const output = {
        $schema: './state-matrix.schema.json',
        ...merged,
      };
      writeFileSync(opts.matrixPath, JSON.stringify(output, null, 2) + '\n', 'utf-8');
      return 0;
    }
```

Also update `stringifyForDiff` to ignore `$schema` for diff:

```ts
function stringifyForDiff(matrix: StateMatrix): string {
  // Strip generated_at and $schema (meta keys, not content) before diff
  const { generated_at: _gen, $schema: _s, ...rest } = matrix as StateMatrix & { $schema?: string };
  return JSON.stringify({ ...rest, generated_at: '' }, null, 2);
}
```

- [ ] **Step 8.3: Verify `--check` still green after schema addition**

```bash
cd apps/web && pnpm tsx scripts/extract-mockup-states.ts --write
pnpm tsx scripts/extract-mockup-states.ts --check
echo "Exit code: $?"
```
Expected: Exit code 0.

- [ ] **Step 8.4: Run unit tests**

```bash
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts
```
Expected: 15 tests still passing.

- [ ] **Step 8.5: Commit**

```bash
cd ../..
git add apps/web/e2e/state-coverage/state-matrix.schema.json apps/web/e2e/state-coverage/state-matrix.json apps/web/scripts/extract-mockup-states.ts
git commit -m "feat(state-coverage): JSON Schema sidecar for IDE validation (refs #1070)"
```

---

## Task 9: `state-override.ts` helper — `readStateOverride` (TDD)

**Files:**
- Create: `apps/web/src/lib/visual-test/state-override.ts`
- Create: `apps/web/src/lib/visual-test/__tests__/state-override.test.ts`

- [ ] **Step 9.1: Write the failing test**

```ts
// apps/web/src/lib/visual-test/__tests__/state-override.test.ts
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

describe('readStateOverride', () => {
  const ORIGINAL_ENV = process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;

  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    if (ORIGINAL_ENV === undefined) delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    else process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = ORIGINAL_ENV;
  });

  it('returns null when IS_VISUAL_TEST_BUILD=false (production)', async () => {
    delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    const { readStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=loading');
    expect(readStateOverride(params)).toBeNull();
  });

  it('returns ?state value when IS_VISUAL_TEST_BUILD=true', async () => {
    process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = '1';
    const { readStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=loading');
    expect(readStateOverride(params)).toBe('loading');
  });

  it('returns null when searchParams is null', async () => {
    process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = '1';
    const { readStateOverride } = await import('../state-override');
    expect(readStateOverride(null)).toBeNull();
  });

  it('returns null when searchParams lacks state key', async () => {
    process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = '1';
    const { readStateOverride } = await import('../state-override');
    const params = new URLSearchParams('other=foo');
    expect(readStateOverride(params)).toBeNull();
  });
});
```

- [ ] **Step 9.2: Run, verify FAIL**

```bash
pnpm vitest run src/lib/visual-test/__tests__/state-override.test.ts
```
Expected: FAIL "Cannot find module '../state-override'".

- [ ] **Step 9.3: Implement minimal**

```ts
// apps/web/src/lib/visual-test/state-override.ts
/**
 * WS-D Foundation: shared `?state=` URL override helper for visual regression tests.
 *
 * Gated by NEXT_PUBLIC_VISUAL_TEST_BUILD env flag — production builds eliminate
 * all bodies via terser dead-code-elimination (bundle delta: 0 KB).
 *
 * Spec: docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md
 * Issue: #1070 (umbrella #1066)
 */

/** Compile-time flag set by Next.js build via env var. */
export const IS_VISUAL_TEST_BUILD: boolean =
  process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD === '1';

/**
 * Read `?state=` URL param. Returns null in production regardless of URL contents.
 */
export function readStateOverride(searchParams: URLSearchParams | null): string | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (!searchParams) return null;
  return searchParams.get('state');
}
```

- [ ] **Step 9.4: Run, verify PASS**

```bash
pnpm vitest run src/lib/visual-test/__tests__/state-override.test.ts
```
Expected: 4 tests passed.

- [ ] **Step 9.5: Commit**

```bash
git add apps/web/src/lib/visual-test/state-override.ts apps/web/src/lib/visual-test/__tests__/state-override.test.ts
git commit -m "feat(visual-test): readStateOverride helper with IS_VISUAL_TEST_BUILD gate (refs #1070)"
```

---

## Task 10: `state-override.ts` — `readTypedStateOverride` (TDD)

**Files:**
- Modify: `apps/web/src/lib/visual-test/state-override.ts`
- Modify: `apps/web/src/lib/visual-test/__tests__/state-override.test.ts`

- [ ] **Step 10.1: Add failing tests**

Append to test file:

```ts
describe('readTypedStateOverride', () => {
  const ORIGINAL_ENV = process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;

  beforeEach(() => {
    vi.resetModules();
    process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = '1';
  });

  afterEach(() => {
    if (ORIGINAL_ENV === undefined) delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    else process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = ORIGINAL_ENV;
  });

  it('returns typed value when state in allowedStates', async () => {
    const { readTypedStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=loading');
    const result = readTypedStateOverride(params, ['default', 'loading', 'error'] as const);
    expect(result).toBe('loading');
  });

  it('returns null when state value not in allowedStates', async () => {
    const { readTypedStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=bogus');
    const result = readTypedStateOverride(params, ['default', 'loading'] as const);
    expect(result).toBeNull();
  });

  it('returns null when allowedStates is empty', async () => {
    const { readTypedStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=loading');
    const result = readTypedStateOverride(params, []);
    expect(result).toBeNull();
  });

  it('returns null when IS_VISUAL_TEST_BUILD=false even with valid state', async () => {
    delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    const { readTypedStateOverride } = await import('../state-override');
    const params = new URLSearchParams('state=loading');
    const result = readTypedStateOverride(params, ['default', 'loading'] as const);
    expect(result).toBeNull();
  });
});
```

- [ ] **Step 10.2: Run, verify FAIL**

```bash
pnpm vitest run src/lib/visual-test/__tests__/state-override.test.ts
```
Expected: FAIL "readTypedStateOverride is not a function".

- [ ] **Step 10.3: Implement**

Append to `state-override.ts`:

```ts
/**
 * Type-safe variant. Validates the raw value is one of `allowedStates`.
 * Returns null when:
 *  - IS_VISUAL_TEST_BUILD=false
 *  - searchParams is null
 *  - state param missing
 *  - state value not in allowedStates
 */
export function readTypedStateOverride<S extends string>(
  searchParams: URLSearchParams | null,
  allowedStates: readonly S[]
): S | null {
  const raw = readStateOverride(searchParams);
  if (raw === null) return null;
  if (!allowedStates.includes(raw as S)) return null;
  return raw as S;
}
```

- [ ] **Step 10.4: Run, verify PASS**

```bash
pnpm vitest run src/lib/visual-test/__tests__/state-override.test.ts
```
Expected: 8 tests passed.

- [ ] **Step 10.5: Commit**

```bash
git add apps/web/src/lib/visual-test/state-override.ts apps/web/src/lib/visual-test/__tests__/state-override.test.ts
git commit -m "feat(visual-test): readTypedStateOverride type-safe variant (refs #1070)"
```

---

## Task 11: `state-override.ts` — `useStateOverride` React hook (TDD)

**Files:**
- Modify: `apps/web/src/lib/visual-test/state-override.ts`
- Modify: `apps/web/src/lib/visual-test/__tests__/state-override.test.ts`

- [ ] **Step 11.1: Add failing test**

Append to test file:

```ts
import { renderHook } from '@testing-library/react';

describe('useStateOverride', () => {
  const ORIGINAL_ENV = process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;

  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    if (ORIGINAL_ENV === undefined) delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    else process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = ORIGINAL_ENV;
  });

  it('returns null in production regardless of route params', async () => {
    delete process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD;
    vi.doMock('next/navigation', () => ({
      useSearchParams: () => new URLSearchParams('state=loading'),
    }));
    const { useStateOverride } = await import('../state-override');
    const { result } = renderHook(() => useStateOverride(['default', 'loading'] as const));
    expect(result.current).toBeNull();
  });

  it('returns typed value in visual-test mode', async () => {
    process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD = '1';
    vi.doMock('next/navigation', () => ({
      useSearchParams: () => new URLSearchParams('state=loading'),
    }));
    const { useStateOverride } = await import('../state-override');
    const { result } = renderHook(() => useStateOverride(['default', 'loading'] as const));
    expect(result.current).toBe('loading');
  });
});
```

- [ ] **Step 11.2: Run, verify FAIL**

```bash
pnpm vitest run src/lib/visual-test/__tests__/state-override.test.ts
```
Expected: FAIL "useStateOverride is not a function".

- [ ] **Step 11.3: Implement**

Append to `state-override.ts`:

```ts
/**
 * React hook variant. Reads `?state=` from Next.js `useSearchParams()`.
 *
 * In production (IS_VISUAL_TEST_BUILD=false), this is a no-op that always
 * returns null and never imports next/navigation at runtime — dead-code
 * elimination strips the body entirely.
 *
 * @example
 *   const STATES = ['default', 'loading', 'error'] as const;
 *   const state = useStateOverride(STATES);  // 'default' | 'loading' | 'error' | null
 */
export function useStateOverride<S extends string>(
  allowedStates: readonly S[]
): S | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  // Dynamic require pattern: next/navigation only loaded in test builds
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const { useSearchParams } = require('next/navigation');
  const params = useSearchParams() as URLSearchParams | null;
  return readTypedStateOverride(params, allowedStates);
}
```

- [ ] **Step 11.4: Run, verify PASS**

```bash
pnpm vitest run src/lib/visual-test/__tests__/state-override.test.ts
```
Expected: 10 tests passed.

- [ ] **Step 11.5: Commit**

```bash
git add apps/web/src/lib/visual-test/state-override.ts apps/web/src/lib/visual-test/__tests__/state-override.test.ts
git commit -m "feat(visual-test): useStateOverride React hook (refs #1070)"
```

---

## Task 12: CI workflow `state-coverage-check.yml`

**Files:**
- Create: `.github/workflows/state-coverage-check.yml`

- [ ] **Step 12.1: Write the workflow file**

```yaml
name: State Coverage Check

# Issue #1070 (WS-D Mockup Conformity Roadmap, umbrella #1066)
# Verifies state-matrix.json is in sync with admin-mockups/design_files/*.html
# and enforces missing.length === 0 for mockups flagged `enforced: true`.

on:
  pull_request:
    branches: [main, main-dev, main-staging]
    paths:
      - 'admin-mockups/design_files/**'
      - 'apps/web/e2e/state-coverage/**'
      - 'apps/web/scripts/extract-mockup-states.ts'
      - 'apps/web/scripts/__tests__/extract-mockup-states.test.ts'
      - '.github/workflows/state-coverage-check.yml'
  workflow_dispatch:
    inputs:
      mode:
        description: 'check (diff fail) | regenerate (write matrix.json artifact)'
        required: true
        default: 'check'
        type: choice
        options:
          - check
          - regenerate

permissions:
  contents: read
  pull-requests: write

concurrency:
  group: state-coverage-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true

jobs:
  state-coverage:
    name: State Matrix Sync + Enforcement
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: apps/web
    steps:
      - name: Checkout
        uses: actions/checkout@v6

      - name: Setup Frontend
        uses: ./.github/actions/setup-frontend
        with:
          node-version: '20'
          pnpm-version: '10'
          working-directory: apps/web
          frozen-lockfile: 'true'

      - name: Run state matrix sync check
        if: github.event_name != 'workflow_dispatch' || inputs.mode == 'check'
        run: pnpm tsx scripts/extract-mockup-states.ts --check

      - name: Run enforcement check (enforced: true entries)
        if: github.event_name != 'workflow_dispatch' || inputs.mode == 'check'
        run: pnpm tsx scripts/extract-mockup-states.ts --enforced-only

      - name: Regenerate matrix.json (workflow_dispatch only)
        if: github.event_name == 'workflow_dispatch' && inputs.mode == 'regenerate'
        run: pnpm tsx scripts/extract-mockup-states.ts --write

      - name: Upload regenerated matrix
        if: github.event_name == 'workflow_dispatch' && inputs.mode == 'regenerate'
        uses: actions/upload-artifact@v7
        with:
          name: state-matrix-regenerated-${{ github.run_number }}
          path: apps/web/e2e/state-coverage/state-matrix.json
          retention-days: 14

      - name: Comment on PR (sync failure)
        if: failure() && github.event_name == 'pull_request'
        uses: actions/github-script@v8
        with:
          script: |
            const body = `## State Coverage Check failed

            \`state-matrix.json\` is out-of-sync with mockup HTML files in \`admin-mockups/design_files/\`.

            **To fix**:
            \`\`\`bash
            cd apps/web
            pnpm tsx scripts/extract-mockup-states.ts --write
            git add e2e/state-coverage/state-matrix.json
            git commit -m "chore(state-coverage): refresh matrix from mockup edits"
            \`\`\`

            See \`docs/for-developers/testing/frontend/visual-state-coverage.md\` for full workflow.
            `;
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body
            });
```

- [ ] **Step 12.2: Validate YAML locally**

```bash
cd ../..
# Quick syntax check via pnpm (any installed yaml linter), or eyeball it
cat .github/workflows/state-coverage-check.yml | head -10
```

- [ ] **Step 12.3: Commit**

```bash
git add .github/workflows/state-coverage-check.yml
git commit -m "feat(ci): state-coverage-check workflow (refs #1070)"
```

---

## Task 13: Documentation — adoption guide

**Files:**
- Create: `docs/for-developers/testing/frontend/visual-state-coverage.md`

- [ ] **Step 13.1: Write the doc**

```markdown
# Visual State Coverage

> **Issue:** #1070 (WS-D Mockup Conformity Roadmap, umbrella #1066)
> **Status:** Foundation shipped 2026-05-12

## Overview

Each mockup in `admin-mockups/design_files/*.html` declares canonical states in a header comment (e.g. `Stati: default · loading · error · not-found`). The Foundation tooling extracts those states into a machine-readable matrix and provides:

- `apps/web/scripts/extract-mockup-states.ts` — parser (jsdom)
- `apps/web/e2e/state-coverage/state-matrix.json` — inventory
- `apps/web/src/lib/visual-test/state-override.ts` — `?state=` URL override helper
- `.github/workflows/state-coverage-check.yml` — CI gate

## Workflow

### When a mockup is edited

1. Edit `admin-mockups/design_files/<mockup>.html`. Update `Stati:` line if states change.
2. Regenerate matrix locally:
   ```bash
   cd apps/web
   pnpm tsx scripts/extract-mockup-states.ts --write
   ```
3. Commit both the mockup change and the regenerated `state-matrix.json`:
   ```bash
   git add admin-mockups/design_files/<mockup>.html apps/web/e2e/state-coverage/state-matrix.json
   git commit -m "feat(mockup): update <feature> states"
   ```

If you forget step 2, CI will fail at the `state-coverage` step with a guidance comment on the PR.

### When a state gets test coverage

After implementing a Playwright test that exercises a specific declared state for a route:

1. Open `apps/web/e2e/state-coverage/state-matrix.json`.
2. Find the entry by `mockup_path`.
3. Add the state name to `covered_states`:
   ```json
   {
     "mockup_path": "admin-mockups/design_files/nanolith-runthrough-game-detail.html",
     "route": "/library/{gameId}",
     "declared_states": ["default", "loading", "error", "not-found"],
     "covered_states": ["loading"],   // ← added
     "missing": ["default", "error", "not-found"],   // ← recomputed
     "enforced": false
   }
   ```
4. Or run `pnpm tsx scripts/extract-mockup-states.ts --write` to let the parser recompute `missing` for you.

### When a route is ready for enforcement

Once all declared states have test coverage and the team wants to block regression:

1. Set `enforced: true` on the matrix entry.
2. Increment `enforced_count` accordingly.
3. CI `--enforced-only` check now blocks PRs that leave declared states uncovered.

## CLI reference

```bash
# Dry-run: exit 1 if matrix.json out-of-sync with mockup HTML
pnpm tsx scripts/extract-mockup-states.ts --check

# Regenerate matrix.json (preserves covered_states + enforced)
pnpm tsx scripts/extract-mockup-states.ts --write

# Validate enforced entries have no missing states
pnpm tsx scripts/extract-mockup-states.ts --enforced-only
```

## Using `state-override.ts` in a route component (PR2 Exemplar onward)

```tsx
'use client';
import { useStateOverride } from '@/lib/visual-test/state-override';

const LIBRARY_GAME_DETAIL_STATES = ['default', 'loading', 'error', 'not-found'] as const;

export default function LibraryGameDetailPage() {
  const stateOverride = useStateOverride(LIBRARY_GAME_DETAIL_STATES);
  // stateOverride is `null` in production (dead-code-eliminated body).
  // In `?state=loading` visual test mode, returns 'loading' for branching.

  if (stateOverride === 'loading') return <SkeletonView />;
  if (stateOverride === 'error') return <ErrorBoundary />;
  // ... real data fetch + render
}
```

**Production guarantee**: `NEXT_PUBLIC_VISUAL_TEST_BUILD=1` is set **only** by `playwright.config.ts:webServer.env`. Production `pnpm build` never sets the flag → terser drops the entire body → bundle delta 0 KB.

## Distinction vs other visual workflows

| Workflow | Scope |
|---|---|
| `visual-regression-mockups.yml` | Mockup HTML → baseline PNG (visual stability of mockups) |
| `visual-regression-migrated.yml` | Route live → baseline PNG (implementation stability) |
| **`state-coverage-check.yml`** | Mockup `Stati:` ↔ matrix.json consistency (declarative gate) |
| `visual-regression-conformity.yml` (WS-C future) | Route ↔ mockup pixel diff (visual gate) |

No overlap. The state coverage gate is **declarative** (state names match), not **visual** (pixel diff).

## Known limitations

- Mockups with hybrid `A · state-name` declaration format (e.g. `nanolith-runthrough-error-states.html`) may extract the prefix letter as a state name; manual cleanup of `state-matrix.json` after first run is acceptable.
- State proliferation cap: keep canonical states to 4-6 per route. Document timing-dependent states (SSE, real-time) as exceptions in PR description and cover via unit tests rather than visual.

## References

- Spec: [`docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md`](../specs/2026-05-12-ws-d-state-coverage-design.md)
- Roadmap: [`docs/for-developers/specs/2026-05-12-mockup-conformity-roadmap.md`](../specs/2026-05-12-mockup-conformity-roadmap.md) §3 WS-D
- Umbrella issue: #1066
- Tracking issue: #1070
```

- [ ] **Step 13.2: Commit**

```bash
git add docs/for-developers/testing/frontend/visual-state-coverage.md
git commit -m "docs(state-coverage): adoption workflow guide (refs #1070)"
```

---

## Task 14: Final smoke + push + open PR

**Files:** (verification only)

- [ ] **Step 14.1: Run full unit test suite for touched files**

```bash
cd apps/web
pnpm vitest run scripts/__tests__/extract-mockup-states.test.ts src/lib/visual-test/__tests__/state-override.test.ts
```
Expected: 25 tests passed across 2 files (15 parser + 10 state-override).

- [ ] **Step 14.2: Run typecheck (clean build)**

```bash
rm -rf .next .next-dev 2>&1 || true
pnpm typecheck
```
Expected: 0 errors.

- [ ] **Step 14.3: Run lint on touched files**

```bash
pnpm lint scripts/extract-mockup-states.ts scripts/__tests__/extract-mockup-states.test.ts src/lib/visual-test/state-override.ts src/lib/visual-test/__tests__/state-override.test.ts
```
Expected: 0 errors (warnings acceptable).

- [ ] **Step 14.4: Verify CLI behavior end-to-end**

```bash
pnpm tsx scripts/extract-mockup-states.ts --check
echo "Check exit: $?"
pnpm tsx scripts/extract-mockup-states.ts --enforced-only
echo "Enforced exit: $?"
```
Expected: both exit code 0 (matrix in sync, zero enforced entries to validate).

- [ ] **Step 14.5: Push branch**

```bash
cd ../..
git push -u origin feature/issue-1070-mockup-conformity-states
```

- [ ] **Step 14.6: Open PR via gh CLI**

```bash
gh pr create --base main-dev \
  --title "feat(state-coverage): WS-D Foundation — parser + matrix + helper + CI gate (refs #1070)" \
  --body "$(cat <<'EOF'
## Summary

WS-D Foundation deliverables (Mockup Conformity Roadmap umbrella #1066):

- Parser `apps/web/scripts/extract-mockup-states.ts` (jsdom) extracts canonical `Stati:` declarations from `admin-mockups/design_files/*.html` mockups
- Inventory `apps/web/e2e/state-coverage/state-matrix.json` with all current mockups bootstrapped at `enforced: false`
- Helper `apps/web/src/lib/visual-test/state-override.ts` (three-layer API: standalone, typed, React hook) gated by `IS_VISUAL_TEST_BUILD` env flag for zero production impact
- CI workflow `.github/workflows/state-coverage-check.yml` with two-stage check (sync + enforcement)
- Adoption doc `docs/for-developers/testing/frontend/visual-state-coverage.md`

## DoD status (per Issue #1070 ACs)

- [x] **AC-D.2** — generator output deterministic (parser preserves curated fields, sorted alphabetically, `generated_at` bumped only on content diff)
- [x] **AC-D.3** — fixture pattern uniformato via `state-override.ts` helper (consumo opzionale)
- [ ] **AC-D.1** — 100% declared states covered for enforced routes → **deferred to PR2 Exemplar** (post-merge #1074)
- [ ] **AC-D.4** — route exemplar `/library/[gameId]` cites `error ≠ not-found` → **deferred to PR2 Exemplar**

## Test plan

- [x] 15 parser unit tests (single-line, multi-line, route extraction, file parse, matrix merge, CLI modes)
- [x] 10 state-override unit tests (readStateOverride, readTypedStateOverride, useStateOverride)
- [x] `pnpm typecheck` clean (after `.next` rebuild)
- [x] `pnpm lint` clean on touched files
- [x] `pnpm tsx scripts/extract-mockup-states.ts --check` exits 0 (matrix bootstrap in sync)
- [x] `pnpm tsx scripts/extract-mockup-states.ts --enforced-only` exits 0 (zero enforced entries at bootstrap)
- [ ] CI `state-coverage-check.yml` green on PR (validates first end-to-end run)

## Refs

- Refs umbrella #1066
- Refs tracking #1070
- Spec: \`docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md\`
- Roadmap: \`docs/for-developers/specs/2026-05-12-mockup-conformity-roadmap.md\` §3 WS-D
- Companion: PR #1074 (WS-B Nanolith bugfix, coordination via Wait & rebase strategy)
- Future: PR2 Exemplar starts post-merge #1074

Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Expected: PR URL printed. Verify mergeStateStatus in subsequent `gh pr view`.

---

## Notes for the implementer

- All file paths in this plan are **repo-relative**, matching `git ls-files` output.
- Work in branch `feature/issue-1070-mockup-conformity-states` (already created and configured with parent `main-dev`).
- The branch already has commit `c2e821d2e` (design doc) — Tasks 1-14 add on top.
- After PR opens, monitor CI status via `gh pr checks 1076` (or whatever PR number is assigned).
- Do not bundle PR2 Exemplar changes (`/library/[gameId]/page.tsx` modifications) into this PR — that work is deliberately deferred per spec §2 to avoid file conflict with PR #1074.
