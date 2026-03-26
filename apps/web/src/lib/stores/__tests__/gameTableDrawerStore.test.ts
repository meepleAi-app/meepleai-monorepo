/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, beforeEach } from 'vitest';
import { act } from '@testing-library/react';

import { useGameTableDrawer } from '../gameTableDrawerStore';

describe('useGameTableDrawer', () => {
  beforeEach(() => {
    act(() => useGameTableDrawer.getState().close());
  });

  it('starts closed with no content', () => {
    const state = useGameTableDrawer.getState();
    expect(state.isOpen).toBe(false);
    expect(state.content).toBeNull();
  });

  it('opens with typed content', () => {
    act(() => useGameTableDrawer.getState().open({ type: 'chat', agentId: 'a1' }));
    const state = useGameTableDrawer.getState();
    expect(state.isOpen).toBe(true);
    expect(state.content).toEqual({ type: 'chat', agentId: 'a1' });
  });

  it('closes and clears content', () => {
    act(() => useGameTableDrawer.getState().open({ type: 'stats', gameId: 'g1' }));
    act(() => useGameTableDrawer.getState().close());
    const state = useGameTableDrawer.getState();
    expect(state.isOpen).toBe(false);
    expect(state.content).toBeNull();
  });

  it('replaces content when opened again', () => {
    act(() => useGameTableDrawer.getState().open({ type: 'chat', agentId: 'a1' }));
    act(() => useGameTableDrawer.getState().open({ type: 'kb', gameId: 'g1' }));
    const state = useGameTableDrawer.getState();
    expect(state.content).toEqual({ type: 'kb', gameId: 'g1' });
  });
});
