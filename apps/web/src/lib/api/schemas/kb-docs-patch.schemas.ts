/**
 * PATCH /api/v1/kb-docs/{id} request schema (Phase 3 #1737, BE PR #1732 #1687).
 *
 * Partial update semantics (D-4 #1687):
 * - JSON `null` (omitted in TS) = no-op
 * - empty string ("") = clear the field
 * - empty array ([]) = clear tags
 *
 * Constraints (D-5/D-8 #1687):
 * - title: max 200 chars
 * - tags: max 20 items, each max 50 chars
 *
 * Allowlist enforcement is BE-side (FE sends raw strings):
 * - documentType: 7-value DocumentCategory enum BE-side (D-6)
 * - language: 10-value LanguageCode whitelist BE-side (D-7)
 */
import { z } from 'zod';

const TITLE_MAX = 200;
const TAG_MAX_LENGTH = 50;
const TAGS_MAX_COUNT = 20;

export const PatchKbDocMetadataRequestSchema = z.object({
  title: z.string().max(TITLE_MAX, `Title must not exceed ${TITLE_MAX} characters.`).optional(),
  documentType: z.string().optional(),
  language: z.string().optional(),
  tags: z
    .array(z.string().max(TAG_MAX_LENGTH, `Tag must not exceed ${TAG_MAX_LENGTH} characters.`))
    .max(TAGS_MAX_COUNT, `No more than ${TAGS_MAX_COUNT} tags allowed.`)
    .optional(),
});

export type PatchKbDocMetadataRequest = z.infer<typeof PatchKbDocMetadataRequestSchema>;
