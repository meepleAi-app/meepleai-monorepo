/**
 * CardBrowserContext Tests
 *
 * Coverage:
 * - Initial state (closed, empty)
 * - Open with cards and index
 * - History management (push, dedup, FIFO cap at 50)
 * - Close preserves history
 * - clearHistory resets history
 * - setIndex updates currentIndex and pushes history
 * - navigateTo adds card or jumps to existing
 * - Opening with different card list resets cards but accumulates history
 * - useCardBrowser throws outside provider
 */

import React from 'react';
import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  CardBrowserProvider,
  useCardBrowser,
  type CardRef,
} from '../meeple-card-browser/CardBrowserContext';

// ---------- helpers ----------

function makeCard(id: string, entity: CardRef['entity'] = 'game'): CardRef {
  return {
    id,
    entity,
    title: `Card ${id}`,
    subtitle: `Sub ${id}`,
    color: '25 95% 45%',
  };
}

function makeCards(count: number): CardRef[] {
  return Array.from({ length: count }, (_, i) => makeCard(`card-${i}`));
}

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <CardBrowserProvider>{children}</CardBrowserProvider>
);

// ---------- sessionStorage mock ----------

const store: Record<string, string> = {};

beforeEach(() => {
  Object.keys(store).forEach(k => delete store[k]);

  vi.stubGlobal('sessionStorage', {
    getItem: vi.fn((key: string) => store[key] ?? null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key];
    }),
  });
});

// ---------- tests ----------

describe('CardBrowserContext', () => {
  it('starts closed with empty state', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });

    expect(result.current.isOpen).toBe(false);
    expect(result.current.cards).toEqual([]);
    expect(result.current.currentIndex).toBe(0);
    expect(result.current.history).toEqual([]);
  });

  it('opens with cards and index', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    const cards = makeCards(5);

    act(() => {
      result.current.open(cards, 2);
    });

    expect(result.current.isOpen).toBe(true);
    expect(result.current.cards).toEqual(cards);
    expect(result.current.currentIndex).toBe(2);
  });

  it('pushes to history on open', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    const cards = makeCards(3);

    act(() => {
      result.current.open(cards, 0);
    });

    expect(result.current.history).toHaveLength(1);
    expect(result.current.history[0].id).toBe('card-0');
  });

  it('does not push consecutive duplicates to history', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    const cards = makeCards(3);

    act(() => {
      result.current.open(cards, 1);
    });

    // setIndex to the same card should not duplicate
    act(() => {
      result.current.setIndex(1);
    });

    expect(result.current.history).toHaveLength(1);
  });

  it('enforces max 50 history entries (FIFO)', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });

    // Build 55 unique cards
    const manyCards = Array.from({ length: 55 }, (_, i) => makeCard(`h-${i}`));

    act(() => {
      result.current.open(manyCards, 0);
    });

    // Navigate through cards 1..54 (card 0 already pushed by open)
    for (let i = 1; i < 55; i++) {
      act(() => {
        result.current.setIndex(i);
      });
    }

    expect(result.current.history.length).toBeLessThanOrEqual(50);
    // Earliest entries should have been shifted out
    expect(result.current.history[0].id).not.toBe('h-0');
  });

  it('closes and resets isOpen but history persists', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    const cards = makeCards(3);

    act(() => {
      result.current.open(cards, 0);
    });
    expect(result.current.history).toHaveLength(1);

    act(() => {
      result.current.close();
    });

    expect(result.current.isOpen).toBe(false);
    expect(result.current.history).toHaveLength(1);
  });

  it('clears history', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    const cards = makeCards(3);

    act(() => {
      result.current.open(cards, 0);
    });
    expect(result.current.history).toHaveLength(1);

    act(() => {
      result.current.clearHistory();
    });

    expect(result.current.history).toEqual([]);
  });

  it('resets cards when opening from different list but history accumulates', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    const listA = makeCards(3);
    const listB = [makeCard('b-0', 'player'), makeCard('b-1', 'agent')];

    act(() => {
      result.current.open(listA, 0);
    });
    expect(result.current.history).toHaveLength(1);

    act(() => {
      result.current.open(listB, 1);
    });

    expect(result.current.cards).toEqual(listB);
    expect(result.current.currentIndex).toBe(1);
    // History should have both entries (card-0 from listA, b-1 from listB)
    expect(result.current.history).toHaveLength(2);
    expect(result.current.history[0].id).toBe('card-0');
    expect(result.current.history[1].id).toBe('b-1');
  });

  it('setIndex updates currentIndex and pushes to history', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    const cards = makeCards(5);

    act(() => {
      result.current.open(cards, 0);
    });

    act(() => {
      result.current.setIndex(3);
    });

    expect(result.current.currentIndex).toBe(3);
    expect(result.current.history).toHaveLength(2);
    expect(result.current.history[1].id).toBe('card-3');
  });

  it('navigateTo jumps to existing card in list', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    const cards = makeCards(5);

    act(() => {
      result.current.open(cards, 0);
    });

    act(() => {
      result.current.navigateTo(cards[4]);
    });

    expect(result.current.currentIndex).toBe(4);
    expect(result.current.cards).toHaveLength(5); // no new card added
  });

  it('navigateTo appends card not in current list', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    const cards = makeCards(3);
    const newCard = makeCard('new-card', 'agent');

    act(() => {
      result.current.open(cards, 0);
    });

    act(() => {
      result.current.navigateTo(newCard);
    });

    expect(result.current.cards).toHaveLength(4);
    expect(result.current.currentIndex).toBe(3);
    expect(result.current.cards[3].id).toBe('new-card');
  });

  it('throws when useCardBrowser is used outside provider', () => {
    expect(() => {
      renderHook(() => useCardBrowser());
    }).toThrow('useCardBrowser must be used within CardBrowserProvider');
  });

  it('persists history to sessionStorage', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    const cards = makeCards(2);

    act(() => {
      result.current.open(cards, 0);
    });

    expect(sessionStorage.setItem).toHaveBeenCalledWith(
      'meeple-card-browser-history',
      expect.any(String)
    );

    const storedValue = store['meeple-card-browser-history'];
    const parsed = JSON.parse(storedValue);
    expect(parsed).toHaveLength(1);
    expect(parsed[0].id).toBe('card-0');
  });
});
