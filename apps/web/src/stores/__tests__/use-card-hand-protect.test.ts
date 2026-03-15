import { describe, it, expect, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useCardHand } from '../use-card-hand';

describe('useCardHand protectedIds', () => {
  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.clear());
  });

  it('protectCard adds id to protectedIds', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => {
      result.current.drawCard({
        id: 's1',
        entity: 'session',
        title: 'Session',
        href: '/sessions/s1',
      });
      result.current.protectCard('s1');
    });
    expect(result.current.protectedIds.has('s1')).toBe(true);
  });

  it('unprotectCard removes id from protectedIds', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => {
      result.current.drawCard({
        id: 's1',
        entity: 'session',
        title: 'Session',
        href: '/sessions/s1',
      });
      result.current.protectCard('s1');
      result.current.unprotectCard('s1');
    });
    expect(result.current.protectedIds.has('s1')).toBe(false);
  });

  it('FIFO eviction skips protected cards', () => {
    const { result } = renderHook(() => useCardHand());

    act(() => {
      for (let i = 0; i < 10; i++) {
        result.current.drawCard({
          id: `c${i}`,
          entity: 'game',
          title: `Card ${i}`,
          href: `/game/${i}`,
        });
      }
      result.current.protectCard('c0');
    });

    expect(result.current.cards).toHaveLength(10);

    act(() => {
      result.current.drawCard({ id: 'c10', entity: 'game', title: 'Card 10', href: '/game/10' });
    });

    expect(result.current.cards).toHaveLength(10);
    expect(result.current.cards.find(c => c.id === 'c0')).toBeDefined();
    expect(result.current.cards.find(c => c.id === 'c1')).toBeUndefined();
    expect(result.current.cards.find(c => c.id === 'c10')).toBeDefined();
  });
});
