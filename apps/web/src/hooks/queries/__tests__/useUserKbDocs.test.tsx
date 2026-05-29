import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { toKbDoc, useUserKbDocs } from '../useUserKbDocs';
import type { UserKbDocDto } from '@/lib/api/schemas/kb-docs.schemas';

vi.mock('@/lib/api', () => ({
  api: {
    kbDocs: {
      listUserKbDocs: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';

const dtoBase: UserKbDocDto = {
  id: '11111111-1111-1111-1111-111111111111',
  gameId: '22222222-2222-2222-2222-222222222222',
  gameName: 'Catan',
  fileName: 'catan-rules.pdf',
  processingState: 'Ready',
  pageCount: 24,
  processedAt: '2026-05-28T11:00:00+00:00',
  uploadedAt: '2026-05-28T09:00:00+00:00',
  updatedAt: '2026-05-28T11:00:00+00:00',
};

describe('toKbDoc (adapter)', () => {
  it('passes through BE updatedAt when ProcessedAt is present (#1645 canonical BE-side)', () => {
    const result = toKbDoc(dtoBase);
    expect(result.updatedAt).toBe(dtoBase.updatedAt);
  });

  it('passes through BE updatedAt when ProcessedAt is null (BE returns uploadedAt as updatedAt) (#1645)', () => {
    const result = toKbDoc({
      ...dtoBase,
      processedAt: null,
      updatedAt: dtoBase.uploadedAt,
    });
    expect(result.updatedAt).toBe(dtoBase.uploadedAt);
  });

  it('preserves the other fields unchanged', () => {
    const result = toKbDoc(dtoBase);
    expect(result).toMatchObject({
      id: dtoBase.id,
      gameId: dtoBase.gameId,
      gameName: dtoBase.gameName,
      fileName: dtoBase.fileName,
      processingState: dtoBase.processingState,
      pageCount: dtoBase.pageCount,
      processedAt: dtoBase.processedAt,
    });
  });
});

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('useUserKbDocs', () => {
  beforeEach(() => {
    vi.mocked(api.kbDocs.listUserKbDocs).mockReset();
  });

  it('fetches with default params { page:1, pageSize:20, sortBy:recent, state:ready }', async () => {
    vi.mocked(api.kbDocs.listUserKbDocs).mockResolvedValue({
      items: [dtoBase],
      total: 1,
      page: 1,
      pageSize: 20,
    });

    const { result } = renderHook(() => useUserKbDocs(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.kbDocs.listUserKbDocs).toHaveBeenCalledWith({
      page: 1,
      pageSize: 20,
      sortBy: 'recent',
      state: 'ready',
    });
    expect(result.current.data?.items).toHaveLength(1);
    expect(result.current.data?.items[0].updatedAt).toBe('2026-05-28T11:00:00+00:00');
  });

  it('exposes total count from the envelope', async () => {
    vi.mocked(api.kbDocs.listUserKbDocs).mockResolvedValue({
      items: [],
      total: 42,
      page: 1,
      pageSize: 20,
    });

    const { result } = renderHook(() => useUserKbDocs(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.total).toBe(42);
  });
});
