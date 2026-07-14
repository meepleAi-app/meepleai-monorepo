/**
 * lint-storybook-states.test.ts — unit tests for lint-storybook-states.mjs
 * (DEC-A5 canonical-state coverage gate, umbrella #2342)
 *
 * Run: pnpm vitest run scripts/__tests__/lint-storybook-states.test.ts
 */
import { describe, it, expect } from 'vitest';
import { CANONICAL_STATES, normalizeState, detectStates } from '../lint-storybook-states.mjs';
import { buildFidelityIndex, classifyMockupEntry } from '../lint-storybook-states.mjs';
import {
  scanEntries,
  buildJsonReport,
  buildMdReport,
  parseArgs,
} from '../lint-storybook-states.mjs';

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
  it('does not detect non-canonical state literals (offline, quota-*)', () => {
    const src = `mswForState('offline'); mswForState('quota-soft'); mswForState('default');`;
    expect(detectStates(src)).toEqual(new Set(['default']));
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
    const idx = buildFidelityIndex(Object.keys(files), rel => files[rel]);
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
    const idx = buildFidelityIndex(Object.keys(files), rel => files[rel]);
    const r = classifyMockupEntry(entry, idx, makeIo(files));
    expect(r.verdict).toBe('coverage-gap');
    expect(r.reason).toBe('no-story-path');
  });

  it('skipped-obsolete: design_intent forward-refactor-obsolete', () => {
    const files = {
      'f.fidelity.json': fidelityOf(src, ['default'], 'story.tsx', 'forward-refactor-obsolete'),
    };
    const idx = buildFidelityIndex(Object.keys(files), rel => files[rel]);
    const r = classifyMockupEntry(entry, idx, makeIo({ ...files, 'story.tsx': '' }));
    expect(r.verdict).toBe('skipped-obsolete');
  });

  it('covered: story implements every declared canonical state', () => {
    const story = `mswForState('default'); mswForState('loading'); mswForState('error');`;
    const files = {
      'f.fidelity.json': fidelityOf(src, ['default', 'loading', 'error'], 'story.tsx'),
      'story.tsx': story,
    };
    const idx = buildFidelityIndex(Object.keys(files), rel => files[rel]);
    const r = classifyMockupEntry(entry, idx, makeIo(files));
    expect(r.verdict).toBe('covered');
  });

  it('contract-violation: story omits a declared state, listing the missing ones', () => {
    const story = `mswForState('default');`; // declares loading+error but story only has default
    const files = {
      'f.fidelity.json': fidelityOf(src, ['default', 'loading', 'error'], 'story.tsx'),
      'story.tsx': story,
    };
    const idx = buildFidelityIndex(Object.keys(files), rel => files[rel]);
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
    const idx = buildFidelityIndex(Object.keys(files), rel => files[rel]);
    const r = classifyMockupEntry(entry, idx, makeIo(files));
    expect(r.verdict).toBe('covered'); // offline dropped, only default required & present
  });
});

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
        acceptance: {
          states_covered: ['default', 'loading'],
          story_path: 'x.tsx',
          design_intent: 'current',
        },
      }),
      'x.tsx': `mswForState('default'); mswForState('loading');`,
    };
    const io = {
      exists: (rel: string) => rel in files,
      readFile: (rel: string) => files[rel],
    };
    const idx = buildFidelityIndex(Object.keys(files), rel => files[rel]);
    const results = scanEntries(INDEX_MD, idx, io);
    expect(results).toHaveLength(2); // x + y, comp excluded
    const x = results.find(r => r.mockup === 'x.html');
    const y = results.find(r => r.mockup === 'y.html');
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
    expect(parseArgs(['--strict', '--max-baseline', '61'])).toMatchObject({
      strict: true,
      maxBaseline: 61,
    });
  });
  it('supports --max-baseline=N and --verbose/-v', () => {
    expect(parseArgs(['--max-baseline=3', '-v'])).toMatchObject({ maxBaseline: 3, verbose: true });
  });
  it('throws on unknown arg and on negative baseline', () => {
    expect(() => parseArgs(['--nope'])).toThrow();
    expect(() => parseArgs(['--max-baseline', '-1'])).toThrow();
  });
  it('throws a clear error when --max-baseline has no value', () => {
    expect(() => parseArgs(['--max-baseline'])).toThrow(/value/i);
  });
});

describe('buildMdReport', () => {
  it('renders the metric table, coverage-gap rows and gate-semantics section', () => {
    const report = buildJsonReport(
      [
        { mockup: 'a.html', routes: ['/a'], verdict: 'covered' },
        { mockup: 'b.html', routes: ['/b'], verdict: 'coverage-gap', reason: 'no-fidelity' },
      ],
      5
    );
    const md = buildMdReport(report);
    expect(md).toContain('# Storybook canonical-state coverage');
    expect(md).toContain('| Total page-mock entries | 2 |');
    expect(md).toContain('| Covered | 1 |');
    expect(md).toContain('| Coverage gaps (baseline 5) | 1 |');
    expect(md).toContain('## Coverage gaps');
    expect(md).toContain('no-fidelity');
    expect(md).toContain('## Gate semantics');
    expect(md.endsWith('\n')).toBe(true);
  });
  it('includes a contract-violations section only when there are violations', () => {
    const clean = buildMdReport(
      buildJsonReport([{ mockup: 'a.html', routes: [], verdict: 'covered' }], 0)
    );
    expect(clean).not.toContain('## Contract violations');
    const withViol = buildMdReport(
      buildJsonReport(
        [
          {
            mockup: 'c.html',
            routes: ['/c'],
            verdict: 'contract-violation',
            storyPath: 's.tsx',
            declared: ['error'],
            detected: [],
            missing: ['error'],
          },
        ],
        0
      )
    );
    expect(withViol).toContain('## Contract violations (must be 0)');
    expect(withViol).toContain('**error**');
  });
});
