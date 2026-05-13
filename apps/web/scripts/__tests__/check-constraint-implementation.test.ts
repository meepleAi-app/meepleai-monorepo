/**
 * Tests for WS-E.2a constraint implementation check (refs #1071, AC-E.5+E.2).
 */
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  PATTERN_MAP,
  evaluateConstraint,
  computeDedupKey,
  scanCodebase,
  type Constraint,
  type ImplementationCheck,
} from '../check-constraint-implementation';

function constraint(overrides: Partial<Constraint> = {}): Constraint {
  return {
    mockup: 'test.html',
    id: null,
    text: 'test text',
    metric_type: 'latency',
    target_value: null,
    applies_to: ['test.html'],
    ...overrides,
  };
}

describe('PATTERN_MAP', () => {
  it('defines pattern for each non-fallback metric_type', () => {
    expect(PATTERN_MAP.latency).toBeDefined();
    expect(PATTERN_MAP.performance).toBeDefined();
    expect(PATTERN_MAP.citation).toBeDefined();
    expect(PATTERN_MAP.confidence).toBeDefined();
    expect(PATTERN_MAP.coverage).toBeDefined();
    expect(PATTERN_MAP.accessibility).toBeDefined();
  });

  it('marks ux-constraint and other as manual-review-only', () => {
    expect(PATTERN_MAP['ux-constraint'].manualOnly).toBe(true);
    expect(PATTERN_MAP.other.manualOnly).toBe(true);
  });
});

describe('computeDedupKey', () => {
  it('is deterministic for same constraint identity', () => {
    const a = constraint({ mockup: 'm.html', id: 'G4.2', text: 'x', metric_type: 'latency' });
    const b = constraint({ mockup: 'm.html', id: 'G4.2', text: 'x', metric_type: 'latency' });
    expect(computeDedupKey(a)).toBe(computeDedupKey(b));
  });

  it('changes when constraint text changes (intentional re-eval)', () => {
    const a = constraint({ text: 'before' });
    const b = constraint({ text: 'after' });
    expect(computeDedupKey(a)).not.toBe(computeDedupKey(b));
  });

  it('uses id when present (id takes precedence over text)', () => {
    const a = constraint({ id: 'G4.2', text: 'text-A' });
    const b = constraint({ id: 'G4.2', text: 'text-B' });
    expect(computeDedupKey(a)).toBe(computeDedupKey(b));
  });

  it('returns 16-char hex slice', () => {
    const key = computeDedupKey(constraint());
    expect(key).toMatch(/^[a-f0-9]{16}$/);
  });
});

describe('evaluateConstraint', () => {
  let tmp: string;

  beforeEach(() => {
    tmp = mkdtempSync(join(tmpdir(), 'ws-e2a-'));
  });

  afterEach(() => {
    rmSync(tmp, { recursive: true, force: true });
  });

  function write(relPath: string, content: string): void {
    const full = join(tmp, relPath);
    mkdirSync(join(full, '..'), { recursive: true });
    writeFileSync(full, content);
  }

  it('returns tier=verified when @spec-ref <id> present', () => {
    const c = constraint({ id: 'G4.2', metric_type: 'latency' });
    write('apps/web/src/foo.ts', '// @spec-ref G4.2\nconst x = 1;');
    const result = evaluateConstraint(c, scanCodebase(tmp));
    expect(result.tier).toBe('verified');
    expect(result.evidence.some(e => e.kind === 'spec-ref')).toBe(true);
  });

  it('returns tier=likely-implemented when grep pattern matches but no @spec-ref', () => {
    const c = constraint({ metric_type: 'citation' });
    write('apps/web/src/components/Foo.tsx', '<CitationChip page={1} />');
    const result = evaluateConstraint(c, scanCodebase(tmp));
    expect(result.tier).toBe('likely-implemented');
    expect(result.evidence.length).toBeGreaterThan(0);
  });

  it('returns tier=unknown when no pattern matches', () => {
    const c = constraint({ metric_type: 'citation' });
    write('apps/web/src/components/Foo.tsx', 'export const Foo = () => <div />;');
    const result = evaluateConstraint(c, scanCodebase(tmp));
    expect(result.tier).toBe('unknown');
    expect(result.evidence).toEqual([]);
  });

  it('returns tier=manual-review for ux-constraint metric_type', () => {
    const c = constraint({ metric_type: 'ux-constraint' });
    write('apps/web/src/components/Foo.tsx', '<div data-citation="x" />');
    const result = evaluateConstraint(c, scanCodebase(tmp));
    expect(result.tier).toBe('manual-review');
  });

  it('returns tier=manual-review for other metric_type', () => {
    const c = constraint({ metric_type: 'other' });
    const result = evaluateConstraint(c, scanCodebase(tmp));
    expect(result.tier).toBe('manual-review');
  });

  it('respects path scope (citation looked up only in apps/web/src/**)', () => {
    const c = constraint({ metric_type: 'citation' });
    // Match in OUT-of-scope path → should be ignored.
    write('docs/something.md', '<CitationChip />');
    const result = evaluateConstraint(c, scanCodebase(tmp));
    expect(result.tier).toBe('unknown');
  });

  it('records evidence file paths relative to root', () => {
    const c = constraint({ metric_type: 'accessibility' });
    write('apps/web/__tests__/a11y.spec.ts', 'await axe.run(page);');
    const result = evaluateConstraint(c, scanCodebase(tmp));
    expect(result.tier).toBe('likely-implemented');
    expect(result.evidence[0].file).toBe('apps/web/__tests__/a11y.spec.ts');
  });

  it('@spec-ref wins over grep pattern (verified beats likely)', () => {
    const c = constraint({ id: 'G4.2', metric_type: 'citation' });
    write('apps/web/src/A.tsx', '<CitationChip />'); // pattern match
    write('apps/web/src/B.ts', '// @spec-ref G4.2'); // explicit ref
    const result = evaluateConstraint(c, scanCodebase(tmp));
    expect(result.tier).toBe('verified');
  });
});

describe('full report shape', () => {
  it('returns ImplementationCheck with all canonical fields', () => {
    const c = constraint();
    const result: ImplementationCheck = evaluateConstraint(c, {
      filesByScope: new Map(),
    });
    expect(Object.keys(result).sort()).toEqual(
      ['constraint', 'dedup_key', 'evaluated_at', 'evidence', 'tier'].sort()
    );
  });
});
