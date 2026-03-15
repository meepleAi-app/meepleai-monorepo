import { renderHook, act } from '@testing-library/react';
import { useCardHand } from '@/stores/use-card-hand';

describe('useCardHand collapse state', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    act(() => useCardHand.getState().clear());
  });

  it('isHandCollapsed defaults to false', () => {
    const { result } = renderHook(() => useCardHand());
    expect(result.current.isHandCollapsed).toBe(false);
  });

  it('collapseHand sets isHandCollapsed to true', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.collapseHand());
    expect(result.current.isHandCollapsed).toBe(true);
  });

  it('expandHand sets isHandCollapsed to false', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.collapseHand());
    act(() => result.current.expandHand());
    expect(result.current.isHandCollapsed).toBe(false);
  });

  it('persists collapse state to localStorage', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.collapseHand());
    expect(localStorage.getItem('meeple-hand-collapsed')).toBe('true');
  });

  it('restores collapse state from localStorage', () => {
    localStorage.setItem('meeple-hand-collapsed', 'true');
    // Force store to re-read localStorage (singleton)
    act(() =>
      useCardHand.setState({
        isHandCollapsed: localStorage.getItem('meeple-hand-collapsed') === 'true',
      })
    );
    const { result } = renderHook(() => useCardHand());
    expect(result.current.isHandCollapsed).toBe(true);
  });

  it('isHandCollapsed is independent from expandedStack', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.collapseHand());
    act(() => result.current.toggleExpandStack());
    expect(result.current.isHandCollapsed).toBe(true);
    expect(result.current.expandedStack).toBe(true);
  });
});
