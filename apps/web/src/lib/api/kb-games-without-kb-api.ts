/**
 * Admin RAG onboarding: list of SharedGames with no active Knowledge Base.
 *
 * Matches backend endpoint: GET /api/v1/admin/kb/games/without-kb
 * See: docs/superpowers/plans/2026-04-11-rag-admin-onboarding.md (Task 4)
 */

import { useQuery } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';

export interface GameWithoutKbDto {
  gameId: string;
  title: string;
  publisher: string | null;
  imageUrl: string | null;
  playerCountLabel: string;
  pdfCount: number;
  failedPdfCount: number;
}

export interface GamesWithoutKbPagedResponse {
  items: GameWithoutKbDto[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface GamesWithoutKbParams {
  page?: number;
  pageSize?: number;
  search?: string;
}

const EMPTY_RESPONSE: GamesWithoutKbPagedResponse = {
  items: [],
  total: 0,
  page: 1,
  pageSize: 20,
  totalPages: 0,
};

export async function fetchGamesWithoutKb(
  params: GamesWithoutKbParams = {}
): Promise<GamesWithoutKbPagedResponse> {
  const qs = new URLSearchParams();
  if (params.page !== undefined) qs.set('page', params.page.toString());
  if (params.pageSize !== undefined) qs.set('pageSize', params.pageSize.toString());
  if (params.search) qs.set('search', params.search);

  const query = qs.toString();
  const url = `/api/v1/admin/kb/games/without-kb${query ? `?${query}` : ''}`;

  const result = await apiClient.get<GamesWithoutKbPagedResponse>(url);
  return result ?? EMPTY_RESPONSE;
}

export function useGamesWithoutKb(params: GamesWithoutKbParams = {}) {
  return useQuery({
    queryKey: ['admin', 'kb', 'games-without-kb', params],
    queryFn: () => fetchGamesWithoutKb(params),
    staleTime: 30_000,
  });
}
