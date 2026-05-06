'use client';

/**
 * useKbDocument — fetch a single KB document record by ID (Issue #730 — G4)
 *
 * Calls GET /api/v1/kb-docs/{docId}.
 * Returns full KB document metadata including processing state and chunk counts.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { KbDocument } from '@/lib/api/schemas/kb-document.schemas';

export const kbDocumentKeys = {
  all: ['kb-document'] as const,
  byId: (docId: string) => ['kb-document', docId] as const,
} as const;

/**
 * Fetch a single KB document by its UUID.
 *
 * @param docId - KB document UUID, or undefined to skip the query
 * @param enabled - Additional enable flag (default: true)
 * @returns React Query result with KB document metadata
 *
 * @example
 * ```tsx
 * const { data: doc, isLoading } = useKbDocument(docId);
 * ```
 */
export function useKbDocument(docId: string | undefined, enabled: boolean = true) {
  return useQuery<KbDocument | null, Error>({
    queryKey: kbDocumentKeys.byId(docId ?? ''),
    queryFn: () => api.knowledgeBase.getKbDocument(docId!),
    enabled: enabled && !!docId,
    staleTime: 60_000, // 60s — metadata may change during ingestion
    gcTime: 5 * 60_000, // 5 minutes cache
  });
}
