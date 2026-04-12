import { describe, expect, it, vi } from 'vitest';

import { buildSessionNavItems } from '../buildSessionNavItems';

describe('buildSessionNavItems', () => {
  const handlers = {
    onPlayersClick: vi.fn(),
    onNotesClick: vi.fn(),
    onToolsClick: vi.fn(),
    onPhotosClick: vi.fn(),
  };

  it('returns 4 nav items in canonical order: Giocatori, Note, Tools, Foto', () => {
    const items = buildSessionNavItems(
      { playerCount: 4, hasNotes: true, toolCount: 2, photoCount: 3 },
      handlers
    );
    expect(items).toHaveLength(4);
    expect(items.map(i => i.label)).toEqual(['Giocatori', 'Note', 'Tools', 'Foto']);
  });

  it('shows player count when greater than 0', () => {
    const items = buildSessionNavItems(
      { playerCount: 4, hasNotes: false, toolCount: 0, photoCount: 0 },
      handlers
    );
    expect(items[0].count).toBe(4);
  });

  it('uses presence indicator (1) for hasNotes', () => {
    const items = buildSessionNavItems(
      { playerCount: 0, hasNotes: true, toolCount: 0, photoCount: 0 },
      handlers
    );
    expect(items[1].count).toBe(1);
  });

  it('hides notes count when hasNotes is false', () => {
    const items = buildSessionNavItems(
      { playerCount: 0, hasNotes: false, toolCount: 0, photoCount: 0 },
      handlers
    );
    expect(items[1].count).toBeUndefined();
  });

  it('shows photoCount when greater than 0', () => {
    const items = buildSessionNavItems(
      { playerCount: 0, hasNotes: false, toolCount: 0, photoCount: 12 },
      handlers
    );
    expect(items[3].count).toBe(12);
  });
});
