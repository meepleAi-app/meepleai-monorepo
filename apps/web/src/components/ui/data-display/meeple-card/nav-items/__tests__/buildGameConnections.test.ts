import { describe, expect, it, vi } from 'vitest';

import { buildGameConnections } from '../buildGameConnections';

describe('buildGameConnections', () => {
  const handlers = {
    onKbClick: vi.fn(),
    onChatClick: vi.fn(),
    onSessionClick: vi.fn(),
    onKbPlus: vi.fn(),
    onChatPlus: vi.fn(),
    onSessionPlus: vi.fn(),
  };

  it('returns 3 connection items in fixed order: KB, Chat, Sessioni', () => {
    const items = buildGameConnections({ kbCount: 3, chatCount: 5, sessionCount: 12 }, handlers);
    expect(items).toHaveLength(3);
    expect(items.map(i => i.label)).toEqual(['KB', 'Chat', 'Sessioni']);
  });

  // Issue #4138: the Agent slot showed the same system agent on every game card.
  it('has no agent slot', () => {
    const items = buildGameConnections({ kbCount: 1, chatCount: 1, sessionCount: 1 }, handlers);
    expect(items.some(i => i.entityType === 'agent')).toBe(false);
  });

  it('shows count when greater than 0', () => {
    const items = buildGameConnections({ kbCount: 3, chatCount: 0, sessionCount: 0 }, handlers);
    expect(items[0].count).toBe(3);
    expect(items[1].count).toBeUndefined();
    expect(items[2].count).toBeUndefined();
  });

  it('wires onCreate handler when count is 0', () => {
    const items = buildGameConnections({ kbCount: 0, chatCount: 0, sessionCount: 0 }, handlers);
    expect(items.every(i => typeof i.onCreate === 'function')).toBe(true);
  });

  it('leaves onCreate undefined when count is greater than 0', () => {
    const items = buildGameConnections({ kbCount: 5, chatCount: 1, sessionCount: 1 }, handlers);
    expect(items.every(i => i.onCreate === undefined)).toBe(true);
  });

  it('routes onClick to the correct handler per slot', () => {
    const items = buildGameConnections({ kbCount: 1, chatCount: 1, sessionCount: 1 }, handlers);
    items[0].onClick?.();
    expect(handlers.onKbClick).toHaveBeenCalledOnce();
    items[1].onClick?.();
    expect(handlers.onChatClick).toHaveBeenCalledOnce();
    items[2].onClick?.();
    expect(handlers.onSessionClick).toHaveBeenCalledOnce();
  });

  it('marks slot as disabled when no click handler is provided', () => {
    const items = buildGameConnections(
      { kbCount: 1, chatCount: 0, sessionCount: 0 },
      { onKbClick: vi.fn() }
    );
    expect(items[0].disabled).toBeFalsy();
    expect(items[1].disabled).toBe(true);
    expect(items[2].disabled).toBe(true);
  });

  it('uses entity colors per slot (kb, chat, session)', () => {
    const items = buildGameConnections({ kbCount: 1, chatCount: 1, sessionCount: 1 }, handlers);
    expect(items[0].entityType).toBe('kb');
    expect(items[1].entityType).toBe('chat');
    expect(items[2].entityType).toBe('session');
  });
});
