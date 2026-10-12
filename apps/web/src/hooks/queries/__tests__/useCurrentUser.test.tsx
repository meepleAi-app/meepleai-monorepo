/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useCurrentUser, userKeys } from '../useCurrentUser';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/actions/auth', () => ({
  getCurrentUser: vi.fn(),
}));

import { getCurrentUser } from '@/actions/auth';

const mockGetCurrentUser = getCurrentUser as ReturnType<typeof vi.fn>;

// ── Helpers ───────────────────────────────────────────────────────────────────

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('userKeys', () => {
  it('returns stable query key for current user', () => {
    expect(userKeys.current()).toEqual(['user', 'current']);
  });
});

describe('useCurrentUser', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns user data on success', async () => {
    const mockUser = { id: 'u1', email: 'test@example.com', name: 'Test User' };
    mockGetCurrentUser.mockResolvedValue({ user: mockUser });

    const { result } = renderHook(() => useCurrentUser(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(mockUser);
  });

  it('returns null when user is not authenticated', async () => {
    mockGetCurrentUser.mockResolvedValue({ user: undefined });

    const { result } = renderHook(() => useCurrentUser(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toBeNull();
  });

  it('enters error state when getCurrentUser throws a 401', async () => {
    // The hook's retry function skips retries on 401 errors
    mockGetCurrentUser.mockRejectedValue(new Error('401 Unauthorized'));

    const { result } = renderHook(() => useCurrentUser(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error?.message).toBe('401 Unauthorized');
  });

  it("un errore che non è un 401 conserva l'utente già noto (#4190)", async () => {
    // getCurrentUser non lancia: cattura l'errore e lo restituisce in `error`. Se la query lo
    // trattasse come «anonimo», un refetch al ritorno sulla scheda con la rete giu' azzererebbe
    // l'utente per tutta l'app, ora che AuthProvider legge da qui.
    const mockUser = { id: 'u1', email: 'test@example.com', name: 'Test User' };
    mockGetCurrentUser.mockResolvedValueOnce({ success: true, user: mockUser });
    const { result } = renderHook(() => useCurrentUser(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.data).toEqual(mockUser));
    // TanStack ridisegna solo per le proprieta' lette: senza questa lettura il cambio di
    // `isError` non arriverebbe mai a result.current.
    expect(result.current.isError).toBe(false);

    // Ogni tentativo fallisce: il retry del hook (un secondo tentativo, dopo ~1 s) prevale su
    // quello del wrapper.
    mockGetCurrentUser.mockResolvedValue({
      success: false,
      error: { type: 'network', message: 'Impossibile caricare i dati utente.' },
    });
    await result.current.refetch();

    await waitFor(() => expect(result.current.isError).toBe(true), { timeout: 4000 });
    expect(result.current.data).toEqual(mockUser);
  });

  it('starts in loading state', () => {
    mockGetCurrentUser.mockReturnValue(new Promise(() => {})); // never resolves

    const { result } = renderHook(() => useCurrentUser(), { wrapper: createWrapper() });

    expect(result.current.isLoading).toBe(true);
  });
});
