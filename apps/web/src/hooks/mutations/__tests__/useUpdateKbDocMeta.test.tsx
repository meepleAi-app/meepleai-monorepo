import { describe, expect, it, vi } from 'vitest';
import type { JSX, ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useUpdateKbDocMeta } from '../useUpdateKbDocMeta';
import { api } from '@/lib/api';

function makeWrapper(qc: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }): JSX.Element {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

const DTO_FIXTURE = {
  id: '00000000-0000-0000-0000-000000000001',
  gameId: null,
  gameName: null,
  fileName: 'azul.pdf',
  processingState: 'Ready' as const,
  pageCount: 24,
  processedAt: '2026-05-31T00:00:00Z',
  uploadedAt: '2026-05-31T00:00:00Z',
  updatedAt: '2026-05-31T00:00:00Z',
  title: 'New Title',
  tags: ['strategy'],
  updatedBy: '00000000-0000-0000-0000-000000000003',
};

describe('useUpdateKbDocMeta (Phase 3 #1737)', () => {
  it('calls api.kbDocs.patchKbDocMetadata on mutate', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockResolvedValue(DTO_FIXTURE);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useUpdateKbDocMeta(), { wrapper: makeWrapper(qc) });
    await act(async () => {
      await result.current.mutateAsync({
        id: '00000000-0000-0000-0000-000000000001',
        body: { title: 'New Title' },
      });
    });
    expect(spy).toHaveBeenCalledWith('00000000-0000-0000-0000-000000000001', {
      title: 'New Title',
    });
    spy.mockRestore();
  });

  it('invalidates ["kb-docs","user"] queries on success', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockResolvedValue(DTO_FIXTURE);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
    const { result } = renderHook(() => useUpdateKbDocMeta(), { wrapper: makeWrapper(qc) });
    await act(async () => {
      await result.current.mutateAsync({
        id: '00000000-0000-0000-0000-000000000001',
        body: { title: 'New Title' },
      });
    });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['kb-docs', 'user'] });
    spy.mockRestore();
  });

  it('invalidates ["kb-globale","search"] queries on success (DEC-2 aggressive)', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockResolvedValue(DTO_FIXTURE);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
    const { result } = renderHook(() => useUpdateKbDocMeta(), { wrapper: makeWrapper(qc) });
    await act(async () => {
      await result.current.mutateAsync({
        id: '00000000-0000-0000-0000-000000000001',
        body: { title: 'New Title' },
      });
    });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['kb-globale', 'search'] });
    spy.mockRestore();
  });

  it('exposes error on 404 (S5 anti-info-leak)', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockRejectedValue(new Error('HTTP 404'));
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useUpdateKbDocMeta(), { wrapper: makeWrapper(qc) });
    await act(async () => {
      try {
        await result.current.mutateAsync({
          id: '00000000-0000-0000-0000-000000000001',
          body: { title: 'X' },
        });
      } catch {
        /* expected */
      }
    });
    await waitFor(() => expect(result.current.error).toBeInstanceOf(Error));
    expect(result.current.error?.message).toContain('404');
    spy.mockRestore();
  });

  it('does NOT invalidate on error (cache stays consistent)', async () => {
    const spy = vi.spyOn(api.kbDocs, 'patchKbDocMetadata').mockRejectedValue(new Error('HTTP 422'));
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');
    const { result } = renderHook(() => useUpdateKbDocMeta(), { wrapper: makeWrapper(qc) });
    await act(async () => {
      try {
        await result.current.mutateAsync({
          id: '00000000-0000-0000-0000-000000000001',
          body: { title: 'X' },
        });
      } catch {
        /* expected */
      }
    });
    expect(invalidateSpy).not.toHaveBeenCalled();
    spy.mockRestore();
  });
});
