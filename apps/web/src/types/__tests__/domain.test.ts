/**
 * Tests for domain types
 * Issue #1951: Add type validation tests for coverage
 */

import type {
  Game,
  RuleSpec,
  RuleAtom,
  Session,
  Agent,
  SessionStatus,
  GameSessionDto,
} from '../domain';

describe('Domain Types', () => {
  describe('Game Type', () => {
    it('validates Game structure', () => {
      const game: Game = {
        id: 'game-1',
        title: 'Chess',
        createdAt: '2024-01-01T00:00:00Z',
      };

      expect(game.id).toBe('game-1');
      expect(game.title).toBe('Chess');
      expect(game.createdAt).toBeDefined();
    });
  });

  describe('RuleAtom Type', () => {
    it('validates RuleAtom structure', () => {
      const atom: RuleAtom = {
        id: 'atom-1',
        text: 'Rule text',
        section: null,
        page: null,
        line: null,
      };

      expect(atom.id).toBe('atom-1');
      expect(atom.text).toBe('Rule text');
    });

    it('allows optional fields', () => {
      const atom: RuleAtom = {
        id: 'atom-1',
        text: 'Rule text',
        section: 'Setup',
        page: '5',
        line: '10',
      };

      expect(atom.section).toBe('Setup');
      expect(atom.page).toBe('5');
    });
  });

  describe('RuleSpec Type', () => {
    it('validates RuleSpec structure', () => {
      const spec: RuleSpec = {
        gameId: 'game-1',
        version: 'v1',
        createdAt: '2024-01-01T00:00:00Z',
        rules: [],
      };

      expect(spec.gameId).toBe('game-1');
      expect(spec.rules).toEqual([]);
    });
  });

  describe('Session Type', () => {
    it('validates Session structure', () => {
      const session: Session = {
        id: 'session-1',
        userId: 'user-1',
        gameId: 'game-1',
        createdAt: '2024-01-01T00:00:00Z',
        expiresAt: '2024-01-01T01:00:00Z',
      };

      expect(session.id).toBe('session-1');
      expect(session.userId).toBe('user-1');
    });
  });

  describe('Agent Type', () => {
    it('validates Agent structure', () => {
      const agent: Agent = {
        id: 'agent-1',
        gameId: 'game-1',
        name: 'QA Agent',
        type: 'qa',
        createdAt: '2024-01-01T00:00:00Z',
      };

      expect(agent.type).toBe('qa');
      expect(agent.name).toBe('QA Agent');
    });
  });

  describe('SessionStatus Type', () => {
    it('validates SessionStatus structure', () => {
      const status: SessionStatus = {
        expiresAt: '2024-01-01T01:00:00Z',
        lastSeenAt: '2024-01-01T00:30:00Z',
        remainingMinutes: 30,
      };

      expect(status.remainingMinutes).toBe(30);
    });

    it('allows null lastSeenAt', () => {
      const status: SessionStatus = {
        expiresAt: '2024-01-01T01:00:00Z',
        lastSeenAt: null,
        remainingMinutes: 60,
      };

      expect(status.lastSeenAt).toBeNull();
    });
  });

  describe('GameSessionDto Type', () => {
    it('validates GameSessionDto structure', () => {
      const dto: GameSessionDto = {
        id: 'session-1',
        gameId: 'game-1',
        userId: 'user-1',
        createdAt: '2024-01-01T00:00:00Z',
      };

      expect(dto.id).toBe('session-1');
      expect(dto.gameId).toBe('game-1');
    });
  });
});
