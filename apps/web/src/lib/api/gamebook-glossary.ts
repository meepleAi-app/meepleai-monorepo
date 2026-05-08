/**
 * Gamebook Glossary API client — Iter 1.B.
 *
 * Endpoints:
 *   - GET  /api/v1/gamebook/campaigns/{campaignId}/glossary
 *   - POST /api/v1/gamebook/campaigns/{campaignId}/glossary/bootstrap
 *   - PUT  /api/v1/gamebook/campaigns/{campaignId}/glossary/{entryId}
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

export const GamebookGlossaryEntrySchema = z.object({
  id: z.string().uuid(),
  termEn: z.string().min(1),
  termIt: z.string().min(1),
  source: z.enum(['AutoBootstrap', 'Manual']),
  updatedAt: z.string(),
});

export type GamebookGlossaryEntry = z.infer<typeof GamebookGlossaryEntrySchema>;

// ---------------------------------------------------------------------------
// API functions
// ---------------------------------------------------------------------------

export async function listGlossary(campaignId: string): Promise<GamebookGlossaryEntry[]> {
  const res = await fetch(
    `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(campaignId)}/glossary`,
    { credentials: 'include' }
  );
  await ensureOk(res, 'listGlossary');
  return z.array(GamebookGlossaryEntrySchema).parse(await res.json());
}

export async function bootstrapGlossary(campaignId: string): Promise<GamebookGlossaryEntry[]> {
  const res = await fetch(
    `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(campaignId)}/glossary/bootstrap`,
    { method: 'POST', credentials: 'include' }
  );
  await ensureOk(res, 'bootstrapGlossary');
  return z.array(GamebookGlossaryEntrySchema).parse(await res.json());
}

export async function upsertGlossary(
  campaignId: string,
  entryId: string,
  body: { termEn: string; termIt: string }
): Promise<GamebookGlossaryEntry> {
  const res = await fetch(
    `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(campaignId)}/glossary/${encodeURIComponent(entryId)}`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      credentials: 'include',
    }
  );
  await ensureOk(res, 'upsertGlossary');
  return GamebookGlossaryEntrySchema.parse(await res.json());
}
