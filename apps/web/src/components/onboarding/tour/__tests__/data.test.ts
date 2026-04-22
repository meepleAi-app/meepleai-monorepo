import { describe, expect, it } from 'vitest';

import { ACTIONS, AGENTS, GAMES, MIN_SELECTED, STEP_ENTITIES, STEP_LABELS } from '../data';

describe('onboarding tour data', () => {
  it('GAMES has 8 entries with required fields', () => {
    expect(GAMES).toHaveLength(8);
    for (const g of GAMES) {
      expect(g.id).toMatch(/^[a-z0-9-]+$/);
      expect(g.title).toBeTruthy();
      expect(typeof g.year).toBe('number');
      expect(g.players).toMatch(/\d/);
      expect(g.emoji).toBeTruthy();
      expect(g.gradient).toHaveLength(2);
    }
  });

  it('AGENTS has 4 entries, 3 defaultOn', () => {
    expect(AGENTS).toHaveLength(4);
    expect(AGENTS.filter(a => a.defaultOn)).toHaveLength(3);
  });

  it('ACTIONS map to existing authenticated routes', () => {
    const hrefs = ACTIONS.map(a => a.href);
    expect(hrefs).toEqual(['/game-nights', '/library', '/agents']);
  });

  it('MIN_SELECTED is 3', () => {
    expect(MIN_SELECTED).toBe(3);
  });

  it('STEP_LABELS and STEP_ENTITIES align', () => {
    expect(STEP_LABELS).toEqual(['Giochi', 'Agenti', 'Sessione']);
    expect(STEP_ENTITIES).toEqual(['game', 'agent', 'session']);
  });
});
