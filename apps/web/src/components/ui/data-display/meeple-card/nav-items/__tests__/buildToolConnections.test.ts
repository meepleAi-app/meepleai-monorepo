import { describe, expect, it, vi } from 'vitest';

import { buildToolConnections } from '../buildToolConnections';

describe('buildToolConnections', () => {
  const handlers = {
    onUseClick: vi.fn(),
    onEditClick: vi.fn(),
    onDuplicateClick: vi.fn(),
    onHistoryClick: vi.fn(),
  };

  it('returns 4 action items in canonical order: Usa, Modifica, Duplica, Storico', () => {
    const items = buildToolConnections(handlers);
    expect(items.map(i => i.label)).toEqual(['Usa', 'Modifica', 'Duplica', 'Storico']);
  });

  it('all slots are pure action containers (no counts)', () => {
    const items = buildToolConnections(handlers);
    items.forEach(item => expect(item.count).toBeUndefined());
  });

  it('routes click handlers correctly', () => {
    const items = buildToolConnections(handlers);
    items[0].onClick?.();
    expect(handlers.onUseClick).toHaveBeenCalledOnce();
    items[1].onClick?.();
    expect(handlers.onEditClick).toHaveBeenCalledOnce();
    items[2].onClick?.();
    expect(handlers.onDuplicateClick).toHaveBeenCalledOnce();
    items[3].onClick?.();
    expect(handlers.onHistoryClick).toHaveBeenCalledOnce();
  });

  it('disables slot when handler is missing', () => {
    const items = buildToolConnections({ onUseClick: vi.fn() });
    expect(items[0].disabled).toBeFalsy();
    expect(items[1].disabled).toBe(true);
    expect(items[2].disabled).toBe(true);
    expect(items[3].disabled).toBe(true);
  });

  it('returns ConnectionChipProps shape with entityType (not entity)', () => {
    const items = buildToolConnections(handlers);
    items.forEach(item => expect(item.entityType).toBe('tool'));
  });
});
