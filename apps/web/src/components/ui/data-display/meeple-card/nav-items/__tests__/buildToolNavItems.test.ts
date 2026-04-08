import { describe, expect, it, vi } from 'vitest';

import { buildToolNavItems } from '../buildToolNavItems';

describe('buildToolNavItems', () => {
  const handlers = {
    onUseClick: vi.fn(),
    onEditClick: vi.fn(),
    onDuplicateClick: vi.fn(),
    onHistoryClick: vi.fn(),
  };

  it('returns 4 action items in canonical order: Usa, Modifica, Duplica, Storico', () => {
    const items = buildToolNavItems(handlers);
    expect(items.map(i => i.label)).toEqual(['Usa', 'Modifica', 'Duplica', 'Storico']);
  });

  it('all slots are pure action containers (no counts)', () => {
    const items = buildToolNavItems(handlers);
    items.forEach(item => expect(item.count).toBeUndefined());
  });

  it('routes click handlers correctly', () => {
    const items = buildToolNavItems(handlers);
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
    const items = buildToolNavItems({ onUseClick: vi.fn() });
    expect(items[0].disabled).toBeFalsy();
    expect(items[1].disabled).toBe(true);
    expect(items[2].disabled).toBe(true);
    expect(items[3].disabled).toBe(true);
  });
});
