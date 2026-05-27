import { describe, expect, it } from 'vitest';

import { getSectionsForScope } from '../sections';

describe('getSectionsForScope', () => {
  it('returns 5 sections for game scope (states, withKb, rating, players, year)', () => {
    const sections = getSectionsForScope('game');
    expect(sections.map(s => s.key)).toEqual(['states', 'withKb', 'rating', 'players', 'year']);
  });

  it('game.states is a checkbox group with the 3 user-facing states (Owned/Wishlist/InPrestito)', () => {
    const sections = getSectionsForScope('game');
    const states = sections.find(s => s.key === 'states');
    expect(states?.kind).toBe('checkbox-group');
    expect(states && 'options' in states ? states.options.map(o => o.value) : []).toEqual([
      'Owned',
      'Wishlist',
      'InPrestito',
    ]);
  });

  it('game.rating is a single-slider 0-10', () => {
    const sections = getSectionsForScope('game');
    const rating = sections.find(s => s.key === 'rating');
    expect(rating?.kind).toBe('slider');
    if (rating && rating.kind === 'slider') {
      expect(rating.min).toBe(0);
      expect(rating.max).toBe(10);
    }
  });

  it('game.players is a range with min=1 max=10', () => {
    const sections = getSectionsForScope('game');
    const players = sections.find(s => s.key === 'players');
    expect(players?.kind).toBe('range');
    if (players && players.kind === 'range') {
      expect(players.min).toBe(1);
      expect(players.max).toBe(10);
    }
  });

  it('returns 2 sections for agent scope (types, activeOnly)', () => {
    const sections = getSectionsForScope('agent');
    expect(sections.map(s => s.key)).toEqual(['types', 'activeOnly']);
    expect(sections[0]?.kind).toBe('checkbox-group');
    expect(sections[1]?.kind).toBe('toggle');
  });

  it('returns 3 sections for session scope (statuses, sessionTypes, playerCount)', () => {
    const sections = getSectionsForScope('session');
    expect(sections.map(s => s.key)).toEqual(['statuses', 'sessionTypes', 'playerCount']);
  });

  it('returns 1 section for kb scope (processingStates) with 3 fixed options Ready/Pending/Failed', () => {
    const sections = getSectionsForScope('kb');
    expect(sections.map(s => s.key)).toEqual(['processingStates']);
    const ps = sections[0];
    expect(ps && ps.kind === 'checkbox-group' ? ps.options.map(o => o.value) : []).toEqual([
      'Ready',
      'Pending',
      'Failed',
    ]);
  });

  it('returns 1 section for chat scope (messageCountMin slider min=0)', () => {
    const sections = getSectionsForScope('chat');
    expect(sections.map(s => s.key)).toEqual(['messageCountMin']);
    const mc = sections[0];
    expect(mc?.kind).toBe('slider');
  });

  it('returns a new array reference each call (pure, no shared state)', () => {
    const a = getSectionsForScope('game');
    const b = getSectionsForScope('game');
    expect(a).not.toBe(b);
    expect(a).toEqual(b);
  });
});
