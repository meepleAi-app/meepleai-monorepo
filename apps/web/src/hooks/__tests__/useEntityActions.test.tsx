/**
 * useEntityActions Tests
 * Issue #4033 - Comprehensive Testing
 */

import { renderHook } from '@testing-library/react';
import { useRouter } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { QueryClientWrapper } from '@/__tests__/utils/query-client-wrapper';
import { useEntityActions } from '../use-entity-actions';

// Mock Next.js router
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
}));

describe('useEntityActions', () => {
  const mockPush = vi.fn();
  const mockRouter = { push: mockPush };

  beforeEach(() => {
    vi.clearAllMocks();
    (useRouter as ReturnType<typeof vi.fn>).mockReturnValue(mockRouter);
  });

  describe('Game entity', () => {
    it('returns 4 quick actions for game (including collection action)', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123' }),
        { wrapper: QueryClientWrapper }
      );

      expect(result.current.quickActions).toHaveLength(4);
      expect(result.current.quickActions[0].label).toBe('Aggiungi a Collezione');
      expect(result.current.quickActions[1].label).toBe('Chat con Agent');
      expect(result.current.quickActions[2].label).toBe('Avvia Sessione');
      expect(result.current.quickActions[3].label).toBe('Condividi');
    });

    it('navigates to chat when Chat is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123' }),
        { wrapper: QueryClientWrapper }
      );

      result.current.quickActions[1].onClick();
      expect(mockPush).toHaveBeenCalledWith('/chat/new?game=game-123');
    });

    it('navigates to new session when Avvia Sessione is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123' }),
        { wrapper: QueryClientWrapper }
      );

      result.current.quickActions[2].onClick();
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
    it('returns 3 quick actions for document (including collection action)', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'document', id: 'doc-111' }),
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
