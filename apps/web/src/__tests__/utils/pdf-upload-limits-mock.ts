/**
 * #4449: the PDF size limit of the upload zones comes from the backend (`usePdfUploadLimits`).
 * Upload zone tests replace the hook with this module and set the limit the backend would answer:
 *
 *   vi.mock('@/hooks/queries/usePdfUploadLimits', () => import('@/__tests__/utils/pdf-upload-limits-mock'));
 *   import { resetPdfUploadLimit, setPdfUploadLimit } from '@/__tests__/utils/pdf-upload-limits-mock';
 *
 *   beforeEach(() => resetPdfUploadLimit());   // 100 MB, the single limit
 *   setPdfUploadLimit(20 * 1024 * 1024);       // a plan's cap — not a value any component carries
 *   setPdfUploadLimit(undefined);              // limits not loaded: the zone skips its own check
 */

import type { PdfUploadLimits } from '@/lib/api/clients/pdfClient';

export const SINGLE_PDF_LIMIT_BYTES = 100 * 1024 * 1024;

let current: PdfUploadLimits | undefined;

export function setPdfUploadLimit(maxFileSizeBytes: number | undefined): void {
  current =
    maxFileSizeBytes === undefined
      ? undefined
      : { maxFileSizeBytes, maxConcurrentChunkedUploads: 3 };
}

export function resetPdfUploadLimit(): void {
  setPdfUploadLimit(SINGLE_PDF_LIMIT_BYTES);
}

resetPdfUploadLimit();

export const pdfUploadLimitsQueryKey = ['pdfUploadLimits'] as const;

export function usePdfUploadLimits(): { data: PdfUploadLimits | undefined } {
  return { data: current };
}
