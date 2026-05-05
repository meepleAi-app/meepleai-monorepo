/**
 * Gamebook Libro AI Assistant — Zod Schemas (Sprint 1, Task 1.8)
 *
 * Mirrors ASP.NET Core camelCase JSON serialization defaults.
 * Backend DTOs: UploadPhotoBatchCommand, PhotoBatchStatusDto.
 *
 * Endpoint: POST /api/v1/gamebook/{gameId}/photos
 * Endpoint: GET  /api/v1/gamebook/{gameId}/photos/{batchId}/status
 */

import { z } from 'zod';

// ============================================================================
// Upload command — request body sent to POST /api/v1/gamebook/{gameId}/photos
// ============================================================================

export const PhotoUploadItemSchema = z.object({
  /** Base64-encoded image data (without data URI prefix) */
  base64Data: z.string().min(1),
  /** Original file name (e.g. "page-01.jpg") */
  fileName: z.string().min(1),
  /** MIME type (e.g. "image/jpeg") */
  mimeType: z.string().min(1),
});

export type PhotoUploadItem = z.infer<typeof PhotoUploadItemSchema>;

export const UploadPhotoBatchRequestSchema = z.object({
  gameId: z.string().uuid(),
  photos: z.array(PhotoUploadItemSchema).min(1),
});

export type UploadPhotoBatchRequest = z.infer<typeof UploadPhotoBatchRequestSchema>;

// ============================================================================
// Upload response — returned by POST /api/v1/gamebook/{gameId}/photos
// ============================================================================

export const UploadPhotoBatchResponseSchema = z.object({
  batchId: z.string().uuid(),
});

export type UploadPhotoBatchResponse = z.infer<typeof UploadPhotoBatchResponseSchema>;

// ============================================================================
// Status polling — returned by GET /api/v1/gamebook/{gameId}/photos/{batchId}/status
// ============================================================================

/**
 * Terminal statuses where polling should stop.
 */
export const BATCH_TERMINAL_STATUSES = ['Completed', 'Failed', 'Cancelled'] as const;

export const PhotoBatchStatusSchema = z.object({
  batchId: z.string().uuid(),
  /** Processing status: Pending | Processing | Completed | Failed | Cancelled */
  status: z.string(),
  totalPages: z.number().int().nonnegative(),
  processedPages: z.number().int().nonnegative(),
  /** 0–1 confidence score from SmolDocling preprocessing */
  averageConfidence: z.number().min(0).max(1).nullable(),
  errorMessage: z.string().nullable(),
  createdAt: z.string(),
  completedAt: z.string().nullable(),
});

export type PhotoBatchStatus = z.infer<typeof PhotoBatchStatusSchema>;

/**
 * Returns true when processing has reached a terminal state
 * (polling should stop).
 */
export function isBatchTerminal(status: string): boolean {
  return (BATCH_TERMINAL_STATUSES as readonly string[]).includes(status);
}

/**
 * Returns a 0–100 integer progress percentage.
 */
export function batchProgressPercent(dto: PhotoBatchStatus): number {
  if (dto.totalPages === 0) return 0;
  return Math.round((dto.processedPages / dto.totalPages) * 100);
}

// ============================================================================
// Paragraph — returned by GET /api/v1/photo-batches/{batchId}/paragraphs/{pageNumber}
// ============================================================================

/**
 * A single extracted paragraph for a given page of a photo batch.
 * Corresponds to Phase 3 Task 3.5a endpoint (G4 PR #716).
 */
export const ParagraphSchema = z.object({
  pageNumber: z.number().int().positive(),
  text: z.string(),
  fallbackUsed: z.boolean(),
  fallbackMethod: z.string().nullable(),
});

export type Paragraph = z.infer<typeof ParagraphSchema>;
