/**
 * useSessionAgentLaunch — unit tests
 * Issue #2500 C1 fix: hook must send initialGameStateJson: '' (not '{}') so the
 * BE can default to GameState.Initial(UserId) instead of failing with 422.
 */
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getAgents: vi.fn(),
    },
    agentSessions: {
      launch: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';
import { useSessionAgentLaunch } from '../useSessionAgentLaunch';
import type { AgentDto } from '@/lib/api/schemas';

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

const SESSION_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const GAME_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const AGENT_ID = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
const AGENT_SESSION_ID = 'dddddddd-dddd-dddd-dddd-dddddddddddd';

const activeAgent: AgentDto = {
  id: AGENT_ID,
  name: 'Test RAG Agent',
  type: 'RagAgent',
  strategyName: 'HybridRag',
  strategyParameters: {},
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  lastInvokedAt: null,
  invocationCount: 0,
  isRecentlyUsed: false,
  isIdle: true,
  gameId: GAME_ID,
};

describe('useSessionAgentLaunch', () => {
  beforeEach(() => {
    vi.mocked(api.games.getAgents).mockReset();
    vi.mocked(api.agentSessions.launch).mockReset();
  });

  it('returns idle when sessionId is null', () => {
    const { result } = renderHook(() => useSessionAgentLaunch(null, GAME_ID), { wrapper });
    expect(result.current.status).toBe('idle');
    expect(result.current.agentSessionId).toBe('');
  });

  it('returns idle when gameId is null', () => {
    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, null), { wrapper });
    expect(result.current.status).toBe('idle');
  });

  it('returns idle when enabled=false', () => {
    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, GAME_ID, false), {
      wrapper,
    });
    expect(result.current.status).toBe('idle');
    expect(api.games.getAgents).not.toHaveBeenCalled();
  });

  /**
   * C1 fix: the hook must send initialGameStateJson: '' (empty string), NOT '{}'.
   * The BE validator now accepts empty as "use default" and the handler calls
   * GameState.Initial(UserId) instead of GameState.FromJson('{}') which throws.
   */
  it('C1 fix: sends initialGameStateJson as empty string (not {}) to the launch API', async () => {
    vi.mocked(api.games.getAgents).mockResolvedValueOnce([activeAgent]);
    vi.mocked(api.agentSessions.launch).mockResolvedValueOnce({ agentSessionId: AGENT_SESSION_ID });

    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, GAME_ID), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('ready'));

    expect(api.agentSessions.launch).toHaveBeenCalledWith(
      SESSION_ID,
      expect.objectContaining({
        initialGameStateJson: '',
      })
    );
  });

  it('returns ready with agentSessionId when launch succeeds', async () => {
    vi.mocked(api.games.getAgents).mockResolvedValueOnce([activeAgent]);
    vi.mocked(api.agentSessions.launch).mockResolvedValueOnce({ agentSessionId: AGENT_SESSION_ID });

    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, GAME_ID), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(result.current.agentSessionId).toBe(AGENT_SESSION_ID);
  });

  it('returns no-agent when getAgents returns empty list', async () => {
    vi.mocked(api.games.getAgents).mockResolvedValueOnce([]);

    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, GAME_ID), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('no-agent'));
    expect(api.agentSessions.launch).not.toHaveBeenCalled();
  });

  it('returns error when getAgents fails', async () => {
    vi.mocked(api.games.getAgents).mockRejectedValueOnce(new Error('network'));

    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, GAME_ID), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('error'));
  });

  it('returns error when launch fails', async () => {
    vi.mocked(api.games.getAgents).mockResolvedValueOnce([activeAgent]);
    vi.mocked(api.agentSessions.launch).mockRejectedValueOnce(new Error('422 Unprocessable'));

    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, GAME_ID), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('error'));
    expect(result.current.agentSessionId).toBe('');
  });
});
