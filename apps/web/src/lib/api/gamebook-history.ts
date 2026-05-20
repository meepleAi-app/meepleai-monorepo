/**
 * Gamebook History API client — Iter 1.B.
 *
 * Endpoint:
 *   - GET /api/v1/gamebook/campaigns/{campaignId}/history
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
// Schema
// ---------------------------------------------------------------------------

/**
 * C5 (multi-book generalization 2026-05-19): BE response no longer carries
 * `pageType` — paragraphs are now keyed by `GameBookId` server-side, with
 * the FE relying on the picker to scope inputs.
 */
export const TranslatedParagraphSchema = z.object({
  id: z.string().uuid(),
  paragraphNumber: z.number().int().min(0),
  sourceTextEn: z.string(),
  translatedTextIt: z.string(),
  appliedGlossaryTerms: z
    .array(z.string())
    .nullable()
    .default([])
    .transform(v => v ?? []),
  createdAt: z.string(),
});

export type TranslatedParagraph = z.infer<typeof TranslatedParagraphSchema>;

// ---------------------------------------------------------------------------
// API functions
// ---------------------------------------------------------------------------

export async function listHistory(campaignId: string): Promise<TranslatedParagraph[]> {
  const res = await fetch(
    `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(campaignId)}/history`,
    { credentials: 'include' }
  );
  await ensureOk(res, 'listHistory');
  return z.array(TranslatedParagraphSchema).parse(await res.json());
}
