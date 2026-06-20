import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { playRecordsApi } from '@/lib/api/play-records.api';

import { usePlayRecordPhotoUpload } from '../usePlayRecordPhotoUpload';

function wrapper(client: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('usePlayRecordPhotoUpload', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('uploads and invalidates the record detail query', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    vi.spyOn(playRecordsApi, 'uploadPhoto').mockResolvedValue({
      photoId: 'p1',
      photoUrl: 'u',
      thumbnailUrl: null,
      ocrText: null,
      wasDeduplicated: false,
    });

    const { result } = renderHook(() => usePlayRecordPhotoUpload('rec-1'), {
      wrapper: wrapper(client),
    });

    result.current.mutate({ file: new Blob(['x'], { type: 'image/jpeg' }) });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(playRecordsApi.uploadPhoto).toHaveBeenCalledWith('rec-1', expect.any(Blob), {
      caption: undefined,
      extractScoreFromPhoto: undefined,
    });
    expect(invalidate).toHaveBeenCalled();
  });
});
