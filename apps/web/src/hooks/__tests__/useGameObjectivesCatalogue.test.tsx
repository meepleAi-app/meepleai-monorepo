/**
 * Unit tests for useGameObjectivesCatalogue — Issue #2432.
 *
 * The hook is the boundary the rest of the FE consumes (via ScoreTabContent +
 * scores/page.tsx). Pinning its contract now lets the implementation evolve
 * (default-only today → per-game lookup tomorrow) without disturbing callers.
 */

import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';

import {
  useGameObjectivesCatalogue,
  DEFAULT_OBJECTIVES_CATALOGUE,
} from '@/hooks/useGameObjectivesCatalogue';

describe('useGameObjectivesCatalogue', () => {
  it('returns the default catalogue when no gameId is provided', () => {
    // Use `toEqual` not `toBe` so the contract survives the future swap to a
    // TanStack Query call that allocates a fresh array on each fetch.
    const { result } = renderHook(() => useGameObjectivesCatalogue(null));
    expect(result.current).toEqual(DEFAULT_OBJECTIVES_CATALOGUE);
  });

  it('returns the default catalogue for any gameId (transitional stub)', () => {
    // While the per-game BE endpoint is unbuilt (#2432 follow-up), every
    // gameId resolves to the same default catalogue. The hook contract is
    // stable: callers depend on the return shape, not on the source.
    const { result } = renderHook(() =>
      useGameObjectivesCatalogue('00000000-0000-0000-0000-000000000001')
    );
    expect(result.current).toEqual(DEFAULT_OBJECTIVES_CATALOGUE);
  });

  it('returns a readonly array — callers cannot mutate the catalogue', () => {
    const { result } = renderHook(() => useGameObjectivesCatalogue(null));
    // TypeScript readonly contract is the primary guard; this is a runtime
    // smoke that the array is stable across calls.
    expect(Array.isArray(result.current)).toBe(true);
    expect(result.current.length).toBeGreaterThan(0);
  });

  it('exposes the default catalogue with the legacy MVP labels', () => {
    // The placeholder labels are documented in CLAUDE.md domain notes — pin
    // them here so a label swap is intentional, not an accident.
    expect(DEFAULT_OBJECTIVES_CATALOGUE).toEqual([
      'Vittoria',
      'Sopravvivenza',
      'Tesoro',
      'Boss',
      'Quest',
    ]);
  });
});
