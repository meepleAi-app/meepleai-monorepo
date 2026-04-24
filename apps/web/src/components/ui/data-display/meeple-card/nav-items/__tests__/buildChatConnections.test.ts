import { describe, expect, it, vi } from 'vitest';

import { buildChatConnections } from '../buildChatConnections';

describe('buildChatConnections', () => {
  const handlers = {
    onMessagesClick: vi.fn(),
    onSourcesClick: vi.fn(),
    onAgentLinkClick: vi.fn(),
    onArchiveClick: vi.fn(),
  };

  it('returns 4 nav items in canonical order: Messaggi, Sources, Agente, Archivia', () => {
    const items = buildChatConnections({ messageCount: 10 }, handlers);
    expect(items).toHaveLength(4);
    expect(items.map(i => i.label)).toEqual(['Messaggi', 'Sources', 'Agente', 'Archivia']);
  });

  it('shows messageCount when greater than 0', () => {
    const items = buildChatConnections({ messageCount: 18 }, handlers);
    expect(items[0].count).toBe(18);
  });

  it('hides messageCount when 0', () => {
    const items = buildChatConnections({ messageCount: 0 }, handlers);
    expect(items[0].count).toBeUndefined();
  });

  it('Sources slot is disabled when no handler (v1 default)', () => {
    const items = buildChatConnections({ messageCount: 5 }, { onMessagesClick: vi.fn() });
    expect(items[1].label).toBe('Sources');
    expect(items[1].disabled).toBe(true);
  });

  it('routes click handlers to correct slots', () => {
    const items = buildChatConnections({ messageCount: 5 }, handlers);
    items[0].onClick?.();
    expect(handlers.onMessagesClick).toHaveBeenCalledOnce();
    items[2].onClick?.();
    expect(handlers.onAgentLinkClick).toHaveBeenCalledOnce();
    items[3].onClick?.();
    expect(handlers.onArchiveClick).toHaveBeenCalledOnce();
  });
});
