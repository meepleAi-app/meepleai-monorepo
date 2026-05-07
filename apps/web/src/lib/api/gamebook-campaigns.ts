/**
 * Gamebook Campaigns API client — Iter 1.A.
 *
 * Endpoints exposed by `GamebookCampaignEndpoints`:
 *   - POST /api/v1/gamebook/campaigns
 *   - GET  /api/v1/gamebook/campaigns?gameId=<guid>
 *   - GET  /api/v1/gamebook/campaigns/{id}
 *   - PUT  /api/v1/gamebook/campaigns/{id}/progress
 *
 * All endpoints require authentication via session cookie (`credentials: 'include'`).
 */

import { z } from 'zod';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export const GamebookCampaignSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
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

export async function updateProgress(
  id: string,
  currentParagraph: number
): Promise<GamebookCampaign> {
  const res = await fetch(
    `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(id)}/progress`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ currentParagraph }),
      credentials: 'include',
    }
  );
  return parseJson(res, GamebookCampaignSchema);
}
