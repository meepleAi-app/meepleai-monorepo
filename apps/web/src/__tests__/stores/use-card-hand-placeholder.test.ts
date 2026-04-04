import { act, renderHook } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

import { useCardHand } from '@/stores/use-card-hand';
import type { HandCard } from '@/stores/use-card-hand';

describe('useCardHand placeholder cards', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    act(() => {
      useCardHand.setState({
        cards: [],
        focusedIdx: -1,
        pinnedIds: new Set(),
        protectedIds: new Set(),
        maxHandSize: 10,
      });
    });
  });

  it('HandCard accepts isPlaceholder and placeholderAction fields', () => {
    const card: HandCard = {
      id: 'action-search-game',
      entity: 'game',
      title: 'Cerca Gioco',
      href: '#action-search-game',
      isPlaceholder: true,
      placeholderAction: 'search-game',
    };
    expect(card.isPlaceholder).toBe(true);
    expect(card.placeholderAction).toBe('search-game');
  });

  it('can draw a placeholder card', () => {
    const { result } = renderHook(() => useCardHand());

    const placeholder: HandCard = {
      id: 'action-search-game',
      entity: 'game',
      title: 'Cerca Gioco',
      href: '#action-search-game',
      isPlaceholder: true,
      placeholderAction: 'search-game',
    };

    act(() => result.current.drawCard(placeholder));

    expect(result.current.cards).toHaveLength(1);
    expect(result.current.cards[0].id).toBe('action-search-game');
    expect(result.current.cards[0].isPlaceholder).toBe(true);
  });

  it('placeholder cards survive FIFO eviction when hand is full', () => {
    const { result } = renderHook(() => useCardHand());

    // Draw the placeholder card first
    const placeholder: HandCard = {
      id: 'action-search-agent',
      entity: 'agent',
      title: 'Cerca Agente',
      href: '#action-search-agent',
      isPlaceholder: true,
      placeholderAction: 'search-agent',
    };

    act(() => result.current.drawCard(placeholder));

    // Fill up the rest of the hand (maxHandSize=10, placeholder is slot 1)
    for (let i = 0; i < 9; i++) {
      act(() =>
        result.current.drawCard({
          id: `regular-card-${i}`,
          entity: 'game',
          title: `Game ${i}`,
          href: `/games/${i}`,
        })
      );
    }

    // Hand is now at capacity (10 cards)
    expect(result.current.cards).toHaveLength(10);
    expect(result.current.cards.some(c => c.id === 'action-search-agent')).toBe(true);

    // Draw 5 more regular cards — each should evict a non-placeholder
    for (let i = 9; i < 14; i++) {
      act(() =>
        result.current.drawCard({
          id: `regular-card-${i}`,
          entity: 'game',
          title: `Game ${i}`,
          href: `/games/${i}`,
        })
      );
    }

    // Placeholder must still be in the hand
    expect(result.current.cards.some(c => c.id === 'action-search-agent')).toBe(true);
    // Hand size must not exceed maxHandSize
    expect(result.current.cards.length).toBeLessThanOrEqual(10);
  });

  it('regular cards do not get isPlaceholder by default', () => {
    const card: HandCard = {
      id: 'regular-1',
      entity: 'game',
      title: 'Regular Game',
      href: '/games/1',
    };
    expect(card.isPlaceholder).toBeUndefined();
    expect(card.placeholderAction).toBeUndefined();
  });
});
