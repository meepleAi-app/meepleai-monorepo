import { describe, expect, it, vi } from 'vitest';

import { buildAgentNavItems } from '../buildAgentNavItems';

describe('buildAgentNavItems', () => {
  const handlers = {
    onChatClick: vi.fn(),
    onKbClick: vi.fn(),
    onMemoryClick: vi.fn(),
    onConfigClick: vi.fn(),
  };

  it('returns 4 nav items in canonical order: Chat, KB, Memorie, Config', () => {
    const items = buildAgentNavItems({ chatCount: 3, kbCount: 12 }, handlers);
    expect(items.map(i => i.label)).toEqual(['Chat', 'KB', 'Memorie', 'Config']);
  });

  it('shows chat and kb counts when greater than 0', () => {
    const items = buildAgentNavItems({ chatCount: 5, kbCount: 8 }, handlers);
    expect(items[0].count).toBe(5);
    expect(items[1].count).toBe(8);
  });

  it('Memorie slot is disabled when no handler (v1 default)', () => {
    const items = buildAgentNavItems(
      { chatCount: 0, kbCount: 0 },
      { onChatClick: vi.fn(), onKbClick: vi.fn(), onConfigClick: vi.fn() }
    );
    expect(items[2].disabled).toBe(true);
  });
});
