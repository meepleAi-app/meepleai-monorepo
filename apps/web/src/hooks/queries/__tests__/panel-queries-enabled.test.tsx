/**
 * #4187 — `useGames` e `useRecentChatSessions` si possono tenere ferme.
 *
 * Il pannello chat (ChatSlideOverPanel) è montato su ogni pagina autenticata e chiamava entrambe
 * prima di sapere se era aperto: due richieste a ogni pagina, a pannello chiuso.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

const api = vi.hoisted(() => ({
  getAll: vi.fn(),
  getRecent: vi.fn(),
}));

vi.mock('@/lib/api', () => ({
  api: {
    games: { getAll: (...a: unknown[]) => api.getAll(...a) },
    chatSessions: { getRecent: (...a: unknown[]) => api.getRecent(...a) },
  },
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({ user: { id: 'user-1' } }),
}));

import { useRecentChatSessions } from '../useChatSessions';
import { useGames } from '../useGames';

function wrapper({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={createTestQueryClient()}>{children}</QueryClientProvider>;
}

beforeEach(() => {
  vi.clearAllMocks();
  api.getAll.mockResolvedValue({ games: [], total: 0, page: 1, pageSize: 50, totalPages: 0 });
  api.getRecent.mockResolvedValue({ sessions: [], totalCount: 0 });
});

describe('useGames (#4187)', () => {
  it('does not fetch when disabled', async () => {
    const { result } = renderHook(() => useGames(undefined, 1, 50, false), { wrapper });
    await new Promise(r => setTimeout(r, 20));
    expect(result.current.fetchStatus).toBe('idle');
    expect(api.getAll).not.toHaveBeenCalled();
  });

  it('fetches by default', async () => {
    renderHook(() => useGames(undefined, 1, 50), { wrapper });
    await waitFor(() => expect(api.getAll).toHaveBeenCalledWith(undefined, 1, 50));
  });
});

describe('useRecentChatSessions (#4187)', () => {
  it('does not fetch when disabled, even with a user', async () => {
    const { result } = renderHook(() => useRecentChatSessions(50, false), { wrapper });
    await new Promise(r => setTimeout(r, 20));
    expect(result.current.fetchStatus).toBe('idle');
    expect(api.getRecent).not.toHaveBeenCalled();
  });

  it('fetches by default when there is a user', async () => {
    renderHook(() => useRecentChatSessions(50), { wrapper });
    await waitFor(() => expect(api.getRecent).toHaveBeenCalledWith('user-1', { limit: 50 }));
  });
});
