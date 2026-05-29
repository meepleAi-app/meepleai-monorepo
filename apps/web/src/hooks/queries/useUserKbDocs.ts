/**
 * useUserKbDocs — Phase 2b (#1592) hook for the cross-game per-user KB documents
 * listing (BE-1 #1588). Calls `GET /api/v1/kb-docs` with default
 * `{ page:1, pageSize:20, sortBy:'recent', state:'ready' }` (K3 #1592).
 *
 * Adapter (K1.1, #1645): the BE DTO now explicitly exposes `updatedAt`,
 * which is computed server-side as `ProcessedAt ?? UploadedAt` (the canonical
 * sort key). This eliminates the FE adapter derivation.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { KbDocsListResponse, UserKbDocDto } from '@/lib/api/schemas/kb-docs.schemas';
import type { KbDoc } from '@/lib/library/hybrid-hub.mappers';

/** Adapter: DTO → FE `KbDoc`. Pure, unit-testable. */
export function toKbDoc(dto: UserKbDocDto): KbDoc {
  return {
    id: dto.id,
    gameId: dto.gameId,
    gameName: dto.gameName,
    fileName: dto.fileName,
    processingState: dto.processingState,
    pageCount: dto.pageCount,
    processedAt: dto.processedAt,
    uploadedAt: dto.uploadedAt,
    updatedAt: dto.updatedAt,
  };
}

export interface UseUserKbDocsResult {
  /** Adapted items (DTO → KbDoc) — what the mapper consumes. */
  items: KbDoc[];
  total: number;
  page: number;
  pageSize: number;
}

export function useUserKbDocs(): UseQueryResult<UseUserKbDocsResult> {
  return useQuery({
    queryKey: ['kb-docs', 'user', { page: 1, pageSize: 20, sortBy: 'recent', state: 'ready' }],
    queryFn: async () => {
      const response: KbDocsListResponse = await api.kbDocs.listUserKbDocs({
        page: 1,
        pageSize: 20,
        sortBy: 'recent',
        state: 'ready',
      });
      return {
        items: response.items.map(toKbDoc),
        total: response.total,
        page: response.page,
        pageSize: response.pageSize,
      };
    },
    staleTime: 5 * 60 * 1000, // 5 min, aligns with useAgents
  });
}
