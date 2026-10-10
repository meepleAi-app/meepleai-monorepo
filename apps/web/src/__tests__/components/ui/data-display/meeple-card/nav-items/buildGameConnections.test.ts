import { describe, expect, it } from 'vitest';
import { buildGameConnections } from '@/components/ui/data-display/meeple-card/nav-items/buildGameConnections';

const counts = { kbCount: 3, chatCount: 0, sessionCount: 5 };
const handlers = {};

describe('buildGameConnections', () => {
  // Issue #4138: the Agent slot is gone (one system agent, ADR-094).
  it('returns 3 items always', () => {
    expect(buildGameConnections(counts, handlers).length).toBe(3);
  });

  it('sets href on each item when gameId provided', () => {
    const items = buildGameConnections(counts, handlers, 'abc123');
    expect(items.find(i => i.entityType === 'kb')?.href).toBe('/games/abc123/kb');
    expect(items.find(i => i.entityType === 'session')?.href).toBe('/games/abc123/sessions');
    // ADR-061: /games/[id]/chat was deleted (orphan route). Chat slot now
    // redirects to chat creation flow with game pre-selected via query.
    expect(items.find(i => i.entityType === 'chat')?.href).toBe('/chat/new?gameId=abc123');
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
