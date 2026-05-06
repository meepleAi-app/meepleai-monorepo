/**
 * Gamebook — useGetParagraph hook (Phase 3, Task 3.5a)
 *
 * TanStack Query v5 hook for fetching a single paragraph from a processed
 * photo batch. Paragraphs are stable (5-min staleTime) and typically only
 * re-fetched when the page number or batch changes.
 *
 * Endpoint: GET /api/v1/photo-batches/{batchId}/paragraphs/{pageNumber}?hint=...
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { getParagraph } from '../api';

import type { Paragraph } from '../schemas';

interface UseGetParagraphOptions {
  batchId: string | undefined;
  pageNumber: number;
  hint?: string;
  enabled?: boolean;
}

/**
 * Query key factory for paragraph lookups.
 * Exported so test files can prime or invalidate the cache.
 */
export const paragraphKeys = {
  byPage: (batchId: string, pageNumber: number, hint?: string) =>
    ['gamebook', 'paragraph', batchId, pageNumber, hint ?? ''] as const,
};

/**
 * Fetch a single extracted paragraph for a given page of a photo batch.
 *
 * - Disabled when `batchId` is undefined or `pageNumber` ≤ 0.
 * - 5-minute staleTime: paragraphs are stable once a batch is processed.
 * - Does not retry on error — let the caller surface the error state.
 *
 * @example
 * ```tsx
 * const { data: paragraph, isLoading, error } = useGetParagraph({
 *   batchId,
 *   pageNumber: currentPage,
 * });
 * ```
 */
export function useGetParagraph({
  batchId,
  pageNumber,
  hint,
  enabled = true,
}: UseGetParagraphOptions): UseQueryResult<Paragraph, Error> {
  return useQuery<Paragraph, Error>({
    queryKey: paragraphKeys.byPage(batchId ?? '', pageNumber, hint),
    queryFn: () => getParagraph(batchId!, pageNumber, hint),
    enabled: enabled && !!batchId && pageNumber > 0,
    staleTime: 5 * 60_000, // 5 min — paragraphs are stable once batch is processed
    retry: false,
  });
}
