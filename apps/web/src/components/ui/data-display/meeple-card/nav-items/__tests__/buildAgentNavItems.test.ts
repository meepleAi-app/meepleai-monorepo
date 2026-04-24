import { describe, expect, it, vi } from 'vitest';

import { buildAgentConnections } from '../buildAgentNavItems';

describe('buildAgentConnections', () => {
  const handlers = {
    onChatClick: vi.fn(),
    onKbClick: vi.fn(),
    onMemoryClick: vi.fn(),
    onConfigClick: vi.fn(),
  };

  it('returns 4 connection items in canonical order: Chat, KB, Memorie, Config', () => {
    const items = buildAgentConnections({ chatCount: 3, kbCount: 12 }, handlers);
    expect(items).toHaveLength(4);
    expect(items.map(i => i.label)).toEqual(['Chat', 'KB', 'Memorie', 'Config']);
  });

  it('emits the canonical entityType per slot (chat/kb/agent/agent)', () => {
    const items = buildAgentConnections({ chatCount: 3, kbCount: 12 }, handlers);
    expect(items.map(i => i.entityType)).toEqual(['chat', 'kb', 'agent', 'agent']);
  });

  it('shows chat and kb counts when greater than 0', () => {
    const items = buildAgentConnections({ chatCount: 5, kbCount: 8 }, handlers);
    expect(items[0].count).toBe(5);
    expect(items[1].count).toBe(8);
  });

  it('hides count when value is 0', () => {
    const items = buildAgentConnections({ chatCount: 0, kbCount: 0 }, handlers);
    expect(items[0].count).toBeUndefined();
    expect(items[1].count).toBeUndefined();
  });

  it('Memorie slot is disabled when no handler (v1 default)', () => {
    const items = buildAgentConnections(
      { chatCount: 0, kbCount: 0 },
      { onChatClick: vi.fn(), onKbClick: vi.fn(), onConfigClick: vi.fn() }
    );
    expect(items[2].disabled).toBe(true);
  });
});
