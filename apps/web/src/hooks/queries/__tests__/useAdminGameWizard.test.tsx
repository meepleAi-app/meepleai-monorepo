/**
 * useAdminGameWizard Hook Tests
 * Issue #4673: Admin Game Wizard mutation hooks coverage.
 *
 * Tests:
 * - useCreateGameFromWizard: success, 409 conflict, generic error, query invalidation
 * - useLaunchAdminPdfProcessing: success, 404 error, generic error
 */

import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';
import type { ReactNode } from 'react';

import {
  useCreateGameFromWizard,
  useLaunchAdminPdfProcessing,
} from '../useAdminGameWizard';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('@/lib/api', () => ({
  getApiBase: vi.fn(() => ''),
}));

const { mockToastSuccess, mockToastError } = vi.hoisted(() => ({
  mockToastSuccess: vi.fn(),
  mockToastError: vi.fn(),
}));

vi.mock('sonner', () => ({
  toast: {
    success: mockToastSuccess,
    error: mockToastError,
  },
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      mutations: { retry: false },
      queries: { retry: false, gcTime: 0 },
    },
  });

  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
};

const createMockFetch = (status: number, body: unknown) =>
  vi.fn().mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  });

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('useCreateGameFromWizard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('should call the correct endpoint with bggId in body', async () => {
    const mockFetch = createMockFetch(200, {
      sharedGameId: 'game-uuid-1',
      title: 'Gloomhaven',
      bggId: 174430,
      status: 'created',
    });
    vi.stubGlobal('fetch', mockFetch);

    const { result } = renderHook(() => useCreateGameFromWizard(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(174430);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/admin/games/wizard/create'),
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ bggId: 174430 }),
      })
    );
  });

  it('should return the created game data on success', async () => {
    const responseData = {
      sharedGameId: 'game-uuid-1',
      title: 'Gloomhaven',
      bggId: 174430,
      status: 'created',
    };
    vi.stubGlobal('fetch', createMockFetch(200, responseData));

    const { result } = renderHook(() => useCreateGameFromWizard(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(174430);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(responseData);
  });

  it('should show success toast on success', async () => {
    vi.stubGlobal('fetch', createMockFetch(200, {
      sharedGameId: 'id',
      title: 'Catan',
      bggId: 13,
      status: 'created',
    }));

    const { result } = renderHook(() => useCreateGameFromWizard(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(13);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockToastSuccess).toHaveBeenCalledWith(expect.stringContaining('Catan'));
  });

  it('should throw specific message for 409 conflict', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 409,
      json: () => Promise.resolve({ detail: 'A game with this BGG ID already exists' }),
    }));

    const { result } = renderHook(() => useCreateGameFromWizard(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(174430);

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toContain('A game with this BGG ID already exists');
  });

  it('should show error toast on failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      json: () => Promise.resolve({ detail: 'Internal server error' }),
    }));

    const { result } = renderHook(() => useCreateGameFromWizard(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(1);

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(mockToastError).toHaveBeenCalled();
  });

  it('should invalidate sharedGames query on success', async () => {
    const queryClient = new QueryClient({
      defaultOptions: { mutations: { retry: false }, queries: { retry: false, gcTime: 0 } },
    });
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    vi.stubGlobal('fetch', createMockFetch(200, {
      sharedGameId: 'id',
      title: 'Game',
      bggId: 1,
      status: 'created',
    }));

    const { result } = renderHook(() => useCreateGameFromWizard(), {
      wrapper: ({ children }: { children: ReactNode }) => (
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      ),
    });

    result.current.mutate(1);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: ['sharedGames'] })
    );
  });

  it('should use credentials: include in fetch options', async () => {
    const mockFetch = createMockFetch(200, {
      sharedGameId: 'id',
      title: 'Game',
      bggId: 1,
      status: 'created',
    });
    vi.stubGlobal('fetch', mockFetch);

    const { result } = renderHook(() => useCreateGameFromWizard(), {
      wrapper: createWrapper(),
    });

    result.current.mutate(1);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockFetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({ credentials: 'include' })
    );
  });
});

describe('useLaunchAdminPdfProcessing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('should call the correct endpoint with gameId in URL', async () => {
    const mockFetch = createMockFetch(200, {
      pdfDocumentId: 'pdf-uuid',
      gameId: 'game-uuid',
      status: 'processing',
      priority: 'Admin',
    });
    vi.stubGlobal('fetch', mockFetch);

    const { result } = renderHook(() => useLaunchAdminPdfProcessing(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ gameId: 'game-uuid', pdfDocumentId: 'pdf-uuid' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('/game-uuid/launch-processing'),
      expect.any(Object)
    );
  });

  it('should send pdfDocumentId in request body', async () => {
    const mockFetch = createMockFetch(200, {
      pdfDocumentId: 'pdf-uuid',
      gameId: 'game-uuid',
      status: 'processing',
      priority: 'Admin',
    });
    vi.stubGlobal('fetch', mockFetch);

    const { result } = renderHook(() => useLaunchAdminPdfProcessing(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ gameId: 'game-uuid', pdfDocumentId: 'pdf-uuid' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockFetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        body: JSON.stringify({ pdfDocumentId: 'pdf-uuid' }),
      })
    );
  });

  it('should show success toast on success', async () => {
    vi.stubGlobal('fetch', createMockFetch(200, {
      pdfDocumentId: 'pdf-uuid',
      gameId: 'game-uuid',
      status: 'processing',
      priority: 'Admin',
    }));

    const { result } = renderHook(() => useLaunchAdminPdfProcessing(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ gameId: 'game-uuid', pdfDocumentId: 'pdf-uuid' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockToastSuccess).toHaveBeenCalledWith(expect.stringContaining('admin priority'));
  });

  it('should throw specific message for 404 not found', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 404,
      json: () => Promise.resolve({ detail: 'PDF document not found' }),
    }));

    const { result } = renderHook(() => useLaunchAdminPdfProcessing(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ gameId: 'game-uuid', pdfDocumentId: 'missing' });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toContain('PDF document not found');
  });

  it('should show error toast on failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      json: () => Promise.resolve({ detail: 'Server error' }),
    }));

    const { result } = renderHook(() => useLaunchAdminPdfProcessing(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ gameId: 'g', pdfDocumentId: 'p' });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(mockToastError).toHaveBeenCalled();
  });

  it('should return result data on success', async () => {
    const responseData = {
      pdfDocumentId: 'pdf-uuid',
      gameId: 'game-uuid',
      status: 'processing',
      priority: 'Admin',
    };
    vi.stubGlobal('fetch', createMockFetch(200, responseData));

    const { result } = renderHook(() => useLaunchAdminPdfProcessing(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ gameId: 'game-uuid', pdfDocumentId: 'pdf-uuid' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(responseData);
  });
});
