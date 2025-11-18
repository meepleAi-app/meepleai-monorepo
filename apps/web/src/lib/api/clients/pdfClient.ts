/**
 * PDF Client (FE-IMP-005)
 *
 * Modular client for DocumentProcessing bounded context.
 * Covers: PDF processing progress, cancellation, download
 */

import { getApiBase, type HttpClient } from '../core/httpClient';
import {
  ProcessingProgressSchema,
  type ProcessingProgress,
} from '../schemas';

export interface CreatePdfClientParams {
  httpClient: HttpClient;
}

/**
 * PDF API client with Zod validation
 */
export function createPdfClient({ httpClient }: CreatePdfClientParams) {
  return {
    // ========== PDF Processing ==========

    /**
     * Get processing progress for a PDF document
     * @param pdfId PDF document ID (GUID format)
     */
    async getProcessingProgress(pdfId: string): Promise<ProcessingProgress | null> {
      return httpClient.get(
        `/api/v1/pdfs/${encodeURIComponent(pdfId)}/progress`,
        ProcessingProgressSchema
      );
    },

    /**
     * Cancel ongoing PDF processing
     * @param pdfId PDF document ID (GUID format)
     */
    async cancelProcessing(pdfId: string): Promise<void> {
      return httpClient.delete(`/api/v1/pdfs/${encodeURIComponent(pdfId)}/processing`);
    },

    /**
     * Get PDF file download URL (BGAI-074)
     * @param pdfId PDF document ID (GUID format)
     * @returns URL to download/view the PDF file
     */
    getPdfDownloadUrl(pdfId: string): string {
      const baseUrl = getApiBase();
      return `${baseUrl}/api/v1/pdfs/${encodeURIComponent(pdfId)}/download`;
    },
  };
}

export type PdfClient = ReturnType<typeof createPdfClient>;
