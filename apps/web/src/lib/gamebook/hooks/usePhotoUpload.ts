'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';

import { uploadPhoto, type GamebookPhotoArtifact } from '@/lib/api/gamebook-photos';
import { stripExif } from '@/lib/gamebook/clientExifStripper';

/**
 * C4 (multi-book generalization 2026-05-19): photo uploads are now scoped by
 * `gameBookId` instead of the old `pageType` discriminator. Callers must
 * resolve the book id via `useGameBooks` + `BookPicker` before triggering
 * the upload.
 */
export interface UploadPhotoArgs {
  file: File;
  gameBookId: string;
}

/**
 * Privacy-safe error class — thrown by usePhotoUpload mutationFn when the
 * client-side EXIF stripper rejects the file (HEIC/HEIF/AVIF unsupported,
 * or malformed EXIF). React Query routes this to `onError`, where the UI
 * surfaces the appropriate toast. Issue #834.
 */
export class PhotoUploadStripError extends Error {
  constructor(
    public readonly kind: 'error-corrupted' | 'error-unsupported',
    public readonly detail: string
  ) {
    super(
      kind === 'error-unsupported'
        ? `Unsupported format: ${detail}. Please convert to JPEG.`
        : `Image format not supported: ${detail}`
    );
    this.name = 'PhotoUploadStripError';
  }
}

export function usePhotoUpload(campaignId: string) {
  const qc = useQueryClient();
  return useMutation<GamebookPhotoArtifact, Error, UploadPhotoArgs>({
    mutationFn: async ({ file, gameBookId }) => {
      const stripped = await stripExif(file);
      switch (stripped.kind) {
        case 'success':
        case 'fallback-no-exif':
          return uploadPhoto(campaignId, stripped.file, gameBookId);
        case 'error-unsupported':
          throw new PhotoUploadStripError('error-unsupported', stripped.format);
        case 'error-corrupted':
          throw new PhotoUploadStripError('error-corrupted', stripped.reason);
      }
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['gamebook', 'photos', campaignId] });
    },
  });
}
