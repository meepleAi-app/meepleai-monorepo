import type { ProcessingState } from '@/lib/api/schemas/kb-docs.schemas';

/**
 * Canonical display status for a KB/PDF document across the app (card badges,
 * drawer, library). Consolidates the three previously-divergent mappers
 * (extra-meeple-card/drawer-helpers, library/kb-utils, use-kb-detail). #2860.
 */
export type KbDisplayStatus = 'processing' | 'indexed' | 'failed' | 'none';

/**
 * Exhaustive over the canonical ProcessingState enum (lowercased). The
 * `satisfies` clause makes a newly-added ProcessingState value a compile error
 * here until it is mapped. Pending -> processing is the resolved canonical value.
 */
const CANONICAL = {
  pending: 'processing',
  uploading: 'processing',
  extracting: 'processing',
  chunking: 'processing',
  embedding: 'processing',
  indexing: 'processing',
  ready: 'indexed',
  failed: 'failed',
} satisfies Record<Lowercase<ProcessingState>, KbDisplayStatus>;

/**
 * Legacy / alternate payload spellings. The /api/v1/pdfs/{id}/text endpoint
 * emits lowercase `uploaded`; some list endpoints emit `completed`/`processing`.
 */
const ALIASES: Record<string, KbDisplayStatus> = {
  completed: 'indexed',
  uploaded: 'processing',
  processing: 'processing',
};

/**
 * Map any PDF processing-state string (canonical PascalCase, lowercase, or a
 * known alias) to a KbDisplayStatus. Unknown / empty -> 'none'.
 */
export function mapProcessingStateToDisplayStatus(
  state: string | null | undefined
): KbDisplayStatus {
  const key = String(state ?? '')
    .trim()
    .toLowerCase();
  return (CANONICAL as Record<string, KbDisplayStatus>)[key] ?? ALIASES[key] ?? 'none';
}
