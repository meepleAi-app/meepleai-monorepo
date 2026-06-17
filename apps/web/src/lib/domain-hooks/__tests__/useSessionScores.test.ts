import { describe, expect, it, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';

import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { useSessionScores } from '@/lib/domain-hooks/useSessionScores';

describe('useSessionScores — Block A #2389', () => {
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

  it('derives a legacy `scores` map from scoreData when scoringType is Points', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: {
        scores: [
          { playerId: 'p1', points: 12 },
          { playerId: 'p2', points: 7 },
        ],
      },
    });

    const { result } = renderHook(() => useSessionScores());

    expect(result.current.scores).toEqual({ p1: 12, p2: 7 });
  });

  it('returns empty `scores` for non-Points scoringType', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'BinaryWin',
      scoreData: { results: [{ playerId: 'p1', isWinner: true }] },
    });

    const { result } = renderHook(() => useSessionScores());

    expect(result.current.scores).toEqual({});
  });
});
