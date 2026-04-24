import { describe, expect, it, vi } from 'vitest';

import { buildToolkitConnections, buildToolkitNavItems } from '../buildToolkitNavItems';

describe('buildToolkitConnections', () => {
  const handlers = {
    onToolsClick: vi.fn(),
    onDecksClick: vi.fn(),
    onPhasesClick: vi.fn(),
    onHistoryClick: vi.fn(),
  };

  it('returns 4 connection items in canonical order: Tools, Decks, Phases, Storico', () => {
    const items = buildToolkitConnections(
      { toolCount: 6, deckCount: 2, phaseCount: 4, useCount: 18 },
      handlers
    );
    expect(items.map(i => i.label)).toEqual(['Tools', 'Decks', 'Phases', 'Storico']);
  });

  it('shows all 4 counts', () => {
    const items = buildToolkitConnections(
      { toolCount: 6, deckCount: 2, phaseCount: 4, useCount: 18 },
      handlers
    );
    expect(items[0].count).toBe(6);
    expect(items[1].count).toBe(2);
    expect(items[2].count).toBe(4);
    expect(items[3].count).toBe(18);
  });

  it('hides counts when 0', () => {
    const items = buildToolkitConnections(
      { toolCount: 0, deckCount: 0, phaseCount: 0, useCount: 0 },
      handlers
    );
    items.forEach(item => expect(item.count).toBeUndefined());
  });

  it('returns ConnectionChipProps shape with entityType (not entity)', () => {
    const items = buildToolkitConnections(
      { toolCount: 1, deckCount: 1, phaseCount: 1, useCount: 1 },
      handlers
    );
    expect(items[0].entityType).toBe('tool');
    expect(items[1].entityType).toBe('toolkit');
    expect(items[2].entityType).toBe('toolkit');
    expect(items[3].entityType).toBe('session');
  });

  it('deprecated alias buildToolkitNavItems still works', () => {
    expect(buildToolkitNavItems).toBe(buildToolkitConnections);
  });
});
