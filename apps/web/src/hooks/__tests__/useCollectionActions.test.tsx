/**
 * useCollectionActions Hook Tests
 * Issue #4259 - Collection Quick Actions for MeepleCard
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { useCollectionActions } from '../use-collection-actions';

// Mock fetch
global.fetch = vi.fn();

// Create query client for tests
const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
      mutations: {
        retry: false,
      },
    },
  });

// Wrapper with QueryClientProvider
function Wrapper({ children }: { children: ReactNode }) {
  const queryClient = createTestQueryClient();
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

describe('useCollectionActions', () => {
  const gameId = 'test-game-id';

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should fetch library status on mount', async () => {
    // Arrange
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ inLibrary: false, isFavorite: false, associatedData: null }),
    } as Response);

    // Act
    const { result } = renderHook(() => useCollectionActions(gameId), { wrapper: Wrapper });

    // Assert
    await waitFor(() => {
      expect(result.current.isInCollection).toBe(false);
    });
    expect(global.fetch).toHaveBeenCalledWith(
      `/api/v1/library/games/${gameId}/status`,
      expect.objectContaining({ credentials: 'include' })
    );
  });

  it('should return true when game is in collection', async () => {
    // Arrange
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        inLibrary: true,
        isFavorite: true,
        associatedData: {
          hasCustomAgent: true,
          hasPrivatePdf: false,
          chatSessionsCount: 2,
          gameSessionsCount: 5,
          checklistItemsCount: 3,
          labelsCount: 1,
        },
      }),
    } as Response);

    // Act
    const { result } = renderHook(() => useCollectionActions(gameId), { wrapper: Wrapper });

    // Assert
    await waitFor(() => {
      expect(result.current.isInCollection).toBe(true);
      expect(result.current.isFavorite).toBe(true);
      expect(result.current.hasAssociatedData).toBe(true);
    });
  });

  it('should detect associated data correctly', async () => {
    // Arrange
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        inLibrary: true,
        isFavorite: false,
        associatedData: {
          hasCustomAgent: false,
          hasPrivatePdf: true, // Has PDF
          chatSessionsCount: 0,
          gameSessionsCount: 0,
          checklistItemsCount: 0,
          labelsCount: 0,
        },
      }),
    } as Response);

    // Act
    const { result } = renderHook(() => useCollectionActions(gameId), { wrapper: Wrapper });

    // Assert
    await waitFor(() => {
      expect(result.current.hasAssociatedData).toBe(true);
    });
  });

  it('should add game to collection with optimistic update', async () => {
    // Arrange
    (global.fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ inLibrary: false, isFavorite: false, associatedData: null }),
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ id: 'new-entry-id' }),
      } as Response);

    // Act
    const { result } = renderHook(() => useCollectionActions(gameId), { wrapper: Wrapper });

    await waitFor(() => {
      expect(result.current.isInCollection).toBe(false);
    });

    // Add to collection
    result.current.add();

    // Assert - Should immediately update optimistically
    await waitFor(() => {
      expect(result.current.isInCollection).toBe(true);
    });
  });

  it('should remove game from collection with warning callback', async () => {
    // Arrange
    const mockWarningCallback = vi.fn();
    (global.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        inLibrary: true,
        isFavorite: false,
        associatedData: {
          hasCustomAgent: true,
          hasPrivatePdf: false,
          chatSessionsCount: 2,
          gameSessionsCount: 0,
          checklistItemsCount: 0,
          labelsCount: 0,
        },
      }),
    } as Response);

    // Act
    const { result } = renderHook(
      () => useCollectionActions(gameId, mockWarningCallback),
      { wrapper: Wrapper }
    );

    await waitFor(() => {
      expect(result.current.isInCollection).toBe(true);
    });

    // Remove with associated data
    result.current.remove();

    // Assert - Should show warning
    await waitFor(() => {
      expect(mockWarningCallback).toHaveBeenCalledWith(
        expect.objectContaining({ hasCustomAgent: true, chatSessionsCount: 2 }),
        expect.any(Function)
      );
    });
  });

  it('should remove directly when no associated data', async () => {
    // Arrange
    (global.fetch as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          inLibrary: true,
          isFavorite: false,
          associatedData: {
            hasCustomAgent: false,
            hasPrivatePdf: false,
            chatSessionsCount: 0,
            gameSessionsCount: 0,
            checklistItemsCount: 0,
            labelsCount: 0,
          },
        }),
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({}),
      } as Response);

    // Act
    const { result } = renderHook(() => useCollectionActions(gameId), { wrapper: Wrapper });

    await waitFor(() => {
      expect(result.current.isInCollection).toBe(true);
    });

    // Remove without warning (no associated data)
    result.current.remove();

    // Assert - Should call API directly
    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        `/api/v1/library/games/${gameId}`,
        expect.objectContaining({ method: 'DELETE' })
      );
    });
  });
});
