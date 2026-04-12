// apps/web/src/__tests__/stores/card-hand-store.test.ts
import { describe, it, expect, beforeEach } from 'vitest';
import { useCardHand, selectPinnedCards, selectRecentCards } from '@/lib/stores/card-hand-store';
import type { HandCard } from '@/lib/stores/card-hand-store';

function makeCard(
  id: string,
  overrides: Partial<Omit<HandCard, 'addedAt'>> = {}
): Omit<HandCard, 'addedAt'> {
  const [entityType, entityId] = id.split(':') as [HandCard['entityType'], string];
  return {
    id,
    entityType,
    entityId,
    label: overrides.label ?? `Card ${entityId}`,
    href: `/games/${entityId}`,
    pinned: false,
    ...overrides,
  };
}

beforeEach(() => {
  useCardHand.setState({ cards: [] });
});

describe('useCardHand — drawCard', () => {
  it('aggiunge una card', () => {
    useCardHand.getState().drawCard(makeCard('game:1', { label: 'Catan' }));
    expect(useCardHand.getState().cards).toHaveLength(1);
    expect(useCardHand.getState().cards[0].label).toBe('Catan');
  });

  it('aggiornare una card esistente la porta in cima (addedAt più recente)', () => {
    useCardHand.getState().drawCard(makeCard('game:1', { label: 'Catan' }));
    const firstTime = useCardHand.getState().cards[0].addedAt;
    useCardHand.getState().drawCard(makeCard('game:1', { label: 'Catan v2' }));
    const cards = useCardHand.getState().cards;
    expect(cards).toHaveLength(1);
    expect(cards[0].label).toBe('Catan v2');
    expect(cards[0].addedAt).toBeGreaterThanOrEqual(firstTime);
  });

  it('evict la card non-pinned più vecchia quando si supera MAX (10)', () => {
    for (let i = 1; i <= 10; i++) {
      useCardHand.getState().drawCard(makeCard(`game:${i}`));
    }
    expect(useCardHand.getState().cards).toHaveLength(10);
    useCardHand.getState().drawCard(makeCard('game:11'));
    expect(useCardHand.getState().cards).toHaveLength(10);
    expect(useCardHand.getState().cards.find(c => c.id === 'game:1')).toBeUndefined();
    expect(useCardHand.getState().cards.find(c => c.id === 'game:11')).toBeDefined();
  });

  it('NON evict card pinned anche se è la più vecchia', () => {
    for (let i = 1; i <= 10; i++) {
      useCardHand.getState().drawCard(makeCard(`game:${i}`));
    }
    useCardHand.getState().pinCard('game:1');
    useCardHand.getState().drawCard(makeCard('game:11'));
    expect(useCardHand.getState().cards.find(c => c.id === 'game:1')).toBeDefined();
    expect(useCardHand.getState().cards.find(c => c.id === 'game:2')).toBeUndefined();
  });
});

describe('useCardHand — discardCard', () => {
  it('rimuove la card', () => {
    useCardHand.getState().drawCard(makeCard('game:1'));
    useCardHand.getState().discardCard('game:1');
    expect(useCardHand.getState().cards).toHaveLength(0);
  });
});

describe('useCardHand — pinCard / unpinCard', () => {
  it('pin e unpin', () => {
    useCardHand.getState().drawCard(makeCard('game:1'));
    useCardHand.getState().pinCard('game:1');
    expect(useCardHand.getState().cards[0].pinned).toBe(true);
    useCardHand.getState().unpinCard('game:1');
    expect(useCardHand.getState().cards[0].pinned).toBe(false);
  });
});

describe('useCardHand — clearHand', () => {
  it('svuota tutte le card', () => {
    useCardHand.getState().drawCard(makeCard('game:1'));
    useCardHand.getState().drawCard(makeCard('game:2'));
    useCardHand.getState().clearHand();
    expect(useCardHand.getState().cards).toHaveLength(0);
  });
});

describe('selectors', () => {
  it('selectPinnedCards restituisce solo pinned', () => {
    useCardHand.getState().drawCard(makeCard('game:1'));
    useCardHand.getState().drawCard(makeCard('game:2'));
    useCardHand.getState().pinCard('game:1');
    expect(selectPinnedCards(useCardHand.getState())).toHaveLength(1);
    expect(selectPinnedCards(useCardHand.getState())[0].id).toBe('game:1');
  });

  it('selectRecentCards restituisce solo non-pinned, ordinati per addedAt desc', () => {
    useCardHand.getState().drawCard(makeCard('game:1'));
    useCardHand.getState().drawCard(makeCard('game:2'));
    useCardHand.getState().pinCard('game:1');
    const recent = selectRecentCards(useCardHand.getState());
    expect(recent).toHaveLength(1);
    expect(recent[0].id).toBe('game:2');
  });
});
