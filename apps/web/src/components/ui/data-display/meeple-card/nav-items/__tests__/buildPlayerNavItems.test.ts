import { describe, expect, it, vi } from 'vitest';

import { buildPlayerNavItems } from '../buildPlayerNavItems';

describe('buildPlayerNavItems', () => {
  const handlers = {
    onWinsClick: vi.fn(),
    onSessionsClick: vi.fn(),
    onFavoritesClick: vi.fn(),
    onAchievementsClick: vi.fn(),
  };

  it('returns 4 nav items in fixed order: Vittorie, Partite, Preferiti, Trofei', () => {
    const items = buildPlayerNavItems({ totalWins: 10, totalSessions: 30 }, handlers);
    expect(items).toHaveLength(4);
    expect(items.map(i => i.label)).toEqual(['Vittorie', 'Partite', 'Preferiti', 'Trofei']);
  });

  it('shows wins and sessions counts', () => {
    const items = buildPlayerNavItems({ totalWins: 18, totalSessions: 42 }, handlers);
    expect(items[0].count).toBe(18);
    expect(items[1].count).toBe(42);
  });

  it('marks Preferiti slot disabled when favoriteCount is undefined (v1 default)', () => {
    const items = buildPlayerNavItems({ totalWins: 1, totalSessions: 1 }, handlers);
    expect(items[2].disabled).toBe(true);
  });

  it('marks Trofei slot disabled when achievementCount is undefined (v1 default)', () => {
    const items = buildPlayerNavItems({ totalWins: 1, totalSessions: 1 }, handlers);
    expect(items[3].disabled).toBe(true);
  });

  it('hides count when value is 0', () => {
    const items = buildPlayerNavItems({ totalWins: 0, totalSessions: 0 }, handlers);
    expect(items[0].count).toBeUndefined();
    expect(items[1].count).toBeUndefined();
  });
});
