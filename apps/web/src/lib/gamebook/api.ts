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
  ParagraphSchema,
  PhotoBatchStatusSchema,
  UploadPhotoBatchResponseSchema,
  type Paragraph,
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

/**
 * Fetch a single extracted paragraph by physical page number.
 *
 * Endpoint: GET /api/v1/photo-batches/{batchId}/paragraphs/{pageNumber}?hint=...
 * Introduced by G4 (PR #716) — Phase 3 Task 3.5a.
 *
 * @param batchId    - Batch UUID whose pages have been processed
 * @param pageNumber - 1-based page number
 * @param hint       - Optional OCR/translation hint for disambiguation
 * @returns Paragraph DTO, throws on empty / error response
 */
export async function getParagraph(
  batchId: string,
  pageNumber: number,
  hint?: string
): Promise<Paragraph> {
  const url = hint
    ? `/api/v1/photo-batches/${batchId}/paragraphs/${pageNumber}?hint=${encodeURIComponent(hint)}`
    : `/api/v1/photo-batches/${batchId}/paragraphs/${pageNumber}`;
  const result = await apiClient.get<Paragraph>(url, ParagraphSchema);
  if (!result) throw new Error('Empty paragraph response');
  return result;
}

/**
 * Fetch a single extracted paragraph by narrative paragraph number.
 *
 * Endpoint:
 *   GET /api/v1/photo-batches/{batchId}/paragraphs/by-paragraph/{paragraphNumber}?hint=...
 *
 * Introduced by #747 PR-B (`0ba93671a`). Use when the caller indexes content
 * by gamebook paragraph IDs (Tainted Grail, ISS Vanguard, Nanolith) rather
 * than physical photo page index. Backend dispatches via
 * `ParagraphLookupKey.ByParagraphNumber` and falls back to semantic search
 * when no page carries the requested paragraph (response has
 * `fallbackUsed: true`, `fallbackMethod: 'semantic'`, `pageNumber: 0`).
 *
 * @param batchId         - Batch UUID whose pages have been processed
 * @param paragraphNumber - 1-based narrative paragraph number
 * @param hint            - Optional OCR/translation hint for disambiguation
 * @returns Paragraph DTO, throws on empty / error response
 */
export async function getParagraphByParagraphNumber(
  batchId: string,
  paragraphNumber: number,
  hint?: string
): Promise<Paragraph> {
  const base = `/api/v1/photo-batches/${batchId}/paragraphs/by-paragraph/${paragraphNumber}`;
  const url = hint ? `${base}?hint=${encodeURIComponent(hint)}` : base;
  const result = await apiClient.get<Paragraph>(url, ParagraphSchema);
  if (!result) throw new Error('Empty paragraph response');
  return result;
}
