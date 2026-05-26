/**
 * Gamebook Encounter API client — Issue #1484 (consumes BE #1520).
 *
 * Endpoint:
 *   - POST /api/v1/gamebook/campaigns/{campaignId}/photos/{photoId}/encounter-parse
 *
 * Synchronous, ephemeral cheatsheet extraction: the BE returns a fully-formed
 * {@link EncounterCheatsheet} in a single response and persists nothing
 * (Encounter Book privacy, spec §9.1). Unlike the SSE translate flow there is
 * no streaming and no long-term cache.
 *
 * All stat values are strings: the LLM extractor preserves raw textual tokens
 * (e.g. "+3", "5+1", "—", "see §220") and the FE renders them verbatim.
 */

import { z } from 'zod';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

/**
 * Typed error carrying the HTTP status so the orchestrator can distinguish the
 * recoverable branches surfaced by the BE: 409 (LLM parse failure → retry),
 * 404 (photo/segment missing → re-capture), 403/401 (auth/ownership).
 */
export class EncounterParseError extends Error {
  public readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = 'EncounterParseError';
    this.status = status;
  }
}

// ---------------------------------------------------------------------------
// Schemas (mirror EncounterCheatsheetDto, camelCase-serialised)
// ---------------------------------------------------------------------------

export const EncounterDiceRollSchema = z.object({
  sides: z.number().int(),
  count: z.number().int(),
  modifier: z.number().int(),
  threshold: z.number().int(),
});

export type EncounterDiceRoll = z.infer<typeof EncounterDiceRollSchema>;

export const EncounterEnemySchema = z.object({
  name: z.string(),
  icon: z
    .string()
    .nullish()
    .transform(v => v ?? null),
  paragraphMarker: z
    .string()
    .nullish()
    .transform(v => v ?? null),
  hp: z.string(),
  atk: z.string(),
  def: z.string(),
  mov: z.string(),
});

export type EncounterEnemy = z.infer<typeof EncounterEnemySchema>;

export const EncounterOptionSchema = z.object({
  label: z.string(),
  diceRoll: EncounterDiceRollSchema.nullish().transform(v => v ?? null),
  outcome: z
    .string()
    .nullish()
    .transform(v => v ?? null),
});

export type EncounterOption = z.infer<typeof EncounterOptionSchema>;

export const EncounterConditionsSchema = z.object({
  win: z
    .string()
    .nullish()
    .transform(v => v ?? null),
  loss: z
    .string()
    .nullish()
    .transform(v => v ?? null),
});

export type EncounterConditions = z.infer<typeof EncounterConditionsSchema>;

export const EncounterConfidenceSchema = z.object({
  enemies: z.number(),
  options: z.number(),
  conditions: z.number(),
});

export type EncounterConfidence = z.infer<typeof EncounterConfidenceSchema>;

export const EncounterCheatsheetSchema = z.object({
  enemies: z
    .array(EncounterEnemySchema)
    .nullable()
    .default([])
    .transform(v => v ?? []),
  options: z
    .array(EncounterOptionSchema)
    .nullable()
    .default([])
    .transform(v => v ?? []),
  conditions: EncounterConditionsSchema,
  confidence: EncounterConfidenceSchema,
});

export type EncounterCheatsheet = z.infer<typeof EncounterCheatsheetSchema>;

// ---------------------------------------------------------------------------
// API function
// ---------------------------------------------------------------------------

export interface ParseEncounterBody {
  /** 1-based paragraph number within the photographed encounter page. */
  paragraphNumber: number;
  /** GameBook the campaign is currently playing (scopes the parse). */
  gameBookId: string;
}

/**
 * Parse an encounter cheatsheet from a previously-segmented photo paragraph.
 *
 * @throws {EncounterParseError} on any non-2xx response, carrying the status.
 */
export async function parseEncounter(
  campaignId: string,
  photoId: string,
  body: ParseEncounterBody
): Promise<EncounterCheatsheet> {
  const res = await fetch(
    `${API_BASE}/api/v1/gamebook/campaigns/${encodeURIComponent(campaignId)}/photos/${encodeURIComponent(photoId)}/encounter-parse`,
    {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        paragraphNumber: body.paragraphNumber,
        gameBookId: body.gameBookId,
      }),
    }
  );

  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new EncounterParseError(
      res.status,
      `parseEncounter failed ${res.status}: ${text || res.statusText}`
    );
  }

  return EncounterCheatsheetSchema.parse(await res.json());
}
