import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import * as glossaryApi from '@/lib/api/gamebook-glossary';

import {
  useGamebookGlossary,
  useBootstrapGlossary,
  gamebookGlossaryKeys,
} from '../useGamebookGlossary';

vi.mock('@/lib/api/gamebook-glossary');

const CAMPAIGN_ID = '11111111-1111-4111-a111-111111111111';
const ENTRY_ID = '44444444-4444-4444-a444-444444444444';

const fakeEntry: glossaryApi.GamebookGlossaryEntry = {
  id: ENTRY_ID,
  termEn: 'Dragon',
  termIt: 'Drago',
  source: 'AutoBootstrap',
  updatedAt: '2026-05-07T10:00:00Z',
};

function makeWrapper(qc: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

describe('useGamebookGlossary', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('fetches glossary list when campaignId is set', async () => {
    vi.mocked(glossaryApi.listGlossary).mockResolvedValueOnce([fakeEntry]);
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useGamebookGlossary(CAMPAIGN_ID), {
      wrapper: makeWrapper(qc),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(glossaryApi.listGlossary).toHaveBeenCalledWith(CAMPAIGN_ID);
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data![0].termEn).toBe('Dragon');
  });

  it('does not fetch when campaignId is empty string', () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useGamebookGlossary(''), { wrapper: makeWrapper(qc) });
    expect(glossaryApi.listGlossary).not.toHaveBeenCalled();
  });
});

describe('useBootstrapGlossary', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls bootstrapGlossary and invalidates list', async () => {
    vi.mocked(glossaryApi.bootstrapGlossary).mockResolvedValueOnce([fakeEntry]);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');

    const { result } = renderHook(() => useBootstrapGlossary(CAMPAIGN_ID), {
      wrapper: makeWrapper(qc),
    });

    result.current.mutate();

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(glossaryApi.bootstrapGlossary).toHaveBeenCalledWith(CAMPAIGN_ID);
    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: gamebookGlossaryKeys.list(CAMPAIGN_ID) })
    );
    expect(result.current.data).toHaveLength(1);
  });
});
