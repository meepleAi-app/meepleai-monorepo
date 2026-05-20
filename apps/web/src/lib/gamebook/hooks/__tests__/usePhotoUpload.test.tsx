import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import * as photosApi from '@/lib/api/gamebook-photos';
import * as stripperApi from '@/lib/gamebook/clientExifStripper';

import { usePhotoUpload, PhotoUploadStripError } from '../usePhotoUpload';

vi.mock('@/lib/api/gamebook-photos');
vi.mock('@/lib/gamebook/clientExifStripper');

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

  it('strips EXIF then uploads the stripped file (happy path)', async () => {
    const original = new File(['bytes'], 'photo.jpg', { type: 'image/jpeg' });
    const stripped = new File(['stripped'], 'photo.jpg', { type: 'image/jpeg' });
    vi.mocked(stripperApi.stripExif).mockResolvedValueOnce({
      kind: 'success',
      file: stripped,
      tagsRemoved: 5,
    });
    vi.mocked(photosApi.uploadPhoto).mockResolvedValueOnce(fakeArtifact);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => usePhotoUpload(CAMPAIGN_ID), {
      wrapper: makeWrapper(qc),
    });

    result.current.mutate({ file: original, pageType: 'Storybook' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(stripperApi.stripExif).toHaveBeenCalledWith(original);
    expect(photosApi.uploadPhoto).toHaveBeenCalledWith(CAMPAIGN_ID, stripped, 'Storybook');
    expect(result.current.data?.id).toBe(PHOTO_ID);
  });

  it('uploads original file on fallback-no-exif (no re-encode cost)', async () => {
    const file = new File(['bytes'], 'screenshot.png', { type: 'image/png' });
    vi.mocked(stripperApi.stripExif).mockResolvedValueOnce({
      kind: 'fallback-no-exif',
      file,
    });
    vi.mocked(photosApi.uploadPhoto).mockResolvedValueOnce(fakeArtifact);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => usePhotoUpload(CAMPAIGN_ID), {
      wrapper: makeWrapper(qc),
    });

    result.current.mutate({ file, pageType: 'Storybook' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(photosApi.uploadPhoto).toHaveBeenCalledWith(CAMPAIGN_ID, file, 'Storybook');
  });

  it('invalidates gamebook photos query on success', async () => {
    const file = new File(['bytes'], 'photo.jpg', { type: 'image/jpeg' });
    vi.mocked(stripperApi.stripExif).mockResolvedValueOnce({
      kind: 'fallback-no-exif',
      file,
    });
    vi.mocked(photosApi.uploadPhoto).mockResolvedValueOnce(fakeArtifact);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');

    const { result } = renderHook(() => usePhotoUpload(CAMPAIGN_ID), {
      wrapper: makeWrapper(qc),
    });

    result.current.mutate({ file, pageType: 'Encounter' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({ queryKey: ['gamebook', 'photos', CAMPAIGN_ID] })
    );
  });

  it('aborts upload with PhotoUploadStripError on error-unsupported (HEIC)', async () => {
    const file = new File(['bytes'], 'live-photo.heic', { type: 'image/heic' });
    vi.mocked(stripperApi.stripExif).mockResolvedValueOnce({
      kind: 'error-unsupported',
      format: 'heic',
    });

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => usePhotoUpload(CAMPAIGN_ID), {
      wrapper: makeWrapper(qc),
    });

    result.current.mutate({ file, pageType: 'Storybook' });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(photosApi.uploadPhoto).not.toHaveBeenCalled();
    expect(result.current.error).toBeInstanceOf(PhotoUploadStripError);
    const err = result.current.error as PhotoUploadStripError;
    expect(err.kind).toBe('error-unsupported');
    expect(err.detail).toBe('heic');
    expect(err.message).toMatch(/convert to JPEG/);
  });

  it('aborts upload with PhotoUploadStripError on error-corrupted', async () => {
    const file = new File(['garbage'], 'broken.jpg', { type: 'image/jpeg' });
    vi.mocked(stripperApi.stripExif).mockResolvedValueOnce({
      kind: 'error-corrupted',
      reason: 'Not a valid JPEG file',
    });

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => usePhotoUpload(CAMPAIGN_ID), {
      wrapper: makeWrapper(qc),
    });

    result.current.mutate({ file, pageType: 'Storybook' });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(photosApi.uploadPhoto).not.toHaveBeenCalled();
    expect(result.current.error).toBeInstanceOf(PhotoUploadStripError);
    const err = result.current.error as PhotoUploadStripError;
    expect(err.kind).toBe('error-corrupted');
    expect(err.detail).toBe('Not a valid JPEG file');
  });

  it('surfaces upload-failure error after stripper succeeds', async () => {
    const file = new File(['bytes'], 'photo.jpg', { type: 'image/jpeg' });
    vi.mocked(stripperApi.stripExif).mockResolvedValueOnce({
      kind: 'fallback-no-exif',
      file,
    });
    vi.mocked(photosApi.uploadPhoto).mockRejectedValueOnce(new Error('upload failed 413'));

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => usePhotoUpload(CAMPAIGN_ID), {
      wrapper: makeWrapper(qc),
    });

    result.current.mutate({ file, pageType: 'Storybook' });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toMatch(/upload failed/);
  });
});
