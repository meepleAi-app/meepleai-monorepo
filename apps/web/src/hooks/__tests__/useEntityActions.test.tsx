/**
 * useEntityActions Tests
 * Issue #4033 - Comprehensive Testing
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { QueryClientWrapper } from '@/__tests__/utils/query-client-wrapper';
import { useEntityActions } from '../use-entity-actions';

const mockPush = vi.fn();

// Mock Next.js router
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: vi.fn(),
    prefetch: vi.fn(),
  }),
}));

describe('useEntityActions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Game entity', () => {
    it('returns 5 quick actions for game (including collection + agent)', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123' }),
        { wrapper: QueryClientWrapper }
      );

      expect(result.current.quickActions).toHaveLength(5);
      expect(result.current.quickActions[0].label).toBe('Aggiungi a Collezione');
      expect(result.current.quickActions[1].label).toBe('Crea Agente');
      expect(result.current.quickActions[2].label).toBe('Chat con Agent');
      expect(result.current.quickActions[3].label).toBe('Avvia Sessione');
      expect(result.current.quickActions[4].label).toBe('Condividi');
    });

    it('hides Crea Agente when onCreateAgent is not provided', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123' }),
        { wrapper: QueryClientWrapper }
      );

      const agentAction = result.current.quickActions.find(a => a.label === 'Crea Agente');
      expect(agentAction?.hidden).toBe(true);
    });

    it('shows Crea Agente when onCreateAgent is provided', () => {
      const mockOnCreateAgent = vi.fn();
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123', onCreateAgent: mockOnCreateAgent }),
        { wrapper: QueryClientWrapper }
      );

      const agentAction = result.current.quickActions.find(a => a.label === 'Crea Agente');
      expect(agentAction?.hidden).toBe(false);
    });

    it('hides Crea Agente when game already has an agent', () => {
      const mockOnCreateAgent = vi.fn();
      const { result } = renderHook(() =>
        useEntityActions({
          entity: 'game',
          id: 'game-123',
          onCreateAgent: mockOnCreateAgent,
          data: { hasAgent: true },
        }),
        { wrapper: QueryClientWrapper }
      );

      const agentAction = result.current.quickActions.find(a => a.label === 'Crea Agente');
      expect(agentAction?.hidden).toBe(true);
    });

    it('calls onCreateAgent when Crea Agente is clicked', () => {
      const mockOnCreateAgent = vi.fn();
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123', onCreateAgent: mockOnCreateAgent }),
        { wrapper: QueryClientWrapper }
      );

      const agentAction = result.current.quickActions.find(a => a.label === 'Crea Agente');
      agentAction?.onClick();
      expect(mockOnCreateAgent).toHaveBeenCalled();
    });

    it('navigates to chat when Chat is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123' }),
        { wrapper: QueryClientWrapper }
      );

      result.current.quickActions[2].onClick();
      expect(mockPush).toHaveBeenCalledWith('/chat/new?game=game-123');
    });

    it('navigates to new session when Avvia Sessione is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123', userId: 'user-123' }),
        { wrapper: QueryClientWrapper }
      );

      result.current.quickActions[3].onClick();
      expect(mockPush).toHaveBeenCalledWith('/sessions/new?gameId=game-123');
    });
  });

  describe('Session entity', () => {
    it('returns 4 quick actions for session (including collection action)', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'session', id: 'session-456' }),
        { wrapper: QueryClientWrapper }
      );

      expect(result.current.quickActions).toHaveLength(4);
      expect(result.current.quickActions[0].label).toBe('Salva');
      expect(result.current.quickActions[1].label).toBe('Riprendi');
      expect(result.current.quickActions[2].label).toBe('Usa Toolkit');
      expect(result.current.quickActions[3].label).toBe('Condividi codice');
    });

    it('navigates to session detail when Riprendi is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'session', id: 'session-456' }),
        { wrapper: QueryClientWrapper }
      );

      result.current.quickActions[1].onClick();
      expect(mockPush).toHaveBeenCalledWith('/sessions/session-456');
    });
  });

  describe('Agent entity', () => {
    it('returns 3 quick actions for agent (including collection action)', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'agent', id: 'agent-789' }),
        { wrapper: QueryClientWrapper }
      );

      expect(result.current.quickActions).toHaveLength(3);
      expect(result.current.quickActions[0].label).toBe('Aggiungi ai Preferiti');
      expect(result.current.quickActions[1].label).toBe('Chat');
      expect(result.current.quickActions[2].label).toBe('Statistiche');
    });

    it('navigates to agent stats when Statistiche is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'agent', id: 'agent-789' }),
        { wrapper: QueryClientWrapper }
      );

      result.current.quickActions[2].onClick();
      expect(mockPush).toHaveBeenCalledWith('/agents/agent-789?tab=stats');
    });
  });

  describe('Document entity', () => {
    it('returns 3 quick actions for kb (including collection action)', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'kb', id: 'doc-111' }),
        { wrapper: QueryClientWrapper }
      );

      expect(result.current.quickActions).toHaveLength(3);
      expect(result.current.quickActions[0].label).toBe('Salva');
      expect(result.current.quickActions[1].label).toBe('Download');
      expect(result.current.quickActions[2].label).toBe('Chat sui contenuti');
    });
  });

  describe('ChatSession entity', () => {
    it('returns 3 quick actions for chatSession (including collection action)', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'chatSession', id: 'chat-222' }),
        { wrapper: QueryClientWrapper }
      );

      expect(result.current.quickActions).toHaveLength(3);
      expect(result.current.quickActions[0].label).toBe('Fissa');
      expect(result.current.quickActions[1].label).toBe('Continua Chat');
      expect(result.current.quickActions[2].label).toBe('Esporta');
    });

    it('navigates to chat when Continua Chat is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'chatSession', id: 'chat-222' }),
        { wrapper: QueryClientWrapper }
      );

      result.current.quickActions[1].onClick();
      expect(mockPush).toHaveBeenCalledWith('/chat/chat-222');
    });
  });

  describe('Player entity', () => {
    it('returns 3 quick actions for player (including collection action)', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'player', id: 'player-333' }),
        { wrapper: QueryClientWrapper }
      );

      expect(result.current.quickActions).toHaveLength(3);
      expect(result.current.quickActions[0].label).toBe('Segui');
      expect(result.current.quickActions[1].label).toBe('Messaggia');
      expect(result.current.quickActions[2].label).toBe('Invita a Sessione');
    });
  });

  describe('Event entity', () => {
    it('returns 3 quick actions for event (including collection action)', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'event', id: 'event-444' }),
        { wrapper: QueryClientWrapper }
      );

      expect(result.current.quickActions).toHaveLength(3);
      expect(result.current.quickActions[0].label).toBe('Interessato');
      expect(result.current.quickActions[1].label).toBe('Partecipa');
      expect(result.current.quickActions[2].label).toBe('Condividi');
    });
  });

  describe('Custom entity', () => {
    it('returns empty actions for custom entity', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'custom', id: 'custom-555' }),
        { wrapper: QueryClientWrapper }
      );

      expect(result.current.quickActions).toHaveLength(0);
    });
  });
});
