/**
 * Public Mechanic Card schemas (ME-M1.6, Issue #528).
 *
 * Backend endpoint (locked contract, already implemented):
 *   GET /api/v1/games/{gameId:guid}/card   → authenticated (any logged-in user)
 *
 * Responses:
 *   200 → `PublishedMechanicCardDto` (this schema)
 *   404 → no published card OR the card was suppressed/taken down (→ notFound())
 *   401 → unauthenticated (proxy redirects to /login before this; handled defensively)
 *
 * Sections arrive already grouped and ordered by the backend; each claim is
 * already ordered by its ordinal. The section discriminator reuses the same
 * `Faq → "FAQ"` labelling as `mechanic-analyses.schemas.ts` via
 * {@link MECHANIC_SECTION_LABELS} / {@link MechanicSectionSchema}.
 */
import { z } from 'zod';

import { GameIdString } from './common.schemas';
import { MechanicSectionSchema } from './mechanic-analyses.schemas';

// Re-export for convenience so card consumers can import both from one module.
export {
  MECHANIC_SECTION_LABELS,
  MechanicSection,
  MechanicSectionSchema,
} from './mechanic-analyses.schemas';
export type { MechanicSectionValue } from './mechanic-analyses.schemas';

// ========== Nested DTOs ==========

/**
 * A single verbatim quote from the source rulebook backing a claim.
 * `pdfId` + `pdfPage` + `quote` are the exact props the shared
 * `PdfQuoteHighlighter` needs (documentId / page / quote).
 */
export const PublishedMechanicCitationDtoSchema = z.object({
  pdfId: z.string().uuid(),
  pdfPage: z.number().int(),
  quote: z.string(),
});
export type PublishedMechanicCitationDto = z.infer<typeof PublishedMechanicCitationDtoSchema>;

/** A reworded claim plus the citations that ground it. */
export const PublishedMechanicClaimDtoSchema = z.object({
  id: z.string().uuid(),
  claim: z.string(),
  citations: z.array(PublishedMechanicCitationDtoSchema),
});
export type PublishedMechanicClaimDto = z.infer<typeof PublishedMechanicClaimDtoSchema>;

/**
 * One card section (Summary | Mechanics | Victory | Resources | Phases | Faq).
 * `section` tolerates both the PascalCase string name (backend
 * `JsonStringEnumConverter` output) and the numeric enum via
 * {@link MechanicSectionSchema}. Label via {@link MECHANIC_SECTION_LABELS}
 * (maps `Faq → "FAQ"`).
 */
export const PublishedMechanicSectionDtoSchema = z.object({
  section: MechanicSectionSchema,
  claims: z.array(PublishedMechanicClaimDtoSchema),
});
export type PublishedMechanicSectionDto = z.infer<typeof PublishedMechanicSectionDtoSchema>;

// ========== Root DTO ==========

export const PublishedMechanicCardDtoSchema = z.object({
  cardId: z.string().uuid(),
  sharedGameId: GameIdString,
  title: z.string(),
  version: z.number().int(),
  publishedAt: z.string(),
  gameName: z.string(),
  publisher: z.string().nullable(),
  language: z.string(),
  sections: z.array(PublishedMechanicSectionDtoSchema),
  // #530 (ME-M2.1) — extra provenance for the APA-style "copy citation" output.
  // `sourceAnalysisId` comes from the content snapshot (always present). Both
  // `publicationYear` and `documentName` are resolved at read time and MAY be
  // null when unknown — the FE substitutes `[n.d.]` / a sensible fallback.
  sourceAnalysisId: z.string().uuid(),
  publicationYear: z.number().int().nullable(),
  documentName: z.string().nullable(),
});
export type PublishedMechanicCardDto = z.infer<typeof PublishedMechanicCardDtoSchema>;

// ========== Parser ==========

/**
 * Strict parser for the public card DTO. Throws on shape mismatch (mirrors the
 * `httpClient` zod-validate path). Use in client methods / tests that receive a
 * raw JSON object.
 */
export function parsePublishedMechanicCard(data: unknown): PublishedMechanicCardDto {
  return PublishedMechanicCardDtoSchema.parse(data);
}

// ========== Routes ==========

export const MECHANIC_CARD_ROUTES = {
  /** Public, login-gated card endpoint (`gameId` is the sharedGameId). */
  card: (gameId: string) => `/api/v1/games/${gameId}/card`,
} as const;
