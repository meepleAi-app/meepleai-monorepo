import { describe, it, expect } from 'vitest';

import {
  PLACEHOLDER_ACTION_CARDS,
  ALL_DEFAULT_CARDS,
  DEFAULT_PINNED_CARDS,
} from '@/config/entity-actions';

describe('PLACEHOLDER_ACTION_CARDS', () => {
  it('has exactly 4 cards', () => {
    expect(PLACEHOLDER_ACTION_CARDS).toHaveLength(4);
  });

  it('every card has isPlaceholder set to true', () => {
    for (const card of PLACEHOLDER_ACTION_CARDS) {
      expect(card.isPlaceholder).toBe(true);
    }
  });

  it('every card has a placeholderAction string', () => {
    for (const card of PLACEHOLDER_ACTION_CARDS) {
      expect(typeof card.placeholderAction).toBe('string');
      expect(card.placeholderAction!.length).toBeGreaterThan(0);
    }
  });

  it('every card href starts with #action-', () => {
    for (const card of PLACEHOLDER_ACTION_CARDS) {
      expect(card.href).toMatch(/^#action-/);
    }
  });

  it('has the expected placeholderAction values', () => {
    const actions = PLACEHOLDER_ACTION_CARDS.map(c => c.placeholderAction);
    expect(actions).toContain('search-agent');
    expect(actions).toContain('search-game');
    expect(actions).toContain('start-session');
    expect(actions).toContain('toolkit');
  });

  it('each card has a unique id', () => {
    const ids = PLACEHOLDER_ACTION_CARDS.map(c => c.id);
    const uniqueIds = new Set(ids);
    expect(uniqueIds.size).toBe(ids.length);
  });
});

describe('ALL_DEFAULT_CARDS', () => {
  it('contains all DEFAULT_PINNED_CARDS entries', () => {
    for (const card of DEFAULT_PINNED_CARDS) {
      expect(ALL_DEFAULT_CARDS.some(c => c.id === card.id)).toBe(true);
    }
  });

  it('contains all PLACEHOLDER_ACTION_CARDS entries', () => {
    for (const card of PLACEHOLDER_ACTION_CARDS) {
      expect(ALL_DEFAULT_CARDS.some(c => c.id === card.id)).toBe(true);
    }
  });

  it('has length equal to DEFAULT_PINNED_CARDS + PLACEHOLDER_ACTION_CARDS', () => {
    expect(ALL_DEFAULT_CARDS).toHaveLength(
      DEFAULT_PINNED_CARDS.length + PLACEHOLDER_ACTION_CARDS.length
    );
  });
});
