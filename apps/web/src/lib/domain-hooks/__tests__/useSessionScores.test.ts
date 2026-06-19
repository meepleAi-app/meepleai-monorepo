import { describe, expect, it, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';

import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { useSessionScores } from '@/lib/domain-hooks/useSessionScores';

describe('useSessionScores — Block A/C #2389', () => {
  beforeEach(() => useLiveSessionStore.getState().reset());

  it('returns scoringType and scoreData verbatim', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Ranking',
      scoreData: { positions: [{ playerId: 'p1', position: 1 }] },
    });

    const { result } = renderHook(() => useSessionScores());

    expect(result.current.scoringType).toBe('Ranking');
    expect(result.current.scoreData).toEqual({ positions: [{ playerId: 'p1', position: 1 }] });
  });

  // Block C cleanup: legacy `scores` map is gone — surface only the polymorphic payload.
  it('does not expose a legacy `scores` map on the hook return', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 10 }] },
    });
    const { result } = renderHook(() => useSessionScores());
    expect((result.current as Record<string, unknown>).scores).toBeUndefined();
  });

  // Block C cleanup: leader derives from scoreData.scores (Points-only) keyed by playerId.
  it('derives leader as the playerId with the max points when scoringType is Points', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: {
        scores: [
          { playerId: 'p1', points: 5 },
          { playerId: 'p2', points: 10 },
          { playerId: 'p3', points: 3 },
        ],
      },
    });

    const { result } = renderHook(() => useSessionScores());
    expect(result.current.leader).toBe('p2');
  });

  it('returns null leader when scoringType is not Points', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Ranking',
      scoreData: { positions: [{ playerId: 'p1', position: 1 }] },
    });

    const { result } = renderHook(() => useSessionScores());
    expect(result.current.leader).toBeNull();
  });

  it('returns null leader when scoreData is null', () => {
    const { result } = renderHook(() => useSessionScores());
    expect(result.current.leader).toBeNull();
  });

  it('returns null leader when scoringType is Points but scores array is empty', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [] },
    });

    const { result } = renderHook(() => useSessionScores());
    expect(result.current.leader).toBeNull();
  });
});
