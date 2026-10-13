/**
 * useSessionAgentLaunch — unit tests
 * Issue #2500 C1 fix: hook must send initialGameStateJson: '' (not '{}') so the
 * BE can default to GameState.Initial(UserId) instead of failing with 422.
 * Issue #4154 (ADR-095 fetta 0b): il lancio non scarica piu` un elenco di agenti e non passa un id;
 * il backend usa l'agente di sistema. «Nessun assistente» arriva come codice d'errore dal backend.
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
import { createApiError } from '@/lib/api/core/errors';
import { LaunchSessionAgentRequestSchema } from '@/lib/api/clients/agentSessionsClient';
import { useSessionAgentLaunch } from '../useSessionAgentLaunch';

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

// UUID v4 validi: lo schema reale della richiesta (z.string().uuid()) rifiuta 'bbbbbbbb-bbbb-…'.
const SESSION_ID = '3f2b8c1e-6a4d-4e7f-9b21-5c8d0a7e4f13';
const GAME_ID = '7c9e6679-7425-40de-944b-e07fc1f90ae7';
const AGENT_SESSION_ID = 'a1d0c6e8-3f5b-4c2a-8e7d-9b4f1c2e6a05';

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
    expect(api.agentSessions.launch).not.toHaveBeenCalled();
  });

  it('#4154: non scarica l`elenco degli agenti e lancia senza id dell`agente', async () => {
    vi.mocked(api.agentSessions.launch).mockResolvedValueOnce({ agentSessionId: AGENT_SESSION_ID });

    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, GAME_ID), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(api.games.getAgents).not.toHaveBeenCalled();
    const [, request] = vi.mocked(api.agentSessions.launch).mock.calls[0];
    expect(request).toMatchObject({ gameId: GAME_ID });
    // Lo schema reale della richiesta deve accettarla senza id: se tornasse a esigerlo, il client
    // rifiuterebbe la richiesta prima ancora di mandarla.
    const parsed = LaunchSessionAgentRequestSchema.safeParse(request);
    expect(parsed.success).toBe(true);
    if (!parsed.success) throw new Error('richiesta di lancio rifiutata dallo schema');
    expect(parsed.data).not.toHaveProperty('agentDefinitionId');
  });

  /**
   * C1 fix: the hook must send initialGameStateJson: '' (empty string), NOT '{}'.
   * The BE validator now accepts empty as "use default" and the handler calls
   * GameState.Initial(UserId) instead of GameState.FromJson('{}') which throws.
   */
  it('C1 fix: sends initialGameStateJson as empty string (not {}) to the launch API', async () => {
    vi.mocked(api.agentSessions.launch).mockResolvedValueOnce({ agentSessionId: AGENT_SESSION_ID });

    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, GAME_ID), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(api.agentSessions.launch).toHaveBeenCalledWith(
      SESSION_ID,
      expect.objectContaining({ initialGameStateJson: '' })
    );
  });

  it('returns ready with agentSessionId when launch succeeds', async () => {
    vi.mocked(api.agentSessions.launch).mockResolvedValueOnce({ agentSessionId: AGENT_SESSION_ID });

    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, GAME_ID), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('ready'));
    expect(result.current.agentSessionId).toBe(AGENT_SESSION_ID);
  });

  it('#4154: returns no-agent when the backend reports system_agent_unavailable', async () => {
    // L'errore nasce come lo produce il client HTTP: da una risposta 409 con il corpo che il
    // middleware del backend scrive ({ error: ErrorCode, message }). Copre la catena intera.
    const error = await createApiError(
      '/api/v1/game-sessions/x/agent/launch',
      new Response(
        JSON.stringify({
          error: 'system_agent_unavailable',
          message: "L'assistente non è disponibile: l'agente di sistema non è attivo.",
        }),
        { status: 409, headers: { 'Content-Type': 'application/json' } }
      )
    );
    vi.mocked(api.agentSessions.launch).mockRejectedValueOnce(error);

    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, GAME_ID), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('no-agent'));
    expect(result.current.agentSessionId).toBe('');
  });

  it('returns error when launch fails for any other reason', async () => {
    vi.mocked(api.agentSessions.launch).mockRejectedValueOnce(new Error('422 Unprocessable'));

    const { result } = renderHook(() => useSessionAgentLaunch(SESSION_ID, GAME_ID), { wrapper });

    await waitFor(() => expect(result.current.status).toBe('error'));
    expect(result.current.agentSessionId).toBe('');
  });
});
