/**
 * WS-D Foundation: parser estrae stati canonici dichiarati nei commenti
 * HTML dei mockup (`admin-mockups/design_files/*.html`).
 *
 * Spec: docs/for-developers/specs/2026-05-12-ws-d-state-coverage-design.md
 * Issue: #1070 (umbrella #1066)
 */

import { JSDOM } from 'jsdom';
import { readdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

export interface MockupStateEntry {
  mockup_path: string;
  route: string | null;
  declared_states: string[];
  covered_states: string[];
  missing: string[];
  enforced: boolean;
}

/**
 * Extract state names from a single comment text block.
 * Supports two declaration styles:
 *  - Inline: `Stati:  default · loading · error · not-found`
 *  - Multi-line: `Stati:` on first line, indented `<name> (description)` lines after
 *
 * Returns empty array if no `Stati:` line found.
 */
export function extractStatesFromComment(commentText: string): string[] {
  // Match "Stati:" + everything until next field header or end of comment.
  // Use `[ \t]*` (inline whitespace only) instead of `\s*` to PRESERVE the
  // newline + indentation of the first multi-line state declaration, so the
  // indent-depth heuristic below can distinguish state lines from
  // description continuations.
  const match = commentText.match(
    /Stati:[ \t]*([\s\S]+?)(?=\n\s*(?:Route|Persona|Scope|Mockup|Vincoli|Sorgente|Phase|CSS|Note)[:\s]|$)/
  );
  if (!match) return [];

  const block = match[1];
  // First non-blank line drives inline-vs-multiline detection
  const firstLine =
    block
      .split('\n')
      .find(l => l.trim().length > 0)
      ?.trim() ?? '';

  // Inline form: contains `·` (middot) or `|` separator on first line.
  // `,` is intentionally excluded — descriptions on multi-line declarations
  // often contain commas (e.g. `default (foo, bar)`) which would defeat
  // detection. `·` is the canonical separator in admin-mockups/.
  if (/[·|]/.test(firstLine)) {
    return firstLine
      .split(/[·|]/)
      .map(s => s.trim())
      .filter(Boolean);
  }

  // Multi-line form: state declarations have the *minimum* indentation among
  // non-blank lines in the block; description continuations are indented more
  // deeply (and often start with `(`). Distinguish by indent depth.
  const nonBlankLines = block.split('\n').filter(l => l.trim().length > 0);
  if (nonBlankLines.length === 0) return [];

  const indents = nonBlankLines.map(l => (l.match(/^(\s*)/)?.[1] ?? '').length);
  const minIndent = Math.min(...indents);

  return nonBlankLines
    .filter((_, i) => indents[i] === minIndent)
    .map(l => l.trim().split(/\s+/)[0])
    .filter(token => /^[a-zA-Z][\w-]*$/.test(token));
}

/**
 * Extract `Route:` value from comment text.
 * Returns null if not declared.
 */
export function extractRouteFromComment(commentText: string): string | null {
  const match = commentText.match(/Route:\s*(\S+)/);
  return match ? match[1] : null;
}

/**
 * Walk DOM, collect all COMMENT_NODE text concatenated with newline separators.
 * jsdom: comment nodes have nodeType === 8 (Node.COMMENT_NODE).
 */
function collectAllCommentsText(html: string): string {
  const dom = new JSDOM(html);
  const doc = dom.window.document;
  const walker = doc.createNodeIterator(doc, dom.window.NodeFilter.SHOW_COMMENT);
  const parts: string[] = [];
  let node: Node | null;
  while ((node = walker.nextNode())) {
    parts.push(node.nodeValue ?? '');
  }
  return parts.join('\n');
}

export interface StateMatrix {
  generated_at: string;
  total_mockups: number;
  enforced_count: number;
  entries: MockupStateEntry[];
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

/**
 * Merge fresh-parsed entries into an existing matrix, preserving manually-curated
 * fields (`covered_states`, `enforced`). Filters covered_states down to those
 * still in declared_states (drops obsolete coverage).
 *
 * Output sorted alphabetically by `mockup_path` for deterministic diffs.
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

export type CliMode = 'check' | 'write' | 'enforced-only';

export interface CliOptions {
  mode: CliMode;
  mockupsDir: string;
  matrixPath: string;
  /** Path prefix written into matrix entries (e.g. 'admin-mockups/design_files'). */
  mockupsPathPrefix: string;
}

/**
 * Glob mockup HTML files in `opts.mockupsDir` and parse each into a MockupStateEntry.
 */
function collectAllMockupEntries(opts: CliOptions): MockupStateEntry[] {
  const files = readdirSync(opts.mockupsDir)
    .filter(f => f.endsWith('.html'))
    .sort();
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
 * Stringify matrix omitting meta keys (`generated_at`, `$schema`) for diff
 * comparison — avoids spurious diffs from timestamp bumps or schema link.
 */
function stringifyForDiff(matrix: StateMatrix & { $schema?: string }): string {
  const { generated_at: _gen, $schema: _s, ...rest } = matrix;
  void _gen;
  void _s;
  return JSON.stringify({ ...rest, generated_at: '' }, null, 2);
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
      if (stringifyForDiff(existing) === stringifyForDiff(merged)) return 0;
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

// ─── CLI entry point ───────────────────────────────────────────────────────────
// Runs only when invoked directly via `pnpm tsx scripts/extract-mockup-states.ts`
const invokedDirectly =
  typeof process !== 'undefined' &&
  typeof process.argv[1] === 'string' &&
  process.argv[1].endsWith('extract-mockup-states.ts');

if (invokedDirectly) {
  const args = process.argv.slice(2);
  let mode: CliMode = 'check';
  if (args.includes('--write')) mode = 'write';
  else if (args.includes('--enforced-only')) mode = 'enforced-only';
  else if (args.includes('--check')) mode = 'check';

  // Resolve repo root from script location: apps/web/scripts/ → ../../..
  const scriptDir = new URL('.', import.meta.url).pathname;
  // On Windows, pathname has leading slash that needs trimming: /D:/...
  const normalizedScriptDir =
    process.platform === 'win32' && /^\/[A-Z]:/.test(scriptDir) ? scriptDir.slice(1) : scriptDir;
  const repoRoot = join(normalizedScriptDir, '..', '..', '..');

  runCli({
    mode,
    mockupsDir: join(repoRoot, 'admin-mockups', 'design_files'),
    matrixPath: join(repoRoot, 'apps', 'web', 'e2e', 'state-coverage', 'state-matrix.json'),
    mockupsPathPrefix: 'admin-mockups/design_files',
  }).then(code => process.exit(code));
}
