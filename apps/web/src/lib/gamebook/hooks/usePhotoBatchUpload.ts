/**
 * Gamebook — usePhotoBatchUpload hook (Sprint 1, Task 1.8)
 *
 * TanStack Query v5 mutation wrapping the photo batch upload endpoint.
 * Converts File[] → base64 items → POST /api/v1/gamebook/{gameId}/photos.
 */

'use client';

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { uploadPhotoBatch } from '../api';
import { filesToPhotoItems } from '../file-to-base64';

import type { PhotoUploadItem, UploadPhotoBatchResponse } from '../schemas';

export interface UploadPhotoBatchVariables {
  gameId: string;
  files: File[];
}

export interface UsePhotoBatchUploadOptions {
  onSuccess?: (data: UploadPhotoBatchResponse, variables: UploadPhotoBatchVariables) => void;
  onError?: (error: Error, variables: UploadPhotoBatchVariables) => void;
}

/**
 * Mutation hook for uploading a batch of game-manual photos.
 *
 * @example
 * ```tsx
 * const { mutate, isPending } = usePhotoBatchUpload({
 *   onSuccess: ({ batchId }) => setBatchId(batchId),
 * });
 *
 * // In a submit handler:
 * mutate({ gameId: '...', files: selectedFiles });
 * ```
 */
export function usePhotoBatchUpload(
  options?: UsePhotoBatchUploadOptions
): UseMutationResult<UploadPhotoBatchResponse, Error, UploadPhotoBatchVariables> {
  return useMutation<UploadPhotoBatchResponse, Error, UploadPhotoBatchVariables>({
    mutationFn: async ({ gameId, files }: UploadPhotoBatchVariables) => {
      const photos: PhotoUploadItem[] = await filesToPhotoItems(files);
      return uploadPhotoBatch(gameId, photos);
    },
    onSuccess: options?.onSuccess,
    onError: options?.onError,
  });
}
