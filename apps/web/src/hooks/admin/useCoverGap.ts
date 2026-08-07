/**
 * Admin cover-gap (#3590).
 *
 * Query hook: i giochi del catalogo senza alcuna cover, con la causa per cui la pipeline
 * cover-da-PDF non li copre. Wrappa `api.admin.getCoverGap()`.
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { CoverGapCause, CoverGapPage } from '@/lib/api/schemas/admin/admin-cover.schemas';

import { coverEditorKeys } from './coverEditorKeys';

export function useCoverGap(cause?: CoverGapCause): UseQueryResult<CoverGapPage | null, Error> {
  return useQuery({
    queryKey: coverEditorKeys.gap(cause),
    queryFn: () => api.admin.getCoverGap({ cause, pageSize: 100 }),
  });
}
