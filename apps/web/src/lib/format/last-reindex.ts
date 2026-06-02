/**
 * formatLastReindex — #1676 scope (a) F2 (mockup `sp5-admin-kb.html` L152).
 *
 * Returns a humanized "last reindex" label for the KbDocDetailPanel meta-footer.
 *
 * Cases:
 *   - lastIngestedAt EQUALS uploadedAt → upload-only fallback (doc never re-indexed
 *     after initial ingestion). Label: "📤 upload only".
 *   - lastIngestedAt differs from uploadedAt → reindex label using the project's
 *     standard Italian relative-date formatter. Label: "🔄 last reindex {relative}".
 *   - invalid/null/undefined input → "—" (em dash).
 *
 * Two-date semantics mirror the BE fallback chain in
 * `GetKbDocumentByIdHandler:153`: `IndexedAt ?? ProcessedAt ?? UploadedAt`.
 * When the chain falls through to UploadedAt, both timestamps coincide → the
 * "upload only" path is taken.
 */

import { isValid } from 'date-fns';

import { formatRelativeDate } from '@/lib/play-records/formatRelativeDate';

const EM_DASH = '—';

export interface LastReindexLabel {
  readonly kind: 'reindex' | 'upload-only' | 'unknown';
  readonly label: string;
}

function toDate(input: string | Date | null | undefined): Date | null {
  if (input === null || input === undefined) return null;
  const date = typeof input === 'string' ? new Date(input) : input;
  return isValid(date) ? date : null;
}

export function formatLastReindex(
  lastIngestedAt: string | Date | null | undefined,
  uploadedAt: string | Date | null | undefined
): LastReindexLabel {
  const last = toDate(lastIngestedAt);
  const uploaded = toDate(uploadedAt);

  if (last === null) {
    return { kind: 'unknown', label: EM_DASH };
  }

  // Upload-only: BE fallback chain bottomed-out on UploadedAt → both coincide.
  // Use millisecond equality (timestamps are ISO-string round-tripped).
  if (uploaded !== null && last.getTime() === uploaded.getTime()) {
    return { kind: 'upload-only', label: '📤 upload only' };
  }

  return {
    kind: 'reindex',
    label: `🔄 last reindex ${formatRelativeDate(last)}`,
  };
}
