import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useConnectionBarNav } from '../useConnectionBarNav';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

describe('useConnectionBarNav', () => {
  beforeEach(() => {
    act(() => useCascadeNavigationStore.getState().closeCascade());
  });

  it('opens DeckStack when count >= 1', () => {
    const { result } = renderHook(() => useConnectionBarNav('game-123'));
    const pip = {
      entityType: 'agent' as const,
      count: 1,
      label: 'Agent',
      icon: vi.fn() as any,
      isEmpty: false,
    };
    const rect = new DOMRect(100, 200, 50, 30);

    act(() => result.current.handlePipClick(pip, rect));

    const state = useCascadeNavigationStore.getState();
    expect(state.state).toBe('deckStack');
    expect(state.activeEntityType).toBe('agent');
    expect(state.sourceEntityId).toBe('game-123');
  });

  it('opens DeckStack when count >= 2', () => {
    const { result } = renderHook(() => useConnectionBarNav('game-123'));
    const pip = {
      entityType: 'kb' as const,
      count: 3,
      label: 'KB',
      icon: vi.fn() as any,
      isEmpty: false,
    };
    const rect = new DOMRect(100, 200, 50, 30);

    act(() => result.current.handlePipClick(pip, rect));

    const state = useCascadeNavigationStore.getState();
    expect(state.state).toBe('deckStack');
    expect(state.activeEntityType).toBe('kb');
    expect(state.sourceEntityId).toBe('game-123');
  });

  it('does nothing when isEmpty is true (create action — caller handles)', () => {
    const { result } = renderHook(() => useConnectionBarNav('game-123'));
    const pip = {
      entityType: 'chat' as const,
      count: 0,
      label: 'Chat',
      icon: vi.fn() as any,
      isEmpty: true,
    };
    const rect = new DOMRect(100, 200, 50, 30);

    act(() => result.current.handlePipClick(pip, rect));

    const state = useCascadeNavigationStore.getState();
    expect(state.state).toBe('closed'); // no action taken
  });
});
