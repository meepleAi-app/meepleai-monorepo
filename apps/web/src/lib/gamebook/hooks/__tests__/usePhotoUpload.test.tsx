import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import * as photosApi from '@/lib/api/gamebook-photos';

import { usePhotoUpload } from '../usePhotoUpload';

vi.mock('@/lib/api/gamebook-photos');

const CAMPAIGN_ID = '11111111-1111-4111-a111-111111111111';
const PHOTO_ID = '22222222-2222-4222-a222-222222222222';

const fakeArtifact: photosApi.GamebookPhotoArtifact = {
  id: PHOTO_ID,
  campaignId: CAMPAIGN_ID,
  pageType: 'Storybook',
  status: 'Uploaded',
  ocrFullText: null,
  segments: [],
  failureReason: null,
  createdAt: '2026-05-07T10:00:00Z',
  expiresAt: '2026-05-08T10:00:00Z',
};

function makeWrapper(qc: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

describe('usePhotoUpload', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls uploadPhoto and resolves artifact', async () => {
    vi.mocked(photosApi.uploadPhoto).mockResolvedValueOnce(fakeArtifact);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => usePhotoUpload(CAMPAIGN_ID), {
      wrapper: makeWrapper(qc),
    });

    const file = new File(['bytes'], 'photo.jpg', { type: 'image/jpeg' });
    result.current.mutate({ file, pageType: 'Storybook' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(photosApi.uploadPhoto).toHaveBeenCalledWith(CAMPAIGN_ID, file, 'Storybook');
    expect(result.current.data?.id).toBe(PHOTO_ID);
  });

  it('invalidates gamebook photos query on success', async () => {
    vi.mocked(photosApi.uploadPhoto).mockResolvedValueOnce(fakeArtifact);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');

    const { result } = renderHook(() => usePhotoUpload(CAMPAIGN_ID), {
      wrapper: makeWrapper(qc),
    });

    const file = new File(['bytes'], 'photo.jpg', { type: 'image/jpeg' });
    result.current.mutate({ file, pageType: 'Encounter' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: ['gamebook', 'photos', CAMPAIGN_ID] })
    );
  });

  it('surfaces error on failure', async () => {
    vi.mocked(photosApi.uploadPhoto).mockRejectedValueOnce(new Error('upload failed 413'));
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => usePhotoUpload(CAMPAIGN_ID), {
      wrapper: makeWrapper(qc),
    });

    result.current.mutate({ file: new File([], 'x.jpg'), pageType: 'Storybook' });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toMatch(/upload failed/);
  });
});
