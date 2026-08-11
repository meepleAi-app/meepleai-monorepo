/**
 * Admin Cover Picker Schemas (Epic #3470 — Slice 1d-c)
 *
 * Zod schemas for the admin cover-source picker read/write contract. Mirrors the
 * merged backend records (CoverCandidatesDto, CoverAssignmentDto, AssignCoverRequest).
 *
 * Contract notes (verified against the API):
 * - Enums serialize as PascalCase names (global JsonStringEnumConverter, no naming policy).
 * - PropertyNamingPolicy=CamelCase; no DefaultIgnoreCondition → nullable fields are
 *   emitted as `null` (not omitted). We still mark them `.nullable().optional()` to stay
 *   robust against mocks and a future WhenWritingNull change.
 * - `previewUrl` is a presigned R2 URL and is always non-null.
 */

import { z } from 'zod';

/** The 4 materialized cover sources an admin may pin (CoverAssignmentSource). */
export const CoverAssignmentSourceSchema = z.enum(['Pdf', 'Bgg', 'Wikidata', 'Manual']);
export type CoverAssignmentSource = z.infer<typeof CoverAssignmentSourceSchema>;

/** The 3 UI contexts a cover can be assigned for (CoverContext). */
export const CoverContextSchema = z.enum(['Card', 'Hero', 'Social']);
export type CoverContext = z.infer<typeof CoverContextSchema>;

/** A single materialized cover source available to the picker (CoverCandidateDto). */
export const CoverCandidateSchema = z.object({
  source: CoverAssignmentSourceSchema,
  previewUrl: z.string(),
  license: z.string().nullable().optional(),
  attribution: z.string().nullable().optional(),
  sourceUrl: z.string().nullable().optional(),
});
export type CoverCandidate = z.infer<typeof CoverCandidateSchema>;

/**
 * A single per-context admin assignment: the pinned source + the persisted focal point
 * (CoverContextAssignmentDto, epic #3470 Slice 2e / AC-5). Exposing the focal lets the
 * picker pre-fill the saved value instead of always starting at 0.5/0.5.
 */
export const CoverContextAssignmentSchema = z.object({
  source: CoverAssignmentSourceSchema,
  focalX: z.number(),
  focalY: z.number(),
});
export type CoverContextAssignment = z.infer<typeof CoverContextAssignmentSchema>;

/** Which assignment is currently pinned per context; null = implicit precedence (CoverContextAssignmentsDto). */
export const CoverContextAssignmentsSchema = z.object({
  card: CoverContextAssignmentSchema.nullable().optional(),
  hero: CoverContextAssignmentSchema.nullable().optional(),
  social: CoverContextAssignmentSchema.nullable().optional(),
});
export type CoverContextAssignments = z.infer<typeof CoverContextAssignmentsSchema>;

/** The cover-picker read shape (CoverCandidatesDto). */
export const CoverCandidatesSchema = z.object({
  gameId: z.string().uuid(),
  candidates: z.array(CoverCandidateSchema),
  assignments: CoverContextAssignmentsSchema,
});
export type CoverCandidates = z.infer<typeof CoverCandidatesSchema>;

/** The persisted per-context assignment returned by the write command (CoverAssignmentDto). */
export const CoverAssignmentSchema = z.object({
  context: CoverContextSchema,
  source: CoverAssignmentSourceSchema,
  focalX: z.number(),
  focalY: z.number(),
});
export type CoverAssignment = z.infer<typeof CoverAssignmentSchema>;

/** Request body for the cover-picker upsert (AssignCoverRequest). Focal point is normalized 0..1. */
export const AssignCoverRequestSchema = z.object({
  source: CoverAssignmentSourceSchema,
  focalX: z.number().min(0).max(1),
  focalY: z.number().min(0).max(1),
});
export type AssignCoverRequest = z.infer<typeof AssignCoverRequestSchema>;

/**
 * Request body for the manual (arbitrary-URL) cover set (SetManualCoverRequest, epic #3470
 * Slice 3d). `sourceUrl` must be an absolute HTTPS URL and `license` must be on the DEC-3c
 * whitelist (Public Domain / CC0 / CC-BY / CC-BY-SA) — both re-validated server-side (400 on
 * bad input), so this is the FE-side shape, not the authoritative gate.
 */
export const SetManualCoverRequestSchema = z.object({
  sourceUrl: z.string().url(),
  license: z.string().min(1),
  attribution: z.string().nullable().optional(),
});
export type SetManualCoverRequest = z.infer<typeof SetManualCoverRequestSchema>;

/**
 * The persisted manual cover returned by the write command (ManualCoverResult): the suffix-free
 * DB key + a presigned R2 preview URL (may be empty but is always present). The picker refetches
 * candidates on success, so the returned value is informational.
 */
export const ManualCoverResultSchema = z.object({
  dbKey: z.string(),
  presignedUrl: z.string(),
});
export type ManualCoverResult = z.infer<typeof ManualCoverResultSchema>;

/**
 * #3590 — cover-gap: la causa per cui un gioco è privo di cover. Enum bounded, speculare a
 * `CoverGapCauses` lato backend (`GetCoverGapQuery.cs`): cambiarne uno richiede di cambiare
 * anche l'altro.
 *
 * - `pdf_too_large`      — il PDF eccede il limite del servizio di estrazione
 * - `heuristic_rejected` — nessuna pagina è una cover accettabile (esito CORRETTO, non un guasto)
 * - `no_source`          — nessun PDF collegato: non c'è nulla da cui generare
 * - `other`              — PDF ancora in lavorazione o fallimenti non classificati
 */
export const CoverGapCauseSchema = z.enum([
  'pdf_too_large',
  'heuristic_rejected',
  'no_source',
  'other',
]);
export type CoverGapCause = z.infer<typeof CoverGapCauseSchema>;

/**
 * Un gioco senza alcuna cover. I campi PDF sono nullable perché il gruppo `no_source` non ha
 * alcun documento collegato — non stringerli a required senza cambiare il DTO C#.
 */
export const CoverGapGameSchema = z.object({
  gameId: z.string().uuid(),
  title: z.string(),
  bggId: z.number().nullable(),
  cause: CoverGapCauseSchema,
  pdfFileName: z.string().nullable(),
  pdfSizeBytes: z.number().nullable(),
  errorCategory: z.string().nullable(),
});
export type CoverGapGame = z.infer<typeof CoverGapGameSchema>;

/** Pagina di risultati cover-gap (PagedResult&lt;CoverGapGameDto&gt; lato backend). */
export const CoverGapPageSchema = z.object({
  items: z.array(CoverGapGameSchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
});
export type CoverGapPage = z.infer<typeof CoverGapPageSchema>;
