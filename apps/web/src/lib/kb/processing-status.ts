import type { ProcessingState } from '@/lib/api/schemas/kb-docs.schemas';
import type { PdfState } from '@/types/pdf';

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
 * Fine-grained UI state for the PDF badges. The canonical `ProcessingState` values are the same
 * eight words as `PdfState`, only differently cased; `satisfies` makes a newly-added
 * ProcessingState value a compile error here instead of a silent `pending`.
 */
const PDF_STATE = {
  pending: 'pending',
  uploading: 'uploading',
  extracting: 'extracting',
  chunking: 'chunking',
  embedding: 'embedding',
  indexing: 'indexing',
  ready: 'ready',
  failed: 'failed',
} satisfies Record<Lowercase<ProcessingState>, PdfState>;

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

/**
 * Map a PDF processing-state string (canonical PascalCase, lowercase, or the `completed` alias)
 * to the fine-grained `PdfState` the status badges take. Unknown / empty -> 'pending'.
 */
export function mapProcessingStateToPdfState(state: string | null | undefined): PdfState {
  const key = String(state ?? '')
    .trim()
    .toLowerCase();
  if (key === 'completed') return 'ready';
  return (PDF_STATE as Record<string, PdfState>)[key] ?? 'pending';
}
