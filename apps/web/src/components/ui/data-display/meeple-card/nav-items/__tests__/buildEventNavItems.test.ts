import { describe, expect, it, vi } from 'vitest';

import { buildEventConnections, buildEventNavItems } from '../buildEventNavItems';

describe('buildEventConnections', () => {
  const handlers = {
    onParticipantsClick: vi.fn(),
    onLocationClick: vi.fn(),
    onGamesClick: vi.fn(),
    onDateClick: vi.fn(),
  };

  it('returns 4 connection items in canonical order: Partecipanti, Luogo, Giochi, Data', () => {
    const items = buildEventConnections({ participantCount: 8, gameCount: 3 }, handlers);
    expect(items.map(i => i.label)).toEqual(['Partecipanti', 'Luogo', 'Giochi', 'Data']);
  });

  it('shows participant and game counts', () => {
    const items = buildEventConnections({ participantCount: 12, gameCount: 5 }, handlers);
    expect(items[0].count).toBe(12);
    expect(items[2].count).toBe(5);
  });

  it('Luogo and Data are link slots without counts', () => {
    const items = buildEventConnections({ participantCount: 1, gameCount: 1 }, handlers);
    expect(items[1].count).toBeUndefined();
    expect(items[3].count).toBeUndefined();
  });

  it('returns ConnectionChipProps shape with entityType (not entity)', () => {
    const items = buildEventConnections({ participantCount: 1, gameCount: 1 }, handlers);
    expect(items[0].entityType).toBe('player');
    expect(items[1].entityType).toBe('event');
    expect(items[2].entityType).toBe('game');
    expect(items[3].entityType).toBe('event');
  });

  it('deprecated alias buildEventNavItems still works', () => {
    expect(buildEventNavItems).toBe(buildEventConnections);
  });
});
