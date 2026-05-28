/**
 * useUserKbDocs — Phase 2b (#1592) hook for the cross-game per-user KB documents
 * listing (BE-1 #1588). Calls `GET /api/v1/kb-docs` with default
 * `{ page:1, pageSize:20, sortBy:'recent', state:'ready' }` (K3 #1592).
 *
 * Adapter (K1.1): the BE DTO has `processedAt?` + `uploadedAt` but NOT a single
 * `updatedAt`. Server-side `sortBy=recent` orders by `ProcessedAt ?? UploadedAt`;
 * we mirror that on the FE so the `kbDocToHubItem` mapper signature stays
 * unchanged (AC2.b.4). Follow-up #1645 tracks exposing `updatedAt` explicit BE-side.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { KbDocsListResponse, UserKbDocDto } from '@/lib/api/schemas/kb-docs.schemas';
import type { KbDoc } from '@/lib/library/hybrid-hub.mappers';

/** Adapter: DTO → FE `KbDoc` (derives `updatedAt`). Pure, unit-testable. */
export function toKbDoc(dto: UserKbDocDto): KbDoc {
  return {
    id: dto.id,
    gameId: dto.gameId,
    gameName: dto.gameName,
    fileName: dto.fileName,
    processingState: dto.processingState,
    pageCount: dto.pageCount,
    processedAt: dto.processedAt,
    // K1.1: server-side sortBy=recent uses ProcessedAt ?? UploadedAt — mirror it.
    updatedAt: dto.processedAt ?? dto.uploadedAt,
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
