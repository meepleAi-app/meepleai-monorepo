import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { useCodenamesStateEditor } from '../use-codenames-state-editor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

const mutateMock = vi.fn<[unknown], void>();
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: mutateMock }),
}));

const SID = 'sess-1';
beforeEach(() => {
  mutateMock.mockReset();
  useLiveSessionStore.getState().reset();
});
function current() {
  return useLiveSessionStore.getState().gameState as
    | import('../codenames-state').CodenamesGameState
    | null;
}

describe('useCodenamesStateEditor', () => {
  it('initializeState seeds a 25-cell board + a currentTeam + null clue', () => {
    const { result } = renderHook(() => useCodenamesStateEditor(SID));
    act(() => result.current.initializeState());
    const s = current();
    expect(s?.game).toBe('codenames');
    expect(s?.board).toHaveLength(25);
    expect(['red', 'blue']).toContain(s?.currentTeam);
    expect(s?.clue).toBeNull();
  });

  it('reveals a cell (idempotent) and PUTs immediately (no debounce)', () => {
    const { result } = renderHook(() => useCodenamesStateEditor(SID));
    act(() => result.current.initializeState());
    mutateMock.mockClear();
    act(() => result.current.revealCell(3));
    expect(current()?.board[3].revealed).toBe(true);
    expect(mutateMock).toHaveBeenCalledTimes(1); // immediate, no timer
    act(() => result.current.revealCell(3)); // idempotent
    expect(current()?.board[3].revealed).toBe(true);
  });

  it('setClue clamps number >= 0; clearClue nulls it', () => {
    const { result } = renderHook(() => useCodenamesStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setClue('MARE', -2));
    expect(current()?.clue).toEqual({ word: 'MARE', number: 0 });
    act(() => result.current.clearClue());
    expect(current()?.clue).toBeNull();
  });

  it('switchTeam flips currentTeam and clears the clue', () => {
    const { result } = renderHook(() => useCodenamesStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setClue('MARE', 2));
    const before = current()!.currentTeam;
    act(() => result.current.switchTeam());
    expect(current()?.currentTeam).toBe(before === 'red' ? 'blue' : 'red');
    expect(current()?.clue).toBeNull();
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => useCodenamesStateEditor(SID));
    act(() => result.current.revealCell(0));
    expect(current()).toBeNull();
  });
});
