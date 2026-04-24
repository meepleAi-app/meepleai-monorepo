import { describe, expect, it } from 'vitest';
import { buildGameConnections } from '@/components/ui/data-display/meeple-card/nav-items/buildGameNavItems';

const counts = { kbCount: 3, agentCount: 1, chatCount: 0, sessionCount: 5 };
const handlers = {};

describe('buildGameConnections', () => {
  it('returns 4 items always', () => {
    expect(buildGameConnections(counts, handlers).length).toBe(4);
  });

  it('sets href on each item when gameId provided', () => {
    const items = buildGameConnections(counts, handlers, 'abc123');
    expect(items.find(i => i.entityType === 'kb')?.href).toBe('/games/abc123/kb');
    expect(items.find(i => i.entityType === 'session')?.href).toBe('/games/abc123/sessions');
    expect(items.find(i => i.entityType === 'agent')?.href).toBe('/games/abc123/agent');
    expect(items.find(i => i.entityType === 'chat')?.href).toBe('/games/abc123/chat');
  });

  it('href is undefined when no gameId', () => {
    const items = buildGameConnections(counts, handlers);
    expect(items.every(i => i.href === undefined)).toBe(true);
  });

  it('shows count when count > 0', () => {
    const items = buildGameConnections(counts, handlers);
    expect(items.find(i => i.entityType === 'kb')?.count).toBe(3);
    expect(items.find(i => i.entityType === 'session')?.count).toBe(5);
  });

  it('count is undefined when count === 0', () => {
    const items = buildGameConnections(counts, handlers);
    expect(items.find(i => i.entityType === 'chat')?.count).toBeUndefined();
  });
});
