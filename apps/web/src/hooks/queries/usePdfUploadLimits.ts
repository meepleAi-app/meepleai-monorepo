/**
 * usePdfUploadLimits — #4449: the PDF upload limits of the current user, from
 * GET /api/v1/ingest/pdf/limits.
 *
 * Every upload zone reads the size limit from here. They used to carry their own constants (50,
 * 150, 500 MB) while the backend accepted 100: a file could pass the zone and fail the upload, or be
 * refused by the zone and accepted by the backend. While the limits are loading or unavailable,
 * `data` is undefined and a zone skips its own check — the backend still refuses with 400.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { PdfUploadLimits } from '@/lib/api/clients/pdfClient';

export const pdfUploadLimitsQueryKey = ['pdfUploadLimits'] as const;

export function usePdfUploadLimits() {
  return useQuery<PdfUploadLimits | null, Error>({
    queryKey: pdfUploadLimitsQueryKey,
    queryFn: () => api.pdf.getUploadLimits(),
    // The limit changes with a deploy or a plan change, not while a page is open.
    staleTime: 10 * 60 * 1000,
  });
}
