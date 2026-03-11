/**
 * Session Store Tests (Issue #3163, rewritten for Issue #5041)
 *
 * Tests for session state management using liveSessionsClient.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useSessionStore } from '../sessionStore';
import type { ScoreEntry, ScoreboardData } from '@/components/session/types';
import type {
  LiveSessionDto,
  LiveSessionRoundScoreDto,
} from '@/lib/api/schemas/live-sessions.schemas';

// Mock the api module
const mockCreateSession = vi.fn();
const mockGetSession = vi.fn();
const mockGetByCode = vi.fn();
const mockPauseSession = vi.fn();
const mockResumeSession = vi.fn();
const mockCompleteSession = vi.fn();
const mockUpdateScore = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      createSession: (...args: unknown[]) => mockCreateSession(...args),
      getSession: (...args: unknown[]) => mockGetSession(...args),
      getByCode: (...args: unknown[]) => mockGetByCode(...args),
      pauseSession: (...args: unknown[]) => mockPauseSession(...args),
      resumeSession: (...args: unknown[]) => mockResumeSession(...args),
      completeSession: (...args: unknown[]) => mockCompleteSession(...args),
      getActive: vi.fn().mockResolvedValue([]),
      addPlayer: vi.fn(),
      removePlayer: vi.fn(),
      recordScore: vi.fn(),
      getScores: vi.fn().mockResolvedValue([]),
      startSession: vi.fn(),
    },
    sessionTracking: {
      updateScore: (...args: unknown[]) => mockUpdateScore(...args),
    },
  },
}));

const makeMockSession = (overrides: Partial<LiveSessionDto> = {}): LiveSessionDto => ({
  id: 'session-1',
  code: 'ABC123',
  gameId: null,
  gameName: null,
  gameImageUrl: null,
  hostUserId: 'user-1',
  hostDisplayName: 'Host',
  status: 'InProgress',
  maxPlayers: 10,
  isPublic: false,
  location: null,
  notes: null,
  players: [],
  roundScores: [],
  createdAt: new Date().toISOString(),
  startedAt: null,
  completedAt: null,
  agentMode: 'None',
  ...overrides,
} as LiveSessionDto);

describe('sessionStore', () => {
  beforeEach(() => {
    // Reset store before each test
    useSessionStore.getState().reset();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('createSession', () => {
    it('should create a session successfully', async () => {
      const mockSession = makeMockSession({ id: 'session-1' });

      mockCreateSession.mockResolvedValueOnce('session-1');
      mockGetSession.mockResolvedValueOnce(mockSession);

      const { result } = renderHook(() => useSessionStore());

      await act(async () => {
        await result.current.createSession({
          maxPlayers: 4,
        });
      });

      expect(result.current.activeSession).toEqual(mockSession);
      expect(result.current.isLoading).toBe(false);
      expect(result.current.error).toBeNull();
    });

    it('should handle creation errors', async () => {
      mockCreateSession.mockRejectedValueOnce(new Error('Failed to create'));

      const { result } = renderHook(() => useSessionStore());

      await expect(async () => {
        await act(async () => {
          await result.current.createSession({
            maxPlayers: 2,
          });
        });
      }).rejects.toThrow();

      expect(result.current.error).toBeTruthy();
      expect(result.current.isLoading).toBe(false);
    });
  });

  describe('joinSession', () => {
    it('should join a session by code', async () => {
      const mockSession = makeMockSession({ id: 'session-2', code: 'XYZ789' });

      mockGetByCode.mockResolvedValueOnce(mockSession);

      const { result } = renderHook(() => useSessionStore());

      await act(async () => {
        await result.current.joinSession('XYZ789');
      });

      expect(result.current.activeSession).toEqual(mockSession);
      expect(result.current.isLoading).toBe(false);
    });

    it('should handle invalid session codes (404)', async () => {
      mockGetByCode.mockRejectedValueOnce(new Error('Failed to join session'));

      const { result } = renderHook(() => useSessionStore());

      await expect(async () => {
        await act(async () => {
          await result.current.joinSession('INVALID');
        });
      }).rejects.toThrow();

      expect(result.current.error).toBeTruthy();
    });
  });

  describe('legacy updateScore (Optimistic UI)', () => {
    it('should call sessionTracking.updateScore', async () => {
      const mockSession = makeMockSession({ id: 'session-3' });

      mockUpdateScore.mockResolvedValueOnce(undefined);

      const { result } = renderHook(() => useSessionStore());
      act(() => {
        useSessionStore.setState({ activeSession: mockSession });
      });

      await act(async () => {
        await result.current.updateScore({
          participantId: 'p1',
          scoreValue: 15,
          roundNumber: 1,
          category: 'Test',
        });
      });

      expect(mockUpdateScore).toHaveBeenCalledWith('session-3', {
        participantId: 'p1',
        roundNumber: 1,
        category: 'Test',
        scoreValue: 15,
      });
    });

    it('should throw when updateScore called without active session', async () => {
      const { result } = renderHook(() => useSessionStore());

      await expect(async () => {
        await act(async () => {
          await result.current.updateScore({
            participantId: 'p1',
            scoreValue: 10,
          });
        });
      }).rejects.toThrow('No active session');
    });
  });

  describe('session lifecycle', () => {
    it('should pause a session', async () => {
      const mockSession = makeMockSession({
        id: 'session-5',
        status: 'InProgress',
      });

      mockPauseSession.mockResolvedValueOnce(undefined);

      const { result } = renderHook(() => useSessionStore());
      act(() => {
        useSessionStore.setState({ activeSession: mockSession });
      });

      await act(async () => {
        await result.current.pauseSession();
      });

      expect(result.current.activeSession?.status).toBe('Paused');
    });

    it('should resume a session', async () => {
      const mockSession = makeMockSession({
        id: 'session-6',
        status: 'Paused',
      });

      mockResumeSession.mockResolvedValueOnce(undefined);

      const { result } = renderHook(() => useSessionStore());
      act(() => {
        useSessionStore.setState({ activeSession: mockSession });
      });

      await act(async () => {
        await result.current.resumeSession();
      });

      expect(result.current.activeSession?.status).toBe('InProgress');
    });

    it('should finalize a session (legacy compat)', async () => {
      const mockSession = makeMockSession({
        id: 'session-7',
        status: 'InProgress',
      });

      mockCompleteSession.mockResolvedValueOnce(undefined);

      const { result } = renderHook(() => useSessionStore());
      act(() => {
        useSessionStore.setState({ activeSession: mockSession });
      });

      await act(async () => {
        await result.current.finalizeSession({
          ranks: { p1: 1, p2: 2 },
        });
      });

      expect(result.current.activeSession?.status).toBe('Completed');
    });
  });

  describe('SSE integration', () => {
    it('should add score from SSE event (legacy compat)', () => {
      const mockScore: ScoreEntry = {
        id: 'sse-score-1',
        participantId: 'p1',
        roundNumber: 2,
        category: 'SSE',
        scoreValue: 20,
        timestamp: new Date(),
        createdBy: 'user-1',
      };

      const mockScoreboard: ScoreboardData = {
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

  // ── activeTool (Issue #4974) ──────────────────────────────────────────────

  describe('activeTool', () => {
    it('defaults to scoreboard', () => {
      const { result } = renderHook(() => useSessionStore());
      expect(result.current.activeTool).toBe('scoreboard');
    });

    it('updates activeTool via setActiveTool', () => {
      const { result } = renderHook(() => useSessionStore());
      act(() => {
        result.current.setActiveTool('dice');
      });
      expect(result.current.activeTool).toBe('dice');
    });

    it('switches between all base tools', () => {
      const { result } = renderHook(() => useSessionStore());
      const tools = ['scoreboard', 'turn-order', 'dice', 'whiteboard'] as const;
      for (const tool of tools) {
        act(() => { result.current.setActiveTool(tool); });
        expect(result.current.activeTool).toBe(tool);
      }
    });

    it('accepts custom tool IDs (string)', () => {
      const { result } = renderHook(() => useSessionStore());
      act(() => {
        result.current.setActiveTool('custom-dice-tool');
      });
      expect(result.current.activeTool).toBe('custom-dice-tool');
    });

    it('resets activeTool to scoreboard on reset()', () => {
      const { result } = renderHook(() => useSessionStore());
      act(() => { result.current.setActiveTool('whiteboard'); });
      expect(result.current.activeTool).toBe('whiteboard');

      act(() => { result.current.reset(); });
      expect(result.current.activeTool).toBe('scoreboard');
    });
  });
});
