// apps/web/src/lib/play-records/hooks/usePlayRecordDraftPersist.test.ts
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { PLAY_RECORD_DRAFT_SCHEMA_VERSION, type PlayRecordDraftState } from '../draft-types';
import { usePlayRecordDraftPersist } from './usePlayRecordDraftPersist';

const KEY = 'meepleai:play-record-create-draft:user-1';

function baseState(overrides: Partial<PlayRecordDraftState> = {}): PlayRecordDraftState {
  return {
    currentStep: 0,
    gameType: 'catalog',
    gameName: 'Wingspan',
    sessionDate: new Date('2026-06-20T18:00:00.000Z'),
    visibility: 'Private',
    enableScoring: false,
    scoringDimensions: [],
    dimensionUnits: {},
    players: [],
    ...overrides,
  };
}

beforeEach(() => {
  localStorage.clear();
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('usePlayRecordDraftPersist', () => {
  it('does NOT persist on pristine mount (first run skipped)', () => {
    renderHook(() => usePlayRecordDraftPersist({ userId: 'user-1', state: baseState() }));
    act(() => vi.advanceTimersByTime(2000));
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('persists to localStorage (debounced) after the state changes', () => {
    const { rerender } = renderHook(props => usePlayRecordDraftPersist(props), {
      initialProps: { userId: 'user-1' as string | null, state: baseState() },
    });
    rerender({ userId: 'user-1', state: baseState({ location: 'Padova' }) });
    act(() => vi.advanceTimersByTime(800));
    const raw = localStorage.getItem(KEY);
    expect(raw).not.toBeNull();
    const env = JSON.parse(raw as string);
    expect(env.draft.location).toBe('Padova');
    expect(env.draft.schemaVersion).toBe(PLAY_RECORD_DRAFT_SCHEMA_VERSION);
    expect(env.draft.sessionDate).toBe('2026-06-20T18:00:00.000Z');
  });

  it('returns initialDraft from a valid persisted envelope on mount', () => {
    localStorage.setItem(
      KEY,
      JSON.stringify({
        savedAt: 1_781_827_200_000, // 2026-06-19T00:00:00.000Z — 1 day before fake time, within 7-day TTL
        draft: {
          schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
          currentStep: 2,
          gameType: 'catalog',
          gameName: 'Catan',
          sessionDate: '2026-06-19T10:00:00.000Z',
          visibility: 'Private',
          enableScoring: false,
          scoringDimensions: [],
          dimensionUnits: {},
          players: [{ id: 'p1', name: 'Ada', score: '10' }],
        },
      })
    );
    vi.setSystemTime(new Date('2026-06-20T00:00:00.000Z'));
    const { result } = renderHook(() =>
      usePlayRecordDraftPersist({ userId: 'user-1', state: baseState() })
    );
    expect(result.current.initialDraft?.gameName).toBe('Catan');
    expect(result.current.initialDraft?.currentStep).toBe(2);
  });

  it('discards a stale draft older than the 7-day TTL and clears the key', () => {
    vi.setSystemTime(new Date('2026-06-20T00:00:00.000Z'));
    const eightDaysAgo = new Date('2026-06-20T00:00:00.000Z').getTime() - 8 * 24 * 60 * 60 * 1000;
    localStorage.setItem(
      KEY,
      JSON.stringify({
        savedAt: eightDaysAgo,
        draft: {
          schemaVersion: PLAY_RECORD_DRAFT_SCHEMA_VERSION,
          currentStep: 0,
          gameType: 'catalog',
          gameName: 'Old',
          sessionDate: '2026-06-10T00:00:00.000Z',
          visibility: 'Private',
          enableScoring: false,
          scoringDimensions: [],
          dimensionUnits: {},
          players: [],
        },
      })
    );
    const { result } = renderHook(() =>
      usePlayRecordDraftPersist({ userId: 'user-1', state: baseState() })
    );
    expect(result.current.initialDraft).toBeNull();
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('clear() removes the persisted draft', () => {
    localStorage.setItem(KEY, JSON.stringify({ savedAt: Date.now(), draft: {} }));
    const { result } = renderHook(() =>
      usePlayRecordDraftPersist({ userId: 'user-1', state: baseState() })
    );
    act(() => result.current.clear());
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('is inert when userId is null (no read, no write)', () => {
    const { rerender } = renderHook(props => usePlayRecordDraftPersist(props), {
      initialProps: { userId: null as string | null, state: baseState() },
    });
    rerender({ userId: null, state: baseState({ location: 'X' }) });
    act(() => vi.advanceTimersByTime(800));
    expect(localStorage.getItem(KEY)).toBeNull();
  });
});
