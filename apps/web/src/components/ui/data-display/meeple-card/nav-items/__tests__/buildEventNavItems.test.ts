import { describe, expect, it, vi } from 'vitest';

import { buildEventNavItems } from '../buildEventNavItems';

describe('buildEventNavItems', () => {
  const handlers = {
    onParticipantsClick: vi.fn(),
    onLocationClick: vi.fn(),
    onGamesClick: vi.fn(),
    onDateClick: vi.fn(),
  };

  it('returns 4 nav items in canonical order: Partecipanti, Luogo, Giochi, Data', () => {
    const items = buildEventNavItems({ participantCount: 8, gameCount: 3 }, handlers);
    expect(items.map(i => i.label)).toEqual(['Partecipanti', 'Luogo', 'Giochi', 'Data']);
  });

  it('shows participant and game counts', () => {
    const items = buildEventNavItems({ participantCount: 12, gameCount: 5 }, handlers);
    expect(items[0].count).toBe(12);
    expect(items[2].count).toBe(5);
  });

  it('Luogo and Data are link slots without counts', () => {
    const items = buildEventNavItems({ participantCount: 1, gameCount: 1 }, handlers);
    expect(items[1].count).toBeUndefined();
    expect(items[3].count).toBeUndefined();
  });
});
