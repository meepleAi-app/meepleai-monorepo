import { act, renderHook } from '@testing-library/react';
import { useCardHand } from '@/stores/use-card-hand';
import type { HandCard } from '@/stores/use-card-hand';

const makeCard = (id: string, entity = 'game' as const): HandCard => ({
  id,
  entity,
  title: `Card ${id}`,
  href: `/test/${id}`,
});

describe('useCardHand', () => {
  beforeEach(() => {
    // Clear storage first, then reset Zustand store state directly
    sessionStorage.clear();
    localStorage.clear();
    useCardHand.setState({
      cards: [],
      focusedIdx: -1,
      pinnedIds: new Set(),
      context: 'user',
      expandedStack: false,
      highlightEntity: null,
    });
  });

  describe('drawCard', () => {
    it('adds a card and focuses it', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => result.current.drawCard(makeCard('g1')));
      expect(result.current.cards).toHaveLength(1);
      expect(result.current.cards[0].id).toBe('g1');
      expect(result.current.focusedIdx).toBe(0);
    });

    it('focuses existing card instead of duplicating', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g1'));
        result.current.drawCard(makeCard('g2'));
        result.current.drawCard(makeCard('g1')); // duplicate
      });
      expect(result.current.cards).toHaveLength(2);
      expect(result.current.focusedIdx).toBe(0); // focused back to g1
    });

    it('evicts oldest non-pinned card when hand is full (FIFO)', () => {
      const { result } = renderHook(() => useCardHand());
      // Fill hand to max (10)
      for (let i = 0; i < 10; i++) {
        act(() => result.current.drawCard(makeCard(`g${i}`)));
      }
      expect(result.current.cards).toHaveLength(10);
      // Draw one more — g0 should be evicted
      act(() => result.current.drawCard(makeCard('g10')));
      expect(result.current.cards).toHaveLength(10);
      expect(result.current.cards.find(c => c.id === 'g0')).toBeUndefined();
      expect(result.current.cards.find(c => c.id === 'g10')).toBeDefined();
    });

    it('skips pinned cards during FIFO eviction', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g0'));
        result.current.pinCard('g0'); // pin first card
      });
      // Fill remaining slots
      for (let i = 1; i < 10; i++) {
        act(() => result.current.drawCard(makeCard(`g${i}`)));
      }
      // Draw one more — g1 (first non-pinned) should be evicted, not g0
      act(() => result.current.drawCard(makeCard('g10')));
      expect(result.current.cards.find(c => c.id === 'g0')).toBeDefined(); // pinned survives
      expect(result.current.cards.find(c => c.id === 'g1')).toBeUndefined(); // evicted
    });
  });

  describe('discardCard', () => {
    it('removes card and adjusts focus', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g1'));
        result.current.drawCard(makeCard('g2'));
        result.current.drawCard(makeCard('g3'));
        result.current.focusCard(1); // focus g2
      });
      act(() => result.current.discardCard('g2'));
      expect(result.current.cards).toHaveLength(2);
      // Focus should move to next card (g3, now at index 1)
      expect(result.current.focusedIdx).toBe(1);
    });
  });

  describe('pinCard / unpinCard', () => {
    it('pins and unpins a card', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => result.current.drawCard(makeCard('g1')));
      act(() => result.current.pinCard('g1'));
      expect(result.current.pinnedIds.has('g1')).toBe(true);
      act(() => result.current.unpinCard('g1'));
      expect(result.current.pinnedIds.has('g1')).toBe(false);
    });
  });

  describe('swipeNext / swipePrev', () => {
    it('navigates between cards', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g1'));
        result.current.drawCard(makeCard('g2'));
        result.current.drawCard(makeCard('g3'));
        result.current.focusCard(0);
      });
      act(() => result.current.swipeNext());
      expect(result.current.focusedIdx).toBe(1);
      act(() => result.current.swipePrev());
      expect(result.current.focusedIdx).toBe(0);
    });

    it('does not go below 0 or above length', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => result.current.drawCard(makeCard('g1')));
      act(() => result.current.swipePrev());
      expect(result.current.focusedIdx).toBe(0);
      act(() => result.current.swipeNext());
      expect(result.current.focusedIdx).toBe(0); // only 1 card
    });
  });

  describe('focusByHref', () => {
    it('focuses card matching href', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g1'));
        result.current.drawCard(makeCard('g2'));
      });
      act(() => result.current.focusByHref('/test/g1'));
      expect(result.current.focusedIdx).toBe(0);
    });
  });

  describe('context toggle', () => {
    it('toggles between user and admin', () => {
      const { result } = renderHook(() => useCardHand());
      expect(result.current.context).toBe('user');
      act(() => result.current.toggleContext());
      expect(result.current.context).toBe('admin');
      act(() => result.current.toggleContext());
      expect(result.current.context).toBe('user');
    });
  });

  describe('expandedStack', () => {
    it('toggles stack expansion', () => {
      const { result } = renderHook(() => useCardHand());
      expect(result.current.expandedStack).toBe(false);
      act(() => result.current.toggleExpandStack());
      expect(result.current.expandedStack).toBe(true);
    });
  });

  describe('clear', () => {
    it('removes non-pinned cards only', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g1'));
        result.current.drawCard(makeCard('g2'));
        result.current.pinCard('g1');
      });
      act(() => result.current.clear());
      expect(result.current.cards).toHaveLength(1);
      expect(result.current.cards[0].id).toBe('g1');
    });
  });
});
