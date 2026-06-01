/**
 * Gamebook — useGetParagraph hook (Phase 3, Task 3.5a)
 *
 * TanStack Query v5 hook for fetching a single paragraph from a processed
 * photo batch. Paragraphs are stable (5-min staleTime) and typically only
 * re-fetched when the page number or batch changes.
 *
 * Endpoints (issue #1303 — discriminated lookup):
 *   GET /api/v1/photo-batches/{batchId}/paragraphs/{pageNumber}?hint=...
 *   GET /api/v1/photo-batches/{batchId}/paragraphs/by-paragraph/{paragraphNumber}?hint=...
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { getParagraph, getParagraphByParagraphNumber } from '../api';

import type { Paragraph } from '../schemas';

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/**
 * Discriminated lookup union for paragraph retrieval.
 *
 * - `'page'`  → physical photo page index (legacy, 1-based)
 * - `'paragraph'` → narrative paragraph number extracted from OCR text
 *
 * Issue #1303 (FE follow-up of #747 PR-B): the discriminator selects the
 * backend route (`/paragraphs/{n}` vs `/paragraphs/by-paragraph/{n}`).
 */
export type ParagraphRef =
  | { readonly type: 'page'; readonly value: number }
  | { readonly type: 'paragraph'; readonly value: number };

export interface UseGetParagraphOptions {
  batchId: string | undefined;
  paragraphRef: ParagraphRef;
  hint?: string;
  enabled?: boolean;
}

/**
 * Legacy options shape preserved for one release to give existing callers
 * time to migrate. Triggers a one-time `console.warn` per session in dev.
 *
 * @deprecated Use the `paragraphRef: { type: 'page', value }` shape instead.
 */
export interface UseGetParagraphLegacyOptions {
  batchId: string | undefined;
  /** @deprecated Pass `paragraphRef: { type: 'page', value: pageNumber }` instead. */
  pageNumber: number;
  hint?: string;
  enabled?: boolean;
}

// ---------------------------------------------------------------------------
// Query key factory
// ---------------------------------------------------------------------------

/**
 * Query key factory for paragraph lookups.
 *
 * Page-based and paragraph-based keys live in separate namespaces so a
 * collision like `byPage(b, 5) === byParagraph(b, 5)` cannot happen. The
 * URL paths differ on the wire too, so this is a defensive guarantee, not
 * an optimisation.
 */
export const paragraphKeys = {
  byPage: (batchId: string, pageNumber: number, hint?: string) =>
    ['gamebook', 'paragraph', 'byPage', batchId, pageNumber, hint ?? ''] as const,
  byParagraph: (batchId: string, paragraphNumber: number, hint?: string) =>
    ['gamebook', 'paragraph', 'byParagraph', batchId, paragraphNumber, hint ?? ''] as const,
};

// ---------------------------------------------------------------------------
// Deprecation warning (dev-only, one warn per (batchId,pageNumber) per session)
// ---------------------------------------------------------------------------

/** Track deprecation warnings already emitted in this session. */
const emittedDeprecationKeys = new Set<string>();

/**
 * Reset the deprecation tracking — test-only helper to keep `vi.spyOn(console)`
 * assertions deterministic between tests within the same module.
 *
 * @internal exported solely for test isolation; do not call from production code.
 */
export function __resetDeprecationWarnings(): void {
  emittedDeprecationKeys.clear();
}

function warnLegacyPageNumberOnce(batchId: string | undefined, pageNumber: number): void {
  // Dev-only: production logs are noisy and the warn is meant for engineers
  // staring at their browser console during migration.
  if (process.env.NODE_ENV === 'production') return;

  const key = `${batchId ?? ''}:${pageNumber}`;
  if (emittedDeprecationKeys.has(key)) return;
  emittedDeprecationKeys.add(key);

  console.warn(
    '[useGetParagraph] The `pageNumber` argument is deprecated. ' +
      "Use `paragraphRef: { type: 'page', value }` instead. " +
      'This compatibility shim will be removed in a future release. (issue #1303)'
  );
}

// ---------------------------------------------------------------------------
// Hook implementation
// ---------------------------------------------------------------------------

function isLegacy(
  options: UseGetParagraphOptions | UseGetParagraphLegacyOptions
): options is UseGetParagraphLegacyOptions {
  return Object.hasOwn(options, 'pageNumber');
}

/**
 * Fetch a single extracted paragraph for a given photo batch.
 *
 * - Disabled when `batchId` is undefined or the lookup value ≤ 0.
 * - 5-minute staleTime: paragraphs are stable once a batch is processed.
 * - Does not retry on error — let the caller surface the error state.
 *
 * @example
 * // By physical page (legacy API path)
 * const { data, isLoading } = useGetParagraph({
 *   batchId,
 *   paragraphRef: { type: 'page', value: currentPage },
 * });
 *
 * @example
 * // By narrative paragraph number (issue #747 PR-B endpoint)
 * const { data, isLoading } = useGetParagraph({
 *   batchId,
 *   paragraphRef: { type: 'paragraph', value: 42 },
 * });
 */
export function useGetParagraph(
  options: UseGetParagraphOptions | UseGetParagraphLegacyOptions
): UseQueryResult<Paragraph, Error> {
  // Normalize legacy → discriminated union. Emits one warn per session per
  // (batchId, pageNumber) pair to nudge callers without spamming the console.
  let batchId: string | undefined;
  let paragraphRef: ParagraphRef;
  let hint: string | undefined;
  let enabled: boolean;

  if (isLegacy(options)) {
    batchId = options.batchId;
    paragraphRef = { type: 'page', value: options.pageNumber };
    hint = options.hint;
    enabled = options.enabled ?? true;
    warnLegacyPageNumberOnce(options.batchId, options.pageNumber);
  } else {
    batchId = options.batchId;
    paragraphRef = options.paragraphRef;
    hint = options.hint;
    enabled = options.enabled ?? true;
  }

  const queryKey =
    paragraphRef.type === 'page'
      ? paragraphKeys.byPage(batchId ?? '', paragraphRef.value, hint)
      : paragraphKeys.byParagraph(batchId ?? '', paragraphRef.value, hint);

  return useQuery<Paragraph, Error>({
    queryKey,
    // `enabled: !!batchId && ...` gates the queryFn — the non-null assertions
    // on batchId are safe under that invariant.
    queryFn: () =>
      paragraphRef.type === 'page'
        ? // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- gated by enabled
          getParagraph(batchId!, paragraphRef.value, hint)
        : // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- gated by enabled
          getParagraphByParagraphNumber(batchId!, paragraphRef.value, hint),
    enabled: enabled && !!batchId && paragraphRef.value > 0,
    staleTime: 5 * 60_000,
    retry: false,
  });
}
