/**
 * Gamebook Photos API client — Iter 1.B.
 *
 * Endpoints:
 *   - POST /api/v1/gamebook/campaigns/{campaignId}/photos
 *   - POST /api/v1/gamebook/campaigns/{campaignId}/photos/{photoId}/segment
 *
 * SSE translate endpoint is consumed directly by useTranslateSegmentSSE (not here).
 */

import { z } from 'zod';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function ensureOk(res: Response, ctx: string): Promise<Response> {
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(`${ctx} failed ${res.status}: ${text || res.statusText}`);
  }
  return res;
}

// ---------------------------------------------------------------------------
// Schemas
// ---------------------------------------------------------------------------

export const GamebookSegmentSchema = z.object({
  paragraphNumber: z.number().int().min(0),
  sourceText: z.string().min(1),
  boundingBox: z.string().nullable(),
});

export type GamebookSegment = z.infer<typeof GamebookSegmentSchema>;

/**
 * Photo artifact DTO.
 *
 * C5 (gamebook multi-book generalization, 2026-05-19): `pageType` was removed
 * from the BE response — the page bookkeeping now happens via `GameBookId`
 * on the row (passed on upload, returned on subsequent reads). The FE no
 * longer needs to discriminate between Storybook / Encounter at the schema
 * level since the user picks the book up-front via BookPicker.
 */
export const GamebookPhotoArtifactSchema = z.object({
  id: z.string().uuid(),
  campaignId: z.string().uuid(),
  status: z.enum(['Uploaded', 'Segmented', 'Translated', 'Failed']),
  ocrFullText: z.string().nullable(),
  segments: z
    .array(GamebookSegmentSchema)
    .nullable()
    .default([])
    .transform(v => v ?? []),
  failureReason: z.string().nullable(),
  createdAt: z.string(),
  expiresAt: z.string(),
});

export type GamebookPhotoArtifact = z.infer<typeof GamebookPhotoArtifactSchema>;

export const TranslateChunkSchema = z.object({
  delta: z.string().default(''),
  isComplete: z.boolean(),
  paragraphId: z.string().uuid().optional(),
  appliedTerms: z.array(z.string()).optional(),
});

export type TranslateChunk = z.infer<typeof TranslateChunkSchema>;

// ---------------------------------------------------------------------------
// API functions
// ---------------------------------------------------------------------------

/**
 * Upload a photo for a specific GameBook within a campaign.
 *
 * C4/C5 (gamebook multi-book generalization, 2026-05-19): the BE now requires
 * `gameBookId` in the form body (replacing the old `pageType` discriminator).
 * The caller MUST resolve `gameBookId` up-front via `useGameBooks` +
 * `BookPicker` (or single-book auto-select).
 */
export async function uploadPhoto(
  campaignId: string,
  file: File,
  gameBookId: string
): Promise<GamebookPhotoArtifact> {
  const fd = new FormData();
  fd.append('file', file);
  fd.append('gameBookId', gameBookId);
  const res = await fetch(
    `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(campaignId)}/photos`,
    { method: 'POST', body: fd, credentials: 'include' }
  );
  await ensureOk(res, 'uploadPhoto');
  return GamebookPhotoArtifactSchema.parse(await res.json());
}

export async function segmentPhoto(
  campaignId: string,
  photoId: string
): Promise<GamebookPhotoArtifact> {
  const res = await fetch(
    `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(campaignId)}/photos/${encodeURIComponent(photoId)}/segment`,
    { method: 'POST', credentials: 'include' }
  );
  await ensureOk(res, 'segmentPhoto');
  return GamebookPhotoArtifactSchema.parse(await res.json());
}
