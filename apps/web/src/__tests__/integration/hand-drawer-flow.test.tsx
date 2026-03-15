import { act } from '@testing-library/react';

import { useCardHand } from '@/stores/use-card-hand';

describe('Hand Drawer Flow', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    // Reset store to initial state
    act(() => {
      useCardHand.setState({
        cards: [],
        focusedIdx: -1,
        pinnedIds: new Set(),
        isHandCollapsed: false,
        expandedStack: false,
        highlightEntity: null,
        context: 'user',
      });
    });
  });

  describe('useCardHand store integration', () => {
    it('hand starts empty with no cards', () => {
      const state = useCardHand.getState();
      expect(state.cards).toHaveLength(0);
      expect(state.focusedIdx).toBe(-1);
    });

    it('drawCard adds a card and focuses it', () => {
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g1',
          entity: 'game',
          title: 'Catan',
          href: '/library/g1',
        });
      });
      const state = useCardHand.getState();
      expect(state.cards).toHaveLength(1);
      expect(state.cards[0].title).toBe('Catan');
      expect(state.focusedIdx).toBe(0);
    });

    it('drawing duplicate card focuses it instead of adding', () => {
      act(() => {
        const { drawCard } = useCardHand.getState();
        drawCard({
          id: 'g1',
          entity: 'game',
          title: 'Catan',
          href: '/library/g1',
        });
      });
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g2',
          entity: 'game',
          title: 'Azul',
          href: '/library/g2',
        });
      });
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g1',
          entity: 'game',
          title: 'Catan',
          href: '/library/g1',
        });
      });
      const state = useCardHand.getState();
      expect(state.cards).toHaveLength(2);
      expect(state.focusedIdx).toBe(0); // g1 is at index 0
    });

    it('collapse/expand cycle preserves cards', () => {
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g1',
          entity: 'game',
          title: 'Catan',
          href: '/library/g1',
        });
      });
      act(() => {
        useCardHand.getState().collapseHand();
      });
      expect(useCardHand.getState().isHandCollapsed).toBe(true);
      expect(useCardHand.getState().cards).toHaveLength(1);

      act(() => useCardHand.getState().expandHand());
      expect(useCardHand.getState().isHandCollapsed).toBe(false);
      expect(useCardHand.getState().cards).toHaveLength(1);
    });

    it('FIFO eviction removes oldest non-pinned card when at max', () => {
      act(() => {
        for (let i = 0; i < 10; i++) {
          useCardHand.getState().drawCard({
            id: `g${i}`,
            entity: 'game',
            title: `Game ${i}`,
            href: `/library/g${i}`,
          });
        }
      });
      expect(useCardHand.getState().cards).toHaveLength(10);

      // Draw 11th card — triggers FIFO eviction of oldest non-pinned
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g10',
          entity: 'game',
          title: 'Game 10',
          href: '/library/g10',
        });
      });
      const state = useCardHand.getState();
      expect(state.cards).toHaveLength(10);
      // g0 was evicted (oldest, not pinned)
      expect(state.cards.find(c => c.id === 'g0')).toBeUndefined();
      expect(state.cards.find(c => c.id === 'g10')).toBeDefined();
    });

    it('FIFO eviction skips pinned cards', () => {
      act(() => {
        for (let i = 0; i < 10; i++) {
          useCardHand.getState().drawCard({
            id: `g${i}`,
            entity: 'game',
            title: `Game ${i}`,
            href: `/library/g${i}`,
          });
        }
        // Pin the oldest card
        useCardHand.getState().pinCard('g0');
      });

      // Draw 11th card — g0 is pinned, so g1 (next oldest unpinned) is evicted
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g10',
          entity: 'game',
          title: 'Game 10',
          href: '/library/g10',
        });
      });
      const state = useCardHand.getState();
      expect(state.cards).toHaveLength(10);
      expect(state.cards.find(c => c.id === 'g0')).toBeDefined(); // pinned, kept
      expect(state.cards.find(c => c.id === 'g1')).toBeUndefined(); // evicted
      expect(state.cards.find(c => c.id === 'g10')).toBeDefined();
    });

    it('collapse state persists to localStorage', () => {
      act(() => useCardHand.getState().collapseHand());
      expect(localStorage.getItem('meeple-hand-collapsed')).toBe('true');
      act(() => useCardHand.getState().expandHand());
      expect(localStorage.getItem('meeple-hand-collapsed')).toBe('false');
    });

    it('cards persist to sessionStorage', () => {
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g1',
          entity: 'game',
          title: 'Catan',
          href: '/library/g1',
        });
      });
      const stored = JSON.parse(sessionStorage.getItem('meeple-card-hand') ?? '[]');
      expect(stored).toHaveLength(1);
      expect(stored[0].id).toBe('g1');
    });

    it('pinned card ids persist to localStorage', () => {
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g1',
          entity: 'game',
          title: 'Catan',
          href: '/library/g1',
        });
        useCardHand.getState().pinCard('g1');
      });
      const stored = JSON.parse(localStorage.getItem('meeple-card-pins') ?? '[]');
      expect(stored).toContain('g1');
    });

    it('discardCard removes card and adjusts focus', () => {
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g1',
          entity: 'game',
          title: 'Catan',
          href: '/library/g1',
        });
        useCardHand.getState().drawCard({
          id: 'g2',
          entity: 'game',
          title: 'Azul',
          href: '/library/g2',
        });
      });
      // Focus is on g2 (index 1) after drawing two cards
      expect(useCardHand.getState().focusedIdx).toBe(1);

      act(() => useCardHand.getState().discardCard('g2'));
      const state = useCardHand.getState();
      expect(state.cards).toHaveLength(1);
      expect(state.cards[0].id).toBe('g1');
      expect(state.focusedIdx).toBe(0);
    });

    it('discarding all cards resets focus to -1', () => {
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g1',
          entity: 'game',
          title: 'Catan',
          href: '/library/g1',
        });
      });
      act(() => useCardHand.getState().discardCard('g1'));
      expect(useCardHand.getState().cards).toHaveLength(0);
      expect(useCardHand.getState().focusedIdx).toBe(-1);
    });

    it('swipeNext/swipePrev navigate focus through hand', () => {
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g1',
          entity: 'game',
          title: 'Catan',
          href: '/library/g1',
        });
        useCardHand.getState().drawCard({
          id: 'g2',
          entity: 'game',
          title: 'Azul',
          href: '/library/g2',
        });
        useCardHand.getState().drawCard({
          id: 'g3',
          entity: 'game',
          title: 'Wingspan',
          href: '/library/g3',
        });
      });
      // Focus is on last drawn (index 2)
      expect(useCardHand.getState().focusedIdx).toBe(2);

      act(() => useCardHand.getState().swipePrev());
      expect(useCardHand.getState().focusedIdx).toBe(1);

      act(() => useCardHand.getState().swipePrev());
      expect(useCardHand.getState().focusedIdx).toBe(0);

      // Cannot go below 0
      act(() => useCardHand.getState().swipePrev());
      expect(useCardHand.getState().focusedIdx).toBe(0);

      act(() => useCardHand.getState().swipeNext());
      expect(useCardHand.getState().focusedIdx).toBe(1);

      act(() => useCardHand.getState().swipeNext());
      expect(useCardHand.getState().focusedIdx).toBe(2);

      // Cannot go above last index
      act(() => useCardHand.getState().swipeNext());
      expect(useCardHand.getState().focusedIdx).toBe(2);
    });

    it('clear removes unpinned cards but keeps pinned ones', () => {
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g1',
          entity: 'game',
          title: 'Catan',
          href: '/library/g1',
        });
        useCardHand.getState().drawCard({
          id: 'g2',
          entity: 'game',
          title: 'Azul',
          href: '/library/g2',
        });
        useCardHand.getState().pinCard('g1');
      });

      act(() => useCardHand.getState().clear());
      const state = useCardHand.getState();
      expect(state.cards).toHaveLength(1);
      expect(state.cards[0].id).toBe('g1');
    });

    it('focusByHref sets focus to the card matching the href', () => {
      act(() => {
        useCardHand.getState().drawCard({
          id: 'g1',
          entity: 'game',
          title: 'Catan',
          href: '/library/g1',
        });
        useCardHand.getState().drawCard({
          id: 'g2',
          entity: 'game',
          title: 'Azul',
          href: '/library/g2',
        });
      });
      // Focus is on g2 (index 1)
      expect(useCardHand.getState().focusedIdx).toBe(1);

      act(() => useCardHand.getState().focusByHref('/library/g1'));
      expect(useCardHand.getState().focusedIdx).toBe(0);
    });

    it('toggleExpandStack persists to localStorage', () => {
      expect(useCardHand.getState().expandedStack).toBe(false);
      act(() => useCardHand.getState().toggleExpandStack());
      expect(useCardHand.getState().expandedStack).toBe(true);
      expect(localStorage.getItem('meeple-card-stack-expanded')).toBe('true');

      act(() => useCardHand.getState().toggleExpandStack());
      expect(useCardHand.getState().expandedStack).toBe(false);
      expect(localStorage.getItem('meeple-card-stack-expanded')).toBe('false');
    });
  });
});
