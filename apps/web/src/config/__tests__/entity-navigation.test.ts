/**
 * Entity Navigation Graph - Unit Tests
 *
 * @see Issue #4705 - Integration Testing
 * @see Issue #4690 - Navigation Graph Config
 */

import {
  ENTITY_NAVIGATION_GRAPH,
  getNavigationLinks,
  NAVIGABLE_ENTITIES,
  type ResolvedNavigationLink,
} from '../entity-navigation';

describe('entity-navigation', () => {
  describe('ENTITY_NAVIGATION_GRAPH', () => {
    it('defines navigation targets for all expected entity types', () => {
      const expected = [
        'game',
        'agent',
        'kb',
        'session',
        'player',
        'chatSession',
        'event',
        'toolkit',
      ];
      for (const entity of expected) {
        expect(ENTITY_NAVIGATION_GRAPH).toHaveProperty(entity);
      }
    });

    it('game has 4 navigation targets', () => {
      const targets = ENTITY_NAVIGATION_GRAPH.game!;
      expect(targets).toHaveLength(4);
      expect(targets.map(t => t.entity)).toEqual(['kb', 'agent', 'chatSession', 'session']);
    });

    it('agent has 4 navigation targets', () => {
      const targets = ENTITY_NAVIGATION_GRAPH.agent!;
      expect(targets).toHaveLength(4);
      expect(targets.map(t => t.entity)).toEqual(['game', 'kb', 'chatSession', 'session']);
    });

    it('kb has 2 navigation targets', () => {
      const targets = ENTITY_NAVIGATION_GRAPH.kb!;
      expect(targets).toHaveLength(2);
      expect(targets.map(t => t.entity)).toEqual(['game', 'agent']);
    });

    it('session has 4 navigation targets', () => {
      const targets = ENTITY_NAVIGATION_GRAPH.session!;
      expect(targets).toHaveLength(4);
      expect(targets.map(t => t.entity)).toEqual(['game', 'player', 'agent', 'chatSession']);
    });

    it('player has 2 navigation targets', () => {
      const targets = ENTITY_NAVIGATION_GRAPH.player!;
      expect(targets).toHaveLength(2);
      expect(targets.map(t => t.entity)).toEqual(['session', 'game']);
    });

    it('chatSession has 3 navigation targets', () => {
      const targets = ENTITY_NAVIGATION_GRAPH.chatSession!;
      expect(targets).toHaveLength(3);
      expect(targets.map(t => t.entity)).toEqual(['game', 'agent', 'session']);
    });

    it('every target has a buildHref function', () => {
      for (const entity of Object.keys(ENTITY_NAVIGATION_GRAPH)) {
        const targets = ENTITY_NAVIGATION_GRAPH[entity as keyof typeof ENTITY_NAVIGATION_GRAPH]!;
        for (const target of targets) {
          expect(typeof target.buildHref).toBe('function');
        }
      }
    });

    it('every target has a non-empty label', () => {
      for (const entity of Object.keys(ENTITY_NAVIGATION_GRAPH)) {
        const targets = ENTITY_NAVIGATION_GRAPH[entity as keyof typeof ENTITY_NAVIGATION_GRAPH]!;
        for (const target of targets) {
          expect(target.label.length).toBeGreaterThan(0);
        }
      }
    });
  });

  describe('getNavigationLinks', () => {
    it('resolves game links with a valid id', () => {
      const links = getNavigationLinks('game', { id: 'game-123' });
      expect(links).toHaveLength(4);
      expect(links[0]).toEqual<ResolvedNavigationLink>({
        entity: 'kb',
        label: 'KB',
        href: '/library/game-123?tab=agent',
      });
      expect(links[1]).toEqual<ResolvedNavigationLink>({
        entity: 'agent',
        label: 'Agents',
        href: '/library/game-123?tab=agent',
      });
    });

    it('resolves chatSession links with all required ids', () => {
      const links = getNavigationLinks('chatSession', {
        id: 'chat-1',
        gameId: 'game-1',
        agentId: 'agent-1',
        sessionId: 'session-1',
      });
      expect(links).toHaveLength(3);
      expect(links[0].href).toBe('/library/game-1');
      expect(links[1].href).toBe('/agents/agent-1');
      expect(links[2].href).toBe('/sessions/session-1');
    });

    it('omits links when idKey value is undefined', () => {
      const links = getNavigationLinks('chatSession', {
        id: 'chat-1',
        gameId: undefined,
        agentId: undefined,
        sessionId: undefined,
      });
      expect(links).toHaveLength(0);
    });

    it('returns links for event entity type', () => {
      const links = getNavigationLinks('event', { id: '123' });
      expect(links).toHaveLength(2);
      expect(links.map(l => l.entity)).toEqual(['game', 'session']);
    });

    it('returns empty array for unknown entity types', () => {
      expect(getNavigationLinks('custom' as any, { id: '123' })).toEqual([]);
    });

    it('returns empty array when entityData is empty', () => {
      expect(getNavigationLinks('game', {})).toEqual([]);
    });

    it('kb links use idKey for game and agent', () => {
      const links = getNavigationLinks('kb', {
        id: 'doc-1',
        gameId: 'game-1',
        agentId: 'agent-1',
      });
      expect(links).toHaveLength(2);
      expect(links[0].href).toBe('/library/game-1');
      expect(links[1].href).toBe('/agents/agent-1');
    });

    it('partially available ids only produce matching links', () => {
      const links = getNavigationLinks('chatSession', {
        id: 'chat-1',
        gameId: 'game-1',
      });
      expect(links).toHaveLength(1);
      expect(links[0].entity).toBe('game');
    });
  });

  describe('NAVIGABLE_ENTITIES', () => {
    it('contains all entities with graph entries', () => {
      const graphKeys = Object.keys(ENTITY_NAVIGATION_GRAPH);
      expect(NAVIGABLE_ENTITIES).toEqual(expect.arrayContaining(graphKeys));
      expect(NAVIGABLE_ENTITIES.length).toBe(graphKeys.length);
    });
  });

  describe('bidirectional navigation', () => {
    it('game <-> agent', () => {
      expect(ENTITY_NAVIGATION_GRAPH.game!.map(t => t.entity)).toContain('agent');
      expect(ENTITY_NAVIGATION_GRAPH.agent!.map(t => t.entity)).toContain('game');
    });

    it('game <-> kb', () => {
      expect(ENTITY_NAVIGATION_GRAPH.game!.map(t => t.entity)).toContain('kb');
      expect(ENTITY_NAVIGATION_GRAPH.kb!.map(t => t.entity)).toContain('game');
    });

    it('game <-> session', () => {
      expect(ENTITY_NAVIGATION_GRAPH.game!.map(t => t.entity)).toContain('session');
      expect(ENTITY_NAVIGATION_GRAPH.session!.map(t => t.entity)).toContain('game');
    });

    it('session <-> player', () => {
      expect(ENTITY_NAVIGATION_GRAPH.session!.map(t => t.entity)).toContain('player');
      expect(ENTITY_NAVIGATION_GRAPH.player!.map(t => t.entity)).toContain('session');
    });
  });

  describe('URL generation', () => {
    it('all hrefs start with /', () => {
      for (const entity of Object.keys(ENTITY_NAVIGATION_GRAPH)) {
        const links = getNavigationLinks(entity as keyof typeof ENTITY_NAVIGATION_GRAPH, {
          id: 'test-id',
          gameId: 'game-id',
          agentId: 'agent-id',
          sessionId: 'session-id',
        });
        for (const link of links) {
          expect(link.href).toMatch(/^\//);
        }
      }
    });

    it('no hrefs contain undefined or null', () => {
      for (const entity of Object.keys(ENTITY_NAVIGATION_GRAPH)) {
        const links = getNavigationLinks(entity as keyof typeof ENTITY_NAVIGATION_GRAPH, {
          id: 'id',
          gameId: 'gid',
          agentId: 'aid',
          sessionId: 'sid',
        });
        for (const link of links) {
          expect(link.href).not.toContain('undefined');
          expect(link.href).not.toContain('null');
        }
      }
    });
  });
});
