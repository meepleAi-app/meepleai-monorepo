/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { BggTagInput } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { useImportBggTags } from '../useImportBggTags';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      importBggTags: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

import { api } from '@/lib/api';
import { toast } from 'sonner';

const mockImportBggTags = api.admin.importBggTags as ReturnType<typeof vi.fn>;

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const spy = vi.spyOn(queryClient, 'invalidateQueries');
  return {
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    ),
    invalidateSpy: spy,
  };
}

const SHARED_GAME_ID = '11111111-1111-1111-1111-111111111111';

const tags: BggTagInput[] = [
  { category: 'Mechanism', name: 'Role Selection' },
  { category: 'Mechanism', name: 'Variable Phase Order' },
  { category: 'Theme', name: 'Economic' },
];

describe('useImportBggTags', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls api.admin.importBggTags with sharedGameId + tags array', async () => {
    mockImportBggTags.mockResolvedValue({ inserted: 3, skipped: 0 });

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useImportBggTags(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, tags });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockImportBggTags).toHaveBeenCalledWith(SHARED_GAME_ID, tags);
  });

  it('invalidates golden.byGame on success', async () => {
    mockImportBggTags.mockResolvedValue({ inserted: 3, skipped: 0 });

    const { wrapper, invalidateSpy } = createWrapper();
    const { result } = renderHook(() => useImportBggTags(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, tags });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['golden', SHARED_GAME_ID] });
  });

  it('fires toast.success including inserted/skipped counts when both > 0', async () => {
    // Sprint 2 Task 17 contract: importer UI surfaces both numbers so
    // operators see exactly which rows were duplicates.
    mockImportBggTags.mockResolvedValue({ inserted: 2, skipped: 1 });

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useImportBggTags(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, tags });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(toast.success).toHaveBeenCalledWith('Imported 2 BGG tag(s) (1 skipped as duplicate)');
  });

  it('fires toast.success with no skipped suffix when skipped === 0', async () => {
    mockImportBggTags.mockResolvedValue({ inserted: 3, skipped: 0 });

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useImportBggTags(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, tags });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(toast.success).toHaveBeenCalledWith('Imported 3 BGG tag(s)');
  });

  it('pluralises the skipped suffix correctly when skipped > 1', async () => {
    mockImportBggTags.mockResolvedValue({ inserted: 1, skipped: 2 });

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useImportBggTags(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, tags });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(toast.success).toHaveBeenCalledWith('Imported 1 BGG tag(s) (2 skipped as duplicates)');
  });

  it('fires toast.error with the message on failure', async () => {
    mockImportBggTags.mockRejectedValue(new Error('boom'));

    const { wrapper } = createWrapper();
    const { result } = renderHook(() => useImportBggTags(), { wrapper });

    result.current.mutate({ sharedGameId: SHARED_GAME_ID, tags });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(toast.error).toHaveBeenCalledWith('Failed to import BGG tags: boom');
  });
});
