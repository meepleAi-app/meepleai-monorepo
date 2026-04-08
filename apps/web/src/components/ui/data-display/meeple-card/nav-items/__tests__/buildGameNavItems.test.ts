import { describe, expect, it, vi } from 'vitest';

import { buildGameNavItems } from '../buildGameNavItems';

describe('buildGameNavItems', () => {
  const handlers = {
    onKbClick: vi.fn(),
    onAgentClick: vi.fn(),
    onChatClick: vi.fn(),
    onSessionClick: vi.fn(),
    onKbPlus: vi.fn(),
    onAgentPlus: vi.fn(),
    onChatPlus: vi.fn(),
    onSessionPlus: vi.fn(),
  };

  it('returns 4 nav items in fixed order: KB, Agent, Chat, Sessioni', () => {
    const items = buildGameNavItems(
      { kbCount: 3, agentCount: 1, chatCount: 5, sessionCount: 12 },
      handlers
    );
    expect(items).toHaveLength(4);
    expect(items.map(i => i.label)).toEqual(['KB', 'Agent', 'Chat', 'Sessioni']);
  });

  it('shows count when greater than 0', () => {
    const items = buildGameNavItems(
      { kbCount: 3, agentCount: 1, chatCount: 0, sessionCount: 0 },
      handlers
    );
    expect(items[0].count).toBe(3);
    expect(items[1].count).toBe(1);
    expect(items[2].count).toBeUndefined();
    expect(items[3].count).toBeUndefined();
  });

  it('shows plus indicator when count is 0', () => {
    const items = buildGameNavItems(
      { kbCount: 0, agentCount: 0, chatCount: 0, sessionCount: 0 },
      handlers
    );
    expect(items.every(i => i.showPlus)).toBe(true);
  });

  it('hides plus indicator when count is greater than 0', () => {
    const items = buildGameNavItems(
      { kbCount: 5, agentCount: 1, chatCount: 1, sessionCount: 1 },
      handlers
    );
    expect(items.every(i => !i.showPlus)).toBe(true);
  });

  it('routes onClick to the correct handler per slot', () => {
    const items = buildGameNavItems(
      { kbCount: 1, agentCount: 1, chatCount: 1, sessionCount: 1 },
      handlers
    );
    items[0].onClick?.();
    expect(handlers.onKbClick).toHaveBeenCalledOnce();
    items[1].onClick?.();
    expect(handlers.onAgentClick).toHaveBeenCalledOnce();
    items[2].onClick?.();
    expect(handlers.onChatClick).toHaveBeenCalledOnce();
    items[3].onClick?.();
    expect(handlers.onSessionClick).toHaveBeenCalledOnce();
  });

  it('marks slot as disabled when no click handler is provided', () => {
    const items = buildGameNavItems(
      { kbCount: 1, agentCount: 0, chatCount: 0, sessionCount: 0 },
      { onKbClick: vi.fn() }
    );
    expect(items[0].disabled).toBeFalsy();
    expect(items[1].disabled).toBe(true);
    expect(items[2].disabled).toBe(true);
    expect(items[3].disabled).toBe(true);
  });

  it('uses entity colors per slot (kb, agent, chat, session)', () => {
    const items = buildGameNavItems(
      { kbCount: 1, agentCount: 1, chatCount: 1, sessionCount: 1 },
      handlers
    );
    expect(items[0].entity).toBe('kb');
    expect(items[1].entity).toBe('agent');
    expect(items[2].entity).toBe('chat');
    expect(items[3].entity).toBe('session');
  });
});
