/**
 * Unit tests for `computeRankedParticipants` (Wave D.3 Phase 0.5 §12).
 *
 * Coverage targets (≥12 tests, 6 Gherkin scenarios + edge cases):
 *   - All distinct scores → no ties
 *   - 2-tied at 1st: ranks [1, 1, 3, 4]
 *   - 3-tied at 2nd: ranks [1, 2, 2, 2, 5]
 *   - All tied: ranks [1, 1, ...] all isTied=true
 *   - Tied at last place: ranks [..., 2, 2]
 *   - Empty array: []
 *   - Single participant: rank=1, isTied=false
 *   - Locale-aware secondary sort: accent-insensitive
 *   - Original input not mutated
 *   - tiedPlayerIds always includes self
 */

import { describe, expect, it } from 'vitest';

import type { ParticipantDto } from '@/lib/api/schemas/session-tracking.schemas';

import { computeRankedParticipants, type RankedParticipant } from '../tie-groups';

// ---------------------------------------------------------------------------
// Test factory
// ---------------------------------------------------------------------------

let nextId = 1;
function makeParticipant(displayName: string, totalScore: number): ParticipantDto {
  const id = nextId;
  nextId += 1;
  return {
    id: `00000000-0000-4000-8000-${id.toString(16).padStart(12, '0')}`,
    userId: null,
    displayName,
    isOwner: false,
    joinOrder: 1,
    finalRank: null,
    totalScore,
  };
}

// ---------------------------------------------------------------------------
// Gherkin scenario 1: all distinct
// ---------------------------------------------------------------------------

describe('computeRankedParticipants — all distinct scores', () => {
  it('assigns sequential ranks 1..N when all scores differ', () => {
    nextId = 1;
    const result = computeRankedParticipants([
      makeParticipant('Alice', 100),
      makeParticipant('Bob', 90),
      makeParticipant('Carol', 80),
    ]);
    expect(result.map(p => p.rank)).toEqual([1, 2, 3]);
  });

  it('marks no participant as tied when all scores differ', () => {
    nextId = 1;
    const result = computeRankedParticipants([
      makeParticipant('Alice', 100),
      makeParticipant('Bob', 90),
      makeParticipant('Carol', 80),
    ]);
    for (const p of result) {
      expect(p.isTied).toBe(false);
      expect(p.tiedPlayerIds).toHaveLength(1);
      expect(p.tiedPlayerIds[0]).toBe(p.id);
    }
  });
});

// ---------------------------------------------------------------------------
// Gherkin scenario 2: 2-tied at 1st place
// ---------------------------------------------------------------------------

describe('computeRankedParticipants — 2 tied at 1st', () => {
  it('ranks are [1, 1, 3] (skipping 2nd position)', () => {
    nextId = 1;
    const result = computeRankedParticipants([
      makeParticipant('Alice', 100),
      makeParticipant('Bob', 100),
      makeParticipant('Carol', 90),
    ]);
    expect(result.map(p => p.rank)).toEqual([1, 1, 3]);
  });

  it('Alice and Bob both isTied=true, Carol isTied=false', () => {
    nextId = 1;
    const alice = makeParticipant('Alice', 100);
    const bob = makeParticipant('Bob', 100);
    const carol = makeParticipant('Carol', 90);
    const result = computeRankedParticipants([alice, bob, carol]);
    const byName = new Map(result.map(p => [p.displayName, p]));
    expect(byName.get('Alice')?.isTied).toBe(true);
    expect(byName.get('Bob')?.isTied).toBe(true);
    expect(byName.get('Carol')?.isTied).toBe(false);
  });

  it('Alice and Bob share tiedPlayerIds = [Alice.id, Bob.id]', () => {
    nextId = 1;
    const alice = makeParticipant('Alice', 100);
    const bob = makeParticipant('Bob', 100);
    const carol = makeParticipant('Carol', 90);
    const result = computeRankedParticipants([alice, bob, carol]);
    const aliceR = result.find(p => p.displayName === 'Alice')!;
    const bobR = result.find(p => p.displayName === 'Bob')!;
    expect(new Set(aliceR.tiedPlayerIds)).toEqual(new Set([alice.id, bob.id]));
    expect(new Set(bobR.tiedPlayerIds)).toEqual(new Set([alice.id, bob.id]));
  });

  it('alphabetical tie-breaker: Alice listed before Bob', () => {
    nextId = 1;
    // Insert Bob first to ensure sort stability is from algorithm, not input order.
    const result = computeRankedParticipants([
      makeParticipant('Bob', 100),
      makeParticipant('Alice', 100),
      makeParticipant('Carol', 90),
    ]);
    expect(result[0].displayName).toBe('Alice');
    expect(result[1].displayName).toBe('Bob');
  });
});

// ---------------------------------------------------------------------------
// Gherkin scenario 3: 3-tied at 2nd place
// ---------------------------------------------------------------------------

describe('computeRankedParticipants — 3 tied at 2nd', () => {
  it('ranks are [1, 2, 2, 2, 5]', () => {
    nextId = 1;
    const result = computeRankedParticipants([
      makeParticipant('Alice', 100),
      makeParticipant('Bob', 80),
      makeParticipant('Carol', 80),
      makeParticipant('Dan', 80),
      makeParticipant('Eve', 60),
    ]);
    expect(result.map(p => p.rank)).toEqual([1, 2, 2, 2, 5]);
  });

  it('Alice has isTied=false, Bob/Carol/Dan all tied', () => {
    nextId = 1;
    const result = computeRankedParticipants([
      makeParticipant('Alice', 100),
      makeParticipant('Bob', 80),
      makeParticipant('Carol', 80),
      makeParticipant('Dan', 80),
    ]);
    expect(result[0].isTied).toBe(false);
    expect(result[1].isTied).toBe(true);
    expect(result[2].isTied).toBe(true);
    expect(result[3].isTied).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Gherkin scenario 4: all tied
// ---------------------------------------------------------------------------

describe('computeRankedParticipants — all tied', () => {
  it('all rank=1, all isTied=true, tiedPlayerIds includes everyone', () => {
    nextId = 1;
    const a = makeParticipant('Alice', 50);
    const b = makeParticipant('Bob', 50);
    const c = makeParticipant('Carol', 50);
    const result = computeRankedParticipants([a, b, c]);
    expect(result.map(p => p.rank)).toEqual([1, 1, 1]);
    expect(result.map(p => p.isTied)).toEqual([true, true, true]);
    for (const p of result) {
      expect(new Set(p.tiedPlayerIds)).toEqual(new Set([a.id, b.id, c.id]));
    }
  });
});

// ---------------------------------------------------------------------------
// Gherkin scenario 5: tied at last place
// ---------------------------------------------------------------------------

describe('computeRankedParticipants — tied at last place', () => {
  it('ranks are [1, 2, 2] when last two participants share lowest score', () => {
    nextId = 1;
    const result = computeRankedParticipants([
      makeParticipant('Alice', 100),
      makeParticipant('Bob', 50),
      makeParticipant('Carol', 50),
    ]);
    expect(result.map(p => p.rank)).toEqual([1, 2, 2]);
    expect(result[0].isTied).toBe(false);
    expect(result[1].isTied).toBe(true);
    expect(result[2].isTied).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Empty / single edge cases
// ---------------------------------------------------------------------------

describe('computeRankedParticipants — edge cases', () => {
  it('returns [] for empty input', () => {
    expect(computeRankedParticipants([])).toEqual([]);
  });

  it('single participant: rank=1, isTied=false, tiedPlayerIds=[self.id]', () => {
    nextId = 1;
    const p = makeParticipant('Alice', 10);
    const result = computeRankedParticipants([p]);
    expect(result).toHaveLength(1);
    expect(result[0].rank).toBe(1);
    expect(result[0].isTied).toBe(false);
    expect(result[0].tiedPlayerIds).toEqual([p.id]);
  });

  it('does not mutate the input array', () => {
    nextId = 1;
    const input: ParticipantDto[] = [makeParticipant('Bob', 80), makeParticipant('Alice', 90)];
    const before = input.map(p => p.id);
    computeRankedParticipants(input);
    const after = input.map(p => p.id);
    expect(after).toEqual(before);
  });

  it('preserves all original participant fields on the decorated output', () => {
    nextId = 1;
    const p = makeParticipant('Alice', 42);
    const [result] = computeRankedParticipants([p]);
    expect(result.id).toBe(p.id);
    expect(result.userId).toBe(p.userId);
    expect(result.displayName).toBe(p.displayName);
    expect(result.isOwner).toBe(p.isOwner);
    expect(result.joinOrder).toBe(p.joinOrder);
    expect(result.totalScore).toBe(p.totalScore);
    expect(result.finalRank).toBe(p.finalRank);
  });
});

// ---------------------------------------------------------------------------
// Locale-aware tie-breaker
// ---------------------------------------------------------------------------

describe('computeRankedParticipants — locale-aware secondary sort', () => {
  it('accent-insensitive: Bob before Étienne when scores tied (B<E)', () => {
    nextId = 1;
    const result = computeRankedParticipants([
      makeParticipant('Étienne', 100),
      makeParticipant('Bob', 100),
    ]);
    expect(result.map(p => p.displayName)).toEqual(['Bob', 'Étienne']);
  });

  it('accent folding: Eva before Ève (E<E, but accent ignored)', () => {
    nextId = 1;
    // With sensitivity:'base' É folds to E; the locale order is then by tie-breaker.
    // 'Eva' and 'Ève' are different words; Italian collation orders 'a' before 'e'
    // (so 'Eva' should come before 'Ève').
    const result = computeRankedParticipants([
      makeParticipant('Ève', 100),
      makeParticipant('Eva', 100),
    ]);
    expect(result[0].displayName).toBe('Eva');
    expect(result[1].displayName).toBe('Ève');
  });

  it('lowercase / uppercase folding: alice and Alice treated equivalent', () => {
    nextId = 1;
    // sensitivity:'base' folds case as well as accent. Sort then falls back to
    // input order; we just verify no throw and consistent rank assignment.
    const result = computeRankedParticipants([
      makeParticipant('alice', 100),
      makeParticipant('Alice', 100),
    ]);
    expect(result).toHaveLength(2);
    expect(result.every(p => p.rank === 1 && p.isTied)).toBe(true);
  });
});
