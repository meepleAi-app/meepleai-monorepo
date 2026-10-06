/**
 * Issue #4084 — `mapRuleSpecsToSections` unit tests.
 *
 * This is the mapping that replaced the hardcoded `sections={[]}` literal in
 * GameDetailView. The behavior that matters here: (a) only the LATEST spec is used
 * (backend already orders by createdAt descending), (b) atoms group by `section`,
 * (c) atoms with no section (true for every atom the current PDF generator produces —
 * see #4084) fall into one bucket titled with the caller's fallback, not silently
 * dropped or crashed on.
 */
import { describe, expect, it } from 'vitest';

import type { RuleSpec } from '@/lib/api/schemas';

import { mapRuleSpecsToSections } from '../useGameRules';

function ruleSpec(overrides: Partial<RuleSpec> = {}): RuleSpec {
  return {
    id: 'spec-1',
    gameId: 'game-1',
    version: 'v1',
    createdAt: '2026-01-01T00:00:00.000Z',
    createdByUserId: null,
    parentVersionId: null,
    atoms: [],
    ...overrides,
  };
}

describe('mapRuleSpecsToSections', () => {
  it('returns an empty array when there are no rule specs', () => {
    expect(mapRuleSpecsToSections(undefined, 'Regole')).toEqual([]);
    expect(mapRuleSpecsToSections([], 'Regole')).toEqual([]);
  });

  it('returns an empty array when the latest spec has zero atoms', () => {
    const specs = [ruleSpec({ atoms: [] })];
    expect(mapRuleSpecsToSections(specs, 'Regole')).toEqual([]);
  });

  it('uses only the first (latest) spec, ignoring older ones', () => {
    const specs = [
      ruleSpec({
        id: 'latest',
        atoms: [{ id: 'a1', text: 'Testo nuovo', section: null, page: null, line: null }],
      }),
      ruleSpec({
        id: 'older',
        atoms: [{ id: 'a2', text: 'Testo vecchio', section: null, page: null, line: null }],
      }),
    ];
    const result = mapRuleSpecsToSections(specs, 'Regole');
    expect(result).toHaveLength(1);
    expect(result[0]!.summary).toBe('Testo nuovo');
    expect(result[0]!.summary).not.toContain('vecchio');
  });

  it('groups atoms by section when section is present', () => {
    const specs = [
      ruleSpec({
        atoms: [
          { id: 'a1', text: 'Setup 1', section: 'Setup', page: null, line: null },
          { id: 'a2', text: 'Setup 2', section: 'Setup', page: null, line: null },
          { id: 'a3', text: 'Turno 1', section: 'Turno', page: null, line: null },
        ],
      }),
    ];
    const result = mapRuleSpecsToSections(specs, 'Regole');
    const titles = result.map(s => s.title).sort();
    expect(titles).toEqual(['Setup', 'Turno']);
    expect(result.find(s => s.title === 'Setup')!.summary).toBe('Setup 1\nSetup 2');
  });

  // #4084 — the case that matters TODAY: the real generator leaves `section` null for
  // every atom. Without this fallback, every atom would need its own ungrouped bucket
  // (an accordion with one entry per sentence) or silently vanish from a naive filter.
  it('falls back to one bucket titled with the caller label when section is null for every atom', () => {
    const specs = [
      ruleSpec({
        atoms: [
          { id: 'a1', text: 'Prima frase.', section: null, page: null, line: null },
          { id: 'a2', text: 'Seconda frase.', section: null, page: null, line: null },
        ],
      }),
    ];
    const result = mapRuleSpecsToSections(specs, 'Regole principali');
    expect(result).toHaveLength(1);
    expect(result[0]!.title).toBe('Regole principali');
    expect(result[0]!.summary).toBe('Prima frase.\nSeconda frase.');
  });

  it('treats a blank-string section the same as null (falls back)', () => {
    const specs = [
      ruleSpec({ atoms: [{ id: 'a1', text: 'X', section: '   ', page: null, line: null }] }),
    ];
    const result = mapRuleSpecsToSections(specs, 'Regole');
    expect(result).toEqual([{ id: 'spec-1-0', title: 'Regole', summary: 'X' }]);
  });
});
