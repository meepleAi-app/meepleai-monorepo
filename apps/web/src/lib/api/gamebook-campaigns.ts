/**
 * Gamebook Campaigns API client — Iter 1.A.
 *
 * Endpoints exposed by `GamebookCampaignEndpoints`:
 *   - POST   /api/v1/gamebook/campaigns
 *   - GET    /api/v1/gamebook/campaigns?gameId=<guid>
 *   - GET    /api/v1/gamebook/campaigns/{id}
 *   - PUT    /api/v1/gamebook/campaigns/{id}/progress
 *   - PATCH  /api/v1/gamebook/campaigns/{id}        (rename — owner only)
 *   - DELETE /api/v1/gamebook/campaigns/{id}        (soft-delete — owner only)
 *
 * All endpoints require authentication via session cookie (`credentials: 'include'`).
 */

import { z } from 'zod';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export const GamebookCampaignSchema = z.object({
  id: z.string().uuid(),
  /** Issue #1392: discriminator-aware ref id (SharedGame or PrivateGame, see `gameRefKind`). */
  gameRefId: z.string().uuid(),
  /**
   * Issue #1392 + #1406: discriminator mirroring backend `GameRefKind`. Known
   * values are 0 = Shared, 1 = Private. The schema is intentionally widened to
   * accept any nonnegative integer so that a BE-side enum extension (e.g. a
   * future 2 = Hybrid) does not hard-break the play page with a generic Zod
   * error. Unknown values fall back to Shared (0) and emit a one-shot warning
   * so the schema drift surfaces in production logs — the FE then renders the
   * conservative shared-catalog flow rather than crashing.
   */
  gameRefKind: z
    .number()
    .int()
    .nonnegative()
    .transform(value => {
      if (value !== 0 && value !== 1) {
        if (typeof console !== 'undefined') {
          console.warn(
            `[gamebook-campaigns] unknown GameRefKind=${value}; falling back to Shared (0). FE schema may need updating.`
          );
        }
        return 0;
      }
      return value;
    }),
  ownerUserId: z.string().uuid(),
  title: z.string().min(1).max(200),
  currentParagraph: z.number().int().min(0),
  history: z
    .array(z.number().int())
    .nullable()
    .default([])
    .transform(v => v ?? []),
  lastReadAt: z.string().datetime({ offset: true }),
  createdAt: z.string().datetime({ offset: true }),
  updatedAt: z.string().datetime({ offset: true }),
});

export type GamebookCampaign = z.infer<typeof GamebookCampaignSchema>;

export interface CreateCampaignInput {
  gameId: string;
  title: string;
}

async function parseJson<T>(res: Response, schema: z.ZodSchema<T>): Promise<T> {
  if (!res.ok) {
    let detail = '';
    try {
      detail = await res.text();
    } catch {
      /* ignore body read failure */
    }
    throw new Error(`Gamebook campaigns API error ${res.status}: ${detail || res.statusText}`);
  }
  const raw = await res.json();
  return schema.parse(raw);
}

export async function createCampaign(input: CreateCampaignInput): Promise<GamebookCampaign> {
  const res = await fetch(`${API_BASE}/api/v1/gamebook/campaigns`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
    credentials: 'include',
  });
  return parseJson(res, GamebookCampaignSchema);
}

export async function getCampaign(id: string): Promise<GamebookCampaign> {
  const res = await fetch(`${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(id)}`, {
    credentials: 'include',
  });
  return parseJson(res, GamebookCampaignSchema);
}

export async function listMyCampaigns(gameId?: string): Promise<GamebookCampaign[]> {
  const url = gameId
    ? `${API_BASE}/api/v1/gamebook/campaigns?gameId=${encodeURIComponent(gameId)}`
    : `${API_BASE}/api/v1/gamebook/campaigns`;
  const res = await fetch(url, { credentials: 'include' });
  return parseJson(res, z.array(GamebookCampaignSchema));
}

/**
 * Update the current paragraph progress for a campaign, scoped to a specific
 * GameBook (multi-book generalization, C2).
 *
 * The backend persists progress in `SessionBookProgress` indexed by
 * `(campaignId, gameBookId)`, so the caller MUST identify which book this
 * update belongs to. When a campaign has only one book the FE can default
 * `gameBookId` to that single book id; the picker is only shown when 2+
 * books exist.
 */
export async function updateProgress(
  id: string,
  gameBookId: string,
  currentParagraph: number
): Promise<GamebookCampaign> {
  const res = await fetch(
    `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(id)}/progress`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gameBookId, currentParagraph }),
      credentials: 'include',
    }
  );
  return parseJson(res, GamebookCampaignSchema);
}

/**
 * Issue #1388: per-book progress rows for the ResumeBooksList on the play page.
 * Sorted server-side by most recent visit first. Orphan rows (book deleted)
 * are filtered out by the BE handler.
 */
export const SessionBookProgressSchema = z.object({
  bookId: z.string().uuid(),
  bookName: z.string(),
  lastLocation: z.string(),
  lastVisitedAt: z.string().datetime({ offset: true }),
});

export type SessionBookProgressRow = z.infer<typeof SessionBookProgressSchema>;

export async function getCampaignProgress(id: string): Promise<SessionBookProgressRow[]> {
  const res = await fetch(
    `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(id)}/progress`,
    { credentials: 'include' }
  );
  return parseJson(res, z.array(SessionBookProgressSchema));
}

export async function renameCampaign(id: string, title: string): Promise<GamebookCampaign> {
  const res = await fetch(`${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ title }),
    credentials: 'include',
  });
  return parseJson(res, GamebookCampaignSchema);
}

export async function deleteCampaign(id: string): Promise<void> {
  const res = await fetch(`${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    credentials: 'include',
  });
  if (!res.ok) {
    let detail = '';
    try {
      detail = await res.text();
    } catch {
      /* ignore */
    }
    throw new Error(`Gamebook campaigns API error ${res.status}: ${detail || res.statusText}`);
  }
}
