import { describe, expect, it } from 'vitest';

import type { RowItemBase } from '../HorizontalRow';
import { resolveCardHref } from '../resolveCardHref';

const stubItem = (overrides?: Partial<RowItemBase>): RowItemBase => ({
  id: 'entity-id-stub',
  ...overrides,
});

describe('resolveCardHref (#2085 F2/F3)', () => {
  it('maps trending row → /games/{id}', () => {
    expect(resolveCardHref('trending', stubItem({ id: 'g-azul' }))).toBe('/games/g-azul');
  });

  it('maps games row → /games/{id}', () => {
    expect(resolveCardHref('games', stubItem({ id: 'g-catan' }))).toBe('/games/g-catan');
  });

  it('maps agents row → /agents/{id}', () => {
    expect(resolveCardHref('agents', stubItem({ id: 'a-azul-rules' }))).toBe(
      '/agents/a-azul-rules'
    );
  });

  it('maps toolkits row → /toolkits/{id}', () => {
    expect(resolveCardHref('toolkits', stubItem({ id: 'tk-solo' }))).toBe('/toolkits/tk-solo');
  });

  it('maps kbs row → /knowledge-base/{id} (#2085 F3)', () => {
    expect(resolveCardHref('kbs', stubItem({ id: 'kb-azul-ita' }))).toBe(
      '/knowledge-base/kb-azul-ita'
    );
  });

  it('maps people row → /players/{id}', () => {
    expect(resolveCardHref('people', stubItem({ id: 'p-marco' }))).toBe('/players/p-marco');
  });

  it('maps events row → /game-nights/{id}', () => {
    expect(resolveCardHref('events', stubItem({ id: 'gn-friday' }))).toBe('/game-nights/gn-friday');
  });

  it('returns null for unknown rowId (defensive)', () => {
    expect(resolveCardHref('unknown-row', stubItem())).toBeNull();
  });

  it('returns null when item.id is empty (defensive)', () => {
    expect(resolveCardHref('games', stubItem({ id: '' }))).toBeNull();
  });

  it('encodes IDs with special characters for safe URL routing', () => {
    expect(resolveCardHref('games', stubItem({ id: 'g-id/with slash' }))).toBe(
      '/games/g-id%2Fwith%20slash'
    );
  });
});
