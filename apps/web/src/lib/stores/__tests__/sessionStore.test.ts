/**
 * Session Store Tests (Issue #3163)
 *
 * Tests for Generic Toolkit session state management
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useSessionStore } from '../sessionStore';
import type { Session, Participant, ScoreEntry } from '@/components/session/types';

describe('sessionStore', () => {
  beforeEach(() => {
    // Reset store before each test
    useSessionStore.getState().reset();

    // Mock fetch
    global.fetch = vi.fn();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('createSession', () => {
    it('should create a session successfully', async () => {
      const mockSession: Session = {
        id: 'session-1',
        sessionCode: 'ABC123',
        sessionType: 'Generic',
        sessionDate: new Date(),
        status: 'Active',
        participantCount: 2,
      };

      (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
        ok: true,
        json: async () => mockSession,
      });

      const { result } = renderHook(() => useSessionStore());

      await act(async () => {
        await result.current.createSession({
          participants: [{ displayName: 'Alice' }, { displayName: 'Bob' }],
        });
      });

      expect(result.current.activeSession).toEqual(mockSession);
      expect(result.current.isLoading).toBe(false);
      expect(result.current.error).toBeNull();
    });

    it('should handle creation errors', async () => {
      (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
      });

      const { result } = renderHook(() => useSessionStore());

      await expect(async () => {
        await act(async () => {
          await result.current.createSession({
            participants: [{ displayName: 'Alice' }],
          });
        });
      }).rejects.toThrow();

      expect(result.current.error).toBeTruthy();
      expect(result.current.isLoading).toBe(false);
    });
  });

  describe('joinSession', () => {
    it('should join a session by code', async () => {
      const mockSession: Session = {
        id: 'session-2',
        sessionCode: 'XYZ789',
        sessionType: 'Generic',
        sessionDate: new Date(),
        status: 'Active',
        participantCount: 3,
      };

      (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
        ok: true,
        json: async () => mockSession,
      });

      const { result } = renderHook(() => useSessionStore());

      await act(async () => {
        await result.current.joinSession('XYZ789');
      });

      expect(result.current.activeSession).toEqual(mockSession);
      expect(result.current.isLoading).toBe(false);
    });

    it('should handle invalid session codes (404)', async () => {
      (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
        ok: false,
        status: 404,
        statusText: 'Not Found',
      });

      const { result } = renderHook(() => useSessionStore());

      await expect(async () => {
        await act(async () => {
          await result.current.joinSession('INVALID');
        });
      }).rejects.toThrow('Session not found');

      expect(result.current.error).toContain('Session not found');
    });
  });

  describe('updateScore (Optimistic UI)', () => {
    it('should add optimistic score immediately', async () => {
      const mockSession: Session = {
        id: 'session-3',
        sessionCode: 'TEST01',
        sessionType: 'Generic',
        sessionDate: new Date(),
        status: 'Active',
        participantCount: 1,
      };

      const mockScoreboard = {
        participants: [],
        scores: [],
        rounds: [],
        categories: [],
      };

      // Set up initial state
      const { result } = renderHook(() => useSessionStore());
      act(() => {
        useSessionStore.setState({
          activeSession: mockSession,
          scoreboard: mockScoreboard,
        });
      });

      // Mock successful API response
      const actualScore: ScoreEntry = {
        id: 'score-real-123',
        participantId: 'p1',
        roundNumber: 1,
        category: 'Test',
        scoreValue: 15,
        timestamp: new Date(),
        createdBy: 'user-1',
      };

      (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
        ok: true,
        json: async () => actualScore,
      });

      // Submit score
      await act(async () => {
        await result.current.updateScore({
          participantId: 'p1',
          scoreValue: 15,
          roundNumber: 1,
          category: 'Test',
        });
      });

      // Verify optimistic score was replaced with actual score
      const finalScoreboard = result.current.scoreboard;
      expect(finalScoreboard?.scores).toHaveLength(1);
      expect(finalScoreboard?.scores[0].id).toBe('score-real-123');
    });

    it('should revert optimistic update on error', async () => {
      const mockSession: Session = {
        id: 'session-4',
        sessionCode: 'TEST02',
        sessionType: 'Generic',
        sessionDate: new Date(),
        status: 'Active',
        participantCount: 1,
      };

      const mockScoreboard = {
        participants: [],
        scores: [],
        rounds: [],
        categories: [],
      };

      const { result } = renderHook(() => useSessionStore());
      act(() => {
        useSessionStore.setState({
          activeSession: mockSession,
          scoreboard: mockScoreboard,
        });
      });

      // Mock API failure
      (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
      });

      // Submit score (should fail)
      await expect(async () => {
        await act(async () => {
          await result.current.updateScore({
            participantId: 'p1',
            scoreValue: 10,
          });
        });
      }).rejects.toThrow();

      // Verify optimistic score was reverted
      const finalScoreboard = result.current.scoreboard;
      expect(finalScoreboard?.scores).toHaveLength(0);
      expect(result.current.error).toBeTruthy();
    });
  });

  describe('session lifecycle', () => {
    it('should pause a session', async () => {
      const mockSession: Session = {
        id: 'session-5',
        sessionCode: 'TEST03',
        sessionType: 'Generic',
        sessionDate: new Date(),
        status: 'Active',
        participantCount: 1,
      };

      const pausedSession: Session = { ...mockSession, status: 'Paused' };

      const { result } = renderHook(() => useSessionStore());
      act(() => {
        useSessionStore.setState({ activeSession: mockSession });
      });

      (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
        ok: true,
        json: async () => pausedSession,
      });

      await act(async () => {
        await result.current.pauseSession();
      });

      expect(result.current.activeSession?.status).toBe('Paused');
    });

    it('should resume a session', async () => {
      const mockSession: Session = {
        id: 'session-6',
        sessionCode: 'TEST04',
        sessionType: 'Generic',
        sessionDate: new Date(),
        status: 'Paused',
        participantCount: 1,
      };

      const resumedSession: Session = { ...mockSession, status: 'Active' };

      const { result } = renderHook(() => useSessionStore());
      act(() => {
        useSessionStore.setState({ activeSession: mockSession });
      });

      (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
        ok: true,
        json: async () => resumedSession,
      });

      await act(async () => {
        await result.current.resumeSession();
      });

      expect(result.current.activeSession?.status).toBe('Active');
    });

    it('should finalize a session with ranks', async () => {
      const mockSession: Session = {
        id: 'session-7',
        sessionCode: 'TEST05',
        sessionType: 'Generic',
        sessionDate: new Date(),
        status: 'Active',
        participantCount: 2,
      };

      const finalizedSession: Session = { ...mockSession, status: 'Finalized' };

      const { result } = renderHook(() => useSessionStore());
      act(() => {
        useSessionStore.setState({ activeSession: mockSession });
      });

      (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
        ok: true,
        json: async () => finalizedSession,
      });

      await act(async () => {
        await result.current.finalizeSession({
          ranks: { p1: 1, p2: 2 },
        });
      });

      expect(result.current.activeSession?.status).toBe('Finalized');
    });
  });

  describe('SSE integration', () => {
    it('should add score from SSE event', () => {
      const mockScore: ScoreEntry = {
        id: 'sse-score-1',
        participantId: 'p1',
        roundNumber: 2,
        category: 'SSE',
        scoreValue: 20,
        timestamp: new Date(),
        createdBy: 'user-1',
      };

      const mockScoreboard = {
        participants: [],
        scores: [],
        rounds: [],
        categories: [],
      };

      const { result } = renderHook(() => useSessionStore());
      act(() => {
        useSessionStore.setState({ scoreboard: mockScoreboard });
      });

      act(() => {
        result.current.addScoreFromSSE(mockScore);
      });

      expect(result.current.scoreboard?.scores).toHaveLength(1);
      expect(result.current.scoreboard?.scores[0]).toEqual(mockScore);
    });
  });
});
