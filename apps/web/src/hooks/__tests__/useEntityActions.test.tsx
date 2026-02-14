/**
 * useEntityActions Tests
 * Issue #4033 - Comprehensive Testing
 */

import { renderHook } from '@testing-library/react';
import { useRouter } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';

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
    it('returns 3 quick actions for game', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123' })
      );

      expect(result.current.quickActions).toHaveLength(3);
      expect(result.current.quickActions[0].label).toBe('Chat con Agent');
      expect(result.current.quickActions[1].label).toBe('Avvia Sessione');
      expect(result.current.quickActions[2].label).toBe('Condividi');
    });

    it('navigates to chat when Chat is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123' })
      );

      result.current.quickActions[0].onClick();
      expect(mockPush).toHaveBeenCalledWith('/chat?gameId=game-123');
    });

    it('navigates to new session when Avvia Sessione is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'game', id: 'game-123' })
      );

      result.current.quickActions[1].onClick();
      expect(mockPush).toHaveBeenCalledWith('/sessions/new?gameId=game-123');
    });
  });

  describe('Session entity', () => {
    it('returns 3 quick actions for session', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'session', id: 'session-456' })
      );

      expect(result.current.quickActions).toHaveLength(3);
      expect(result.current.quickActions[0].label).toBe('Riprendi');
      expect(result.current.quickActions[1].label).toBe('Usa Toolkit');
      expect(result.current.quickActions[2].label).toBe('Condividi codice');
    });

    it('navigates to session detail when Riprendi is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'session', id: 'session-456' })
      );

      result.current.quickActions[0].onClick();
      expect(mockPush).toHaveBeenCalledWith('/sessions/session-456');
    });
  });

  describe('Agent entity', () => {
    it('returns 2 quick actions for agent', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'agent', id: 'agent-789' })
      );

      expect(result.current.quickActions).toHaveLength(2);
      expect(result.current.quickActions[0].label).toBe('Chat');
      expect(result.current.quickActions[1].label).toBe('Statistiche');
    });

    it('navigates to agent stats when Statistiche is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'agent', id: 'agent-789' })
      );

      result.current.quickActions[1].onClick();
      expect(mockPush).toHaveBeenCalledWith('/agents/agent-789?tab=stats');
    });
  });

  describe('Document entity', () => {
    it('returns 2 quick actions for document', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'document', id: 'doc-111' })
      );

      expect(result.current.quickActions).toHaveLength(2);
      expect(result.current.quickActions[0].label).toBe('Download');
      expect(result.current.quickActions[1].label).toBe('Chat sui contenuti');
    });
  });

  describe('ChatSession entity', () => {
    it('returns 2 quick actions for chatSession', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'chatSession', id: 'chat-222' })
      );

      expect(result.current.quickActions).toHaveLength(2);
      expect(result.current.quickActions[0].label).toBe('Continua Chat');
      expect(result.current.quickActions[1].label).toBe('Esporta');
    });

    it('navigates to chat when Continua Chat is clicked', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'chatSession', id: 'chat-222' })
      );

      result.current.quickActions[0].onClick();
      expect(mockPush).toHaveBeenCalledWith('/chat/chat-222');
    });
  });

  describe('Player entity', () => {
    it('returns 2 quick actions for player', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'player', id: 'player-333' })
      );

      expect(result.current.quickActions).toHaveLength(2);
      expect(result.current.quickActions[0].label).toBe('Messaggia');
      expect(result.current.quickActions[1].label).toBe('Invita a Sessione');
    });
  });

  describe('Event entity', () => {
    it('returns 2 quick actions for event', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'event', id: 'event-444' })
      );

      expect(result.current.quickActions).toHaveLength(2);
      expect(result.current.quickActions[0].label).toBe('Partecipa');
      expect(result.current.quickActions[1].label).toBe('Condividi');
    });
  });

  describe('Custom entity', () => {
    it('returns empty actions for custom entity', () => {
      const { result } = renderHook(() =>
        useEntityActions({ entity: 'custom', id: 'custom-555' })
      );

      expect(result.current.quickActions).toHaveLength(0);
    });
  });
});
