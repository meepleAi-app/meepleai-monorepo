/**
 * Gamebook Libro AI Assistant — API wrapper (Sprint 1, Task 1.8)
 *
 * Uses the shared HttpClient from core/httpClient.
 * Endpoints:
 *   POST /api/v1/gamebook/{gameId}/photos          → upload batch
 *   GET  /api/v1/gamebook/{gameId}/photos/{batchId}/status → poll status
 */

import { apiClient } from '@/lib/api/client';

import {
  PhotoBatchStatusSchema,
  UploadPhotoBatchResponseSchema,
  type PhotoUploadItem,
  type PhotoBatchStatus,
  type UploadPhotoBatchResponse,
} from './schemas';

/**
 * Upload a batch of photos for a given game.
 *
 * @param gameId - Target game UUID
 * @param photos - Array of base64-encoded photo items
 * @returns Resolved batch ID
 */
export async function uploadPhotoBatch(
  gameId: string,
  photos: PhotoUploadItem[]
): Promise<UploadPhotoBatchResponse> {
  return apiClient.post(
    `/api/v1/gamebook/${gameId}/photos`,
    { gameId, photos },
    UploadPhotoBatchResponseSchema
  );
}

/**
 * Poll the processing status of a photo batch.
 *
 * @param gameId  - Target game UUID
 * @param batchId - Batch UUID returned by uploadPhotoBatch
 * @returns Current batch status DTO, or null if not found / 401
 */
export async function getPhotoBatchStatus(
  gameId: string,
  batchId: string
): Promise<PhotoBatchStatus | null> {
  return apiClient.get(
    `/api/v1/gamebook/${gameId}/photos/${batchId}/status`,
    PhotoBatchStatusSchema
  );
}
