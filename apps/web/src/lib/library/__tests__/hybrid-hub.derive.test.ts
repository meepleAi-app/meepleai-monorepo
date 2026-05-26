import { describe, expect, it } from 'vitest';

import { deriveHybridItems, type HybridHubSources, type HybridHubTab } from '../hybrid-hub.derive';
import type {
  AgentHubItem,
  ChatHubItem,
  GameHubItem,
  KbHubItem,
  SessionHubItem,
} from '../hybrid-hub.types';

function gameItem(overrides: Partial<GameHubItem>): GameHubItem {
  return {
    id: 'g1',
    entity: 'game',
    title: 'Catan',
    subtitle: 'Kosmos',
    updatedAt: '2026-04-01T00:00:00Z',
    href: '/library/g1',
    gameId: 'g1',
    rating: 7.2,
    state: 'Owned',
    ...overrides,
  };
}

function agentItem(overrides: Partial<AgentHubItem>): AgentHubItem {
  return {
    id: 'a1',
    entity: 'agent',
    title: 'Tutor',
    updatedAt: '2026-04-02T00:00:00Z',
    href: '/agents/a1',
    agentType: 'Tutor',
    isActive: true,
    ...overrides,
  };
}

function kbItem(overrides: Partial<KbHubItem>): KbHubItem {
  return {
    id: 'k1',
    entity: 'kb',
    title: 'rules.pdf',
    updatedAt: '2026-04-03T00:00:00Z',
    href: '/knowledge-base/k1',
    processingState: 'Ready',
    ...overrides,
  };
}

function sessionItem(overrides: Partial<SessionHubItem>): SessionHubItem {
  return {
    id: 's1',
    entity: 'session',
    title: 'Session s1',
    updatedAt: '2026-04-04T00:00:00Z',
    href: '/sessions/s1',
    status: 'Completed',
    playerCount: 4,
    ...overrides,
  };
}

function chatItem(overrides: Partial<ChatHubItem>): ChatHubItem {
  return {
    id: 'c1',
    entity: 'chat',
    title: 'Question',
    updatedAt: '2026-04-05T00:00:00Z',
    href: '/chats/c1',
    messageCount: 4,
    ...overrides,
  };
}

function makeSources(overrides: Partial<HybridHubSources> = {}): HybridHubSources {
  return {
    games: [gameItem({})],
    agents: [agentItem({})],
    kb: [kbItem({})],
    sessions: [sessionItem({})],
    chat: [chatItem({})],
    ...overrides,
  };
}

describe('deriveHybridItems — tab filtering', () => {
  it('"all" tab returns the merged union of every source', () => {
    const result = deriveHybridItems(makeSources(), 'all', '', 'recent');
    expect(result).toHaveLength(5);
    expect(new Set(result.map(it => it.entity))).toEqual(
      new Set(['game', 'agent', 'kb', 'session', 'chat'])
    );
  });

  const cases: ReadonlyArray<
    readonly [HybridHubTab, 'game' | 'agent' | 'kb' | 'session' | 'chat']
  > = [
    ['games', 'game'],
    ['agents', 'agent'],
    ['kb', 'kb'],
    ['sessions', 'session'],
    ['chat', 'chat'],
  ];
  it.each(cases)('tab "%s" returns only %s items', (tab, entity) => {
    const result = deriveHybridItems(makeSources(), tab, '', 'recent');
    expect(result).toHaveLength(1);
    expect(result[0]?.entity).toBe(entity);
  });
});

describe('deriveHybridItems — query matching', () => {
  it('empty query returns every item in the tab', () => {
    const result = deriveHybridItems(makeSources(), 'all', '', 'recent');
    expect(result).toHaveLength(5);
  });

  it('matches title (case-insensitive)', () => {
    const sources = makeSources({
      games: [gameItem({ title: 'Catan' }), gameItem({ id: 'g2', title: 'Carcassonne' })],
    });
    const result = deriveHybridItems(sources, 'games', 'cat', 'recent');
    expect(result.map(it => it.id)).toEqual(['g1']);
  });

  it('matches subtitle (case-insensitive)', () => {
    const sources = makeSources({
      games: [
        gameItem({ id: 'g1', subtitle: 'Kosmos' }),
        gameItem({ id: 'g2', subtitle: 'Z-Man' }),
      ],
    });
    const result = deriveHybridItems(sources, 'games', 'z-MAN', 'recent');
    expect(result.map(it => it.id)).toEqual(['g2']);
  });

  it('trims whitespace before matching', () => {
    const result = deriveHybridItems(makeSources(), 'games', '   ', 'recent');
    expect(result).toHaveLength(1);
  });
});

describe('deriveHybridItems — sort', () => {
  it('"recent" sorts by updatedAt descending across entities', () => {
    const sources = makeSources({
      games: [gameItem({ updatedAt: '2026-04-01T00:00:00Z' })],
      agents: [agentItem({ updatedAt: '2026-04-05T00:00:00Z' })],
      kb: [kbItem({ updatedAt: '2026-04-03T00:00:00Z' })],
      sessions: [sessionItem({ updatedAt: '2026-04-02T00:00:00Z' })],
      chat: [chatItem({ updatedAt: '2026-04-04T00:00:00Z' })],
    });
    const result = deriveHybridItems(sources, 'all', '', 'recent');
    expect(result.map(it => it.entity)).toEqual(['agent', 'chat', 'kb', 'session', 'game']);
  });

  it('"title" sorts alphabetically, case-insensitive', () => {
    const sources = makeSources({
      games: [
        gameItem({ id: 'g1', title: 'Carcassonne' }),
        gameItem({ id: 'g2', title: 'azul' }),
        gameItem({ id: 'g3', title: 'Brass' }),
      ],
      agents: [],
      kb: [],
      sessions: [],
      chat: [],
    });
    const result = deriveHybridItems(sources, 'all', '', 'title');
    expect(result.map(it => it.id)).toEqual(['g2', 'g3', 'g1']);
  });

  it('"rating" places game items by rating desc; non-games sink to the bottom', () => {
    const sources = makeSources({
      games: [
        gameItem({ id: 'g1', rating: 7.0 }),
        gameItem({ id: 'g2', rating: 9.0 }),
        gameItem({ id: 'g3', rating: undefined }),
      ],
      agents: [agentItem({})],
      kb: [],
      sessions: [],
      chat: [],
    });
    const result = deriveHybridItems(sources, 'all', '', 'rating');
    expect(result.map(it => it.id)).toEqual(['g2', 'g1', 'g3', 'a1']);
  });

  it('"state" places game items first ordered by state; non-games sink', () => {
    const sources = makeSources({
      games: [
        gameItem({ id: 'g1', state: 'Wishlist' }),
        gameItem({ id: 'g2', state: 'Owned' }),
        gameItem({ id: 'g3', state: 'InPrestito' }),
      ],
      agents: [agentItem({})],
      kb: [],
      sessions: [],
      chat: [],
    });
    const result = deriveHybridItems(sources, 'all', '', 'state');
    expect(result.map(it => it.id)).toEqual(['g2', 'g3', 'g1', 'a1']);
  });
});
