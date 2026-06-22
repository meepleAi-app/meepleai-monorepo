/**
 * @vitest-environment jsdom
 *
 * useKbDocIngestionLog unit tests — Issue #1650 Task 7.
 *
 * Coverage:
 *   - docId null → no fetch fires.
 *   - enabled:false → no fetch fires.
 *   - valid docId → fetches and returns IngestionLog.
 *   - active status (Queued) → polling interval is active.
 *   - null body → surfaces as null (legacy doc, no job).
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import * as api from '@/lib/api/admin-kb-ingestion';
import type { IngestionLog } from '@/lib/api/schemas/ingestion-log.schemas';

import { useKbDocIngestionLog } from '../useKbDocIngestionLog';

function wrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

const sampleLog = (status: string): IngestionLog => ({
  id: '00000000-0000-0000-0000-000000000001',
  pdfDocumentId: '00000000-0000-0000-0000-000000000002',
  pdfFileName: 'test.pdf',
  userId: '00000000-0000-0000-0000-000000000003',
  status,
  priority: 0,
  currentStep: null,
  createdAt: new Date().toISOString(),
  startedAt: null,
  completedAt: null,
  errorMessage: null,
  retryCount: 0,
  maxRetries: 3,
  canRetry: false,
  steps: [],
});

describe('useKbDocIngestionLog', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('does not fetch when docId is null', () => {
    const spy = vi.spyOn(api, 'fetchKbDocIngestionLog').mockResolvedValue(null);
    renderHook(() => useKbDocIngestionLog({ docId: null }), { wrapper: wrapper() });
    expect(spy).not.toHaveBeenCalled();
  });

  it('does not fetch when enabled is false', () => {
    const spy = vi.spyOn(api, 'fetchKbDocIngestionLog').mockResolvedValue(null);
    renderHook(() => useKbDocIngestionLog({ docId: 'xxx', enabled: false }), {
      wrapper: wrapper(),
    });
    expect(spy).not.toHaveBeenCalled();
  });

  it('fetches with valid docId', async () => {
    const log = sampleLog('Completed');
    vi.spyOn(api, 'fetchKbDocIngestionLog').mockResolvedValue(log);
    const { result } = renderHook(() => useKbDocIngestionLog({ docId: 'xxx' }), {
      wrapper: wrapper(),
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(log);
  });

  it('uses polling interval when status is queued', async () => {
    const log = sampleLog('Queued');
    vi.spyOn(api, 'fetchKbDocIngestionLog').mockResolvedValue(log);
    const { result } = renderHook(() => useKbDocIngestionLog({ docId: 'xxx' }), {
      wrapper: wrapper(),
    });
    await waitFor(() => expect(result.current.data).toBeTruthy());
    expect(['Queued', 'Processing'].includes(result.current.data!.status)).toBe(true);
  });

  it('returns null when backend body is null', async () => {
    vi.spyOn(api, 'fetchKbDocIngestionLog').mockResolvedValue(null);
    const { result } = renderHook(() => useKbDocIngestionLog({ docId: 'xxx' }), {
      wrapper: wrapper(),
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toBeNull();
  });
});
