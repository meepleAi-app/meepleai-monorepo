/**
 * PDF Client (FE-IMP-005)
 *
 * Modular client for DocumentProcessing bounded context.
 * Covers: PDF processing progress, cancellation, download
 */

import { getApiBase, type HttpClient } from '../core/httpClient';
import { ProcessingProgressSchema, type ProcessingProgress } from '../schemas';

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

    /**
     * Upload a PDF file for a game
     * POST /api/v1/ingest/pdf (multipart/form-data)
     * @param gameId Game ID to associate the PDF with
     * @param file PDF file to upload
     * @param onProgress Optional progress callback (0-100) via XMLHttpRequest
     * @returns Upload result with documentId and fileName
     */
    async uploadPdf(
      gameId: string,
      file: File,
      onProgress?: (percent: number) => void
    ): Promise<{ documentId: string; fileName: string }> {
      const baseUrl = getApiBase();
      const url = `${baseUrl}/api/v1/ingest/pdf`;

      return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open('POST', url, true);
        xhr.withCredentials = true;

        if (onProgress) {
          xhr.upload.onprogress = (event) => {
            if (event.lengthComputable) {
              const percent = Math.round((event.loaded / event.total) * 100);
              onProgress(percent);
            }
          };
        }

        xhr.onload = () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            try {
              const data = JSON.parse(xhr.responseText);
              resolve({ documentId: data.documentId, fileName: data.fileName });
            } catch {
              resolve({ documentId: '', fileName: file.name });
            }
          } else {
            let message = `Upload failed (${xhr.status})`;
            try {
              const err = JSON.parse(xhr.responseText);
              message = err.detail || err.title || err.message || message;
            } catch { /* use default message */ }
            reject(new Error(message));
          }
        };

        xhr.onerror = () => reject(new Error('Errore di rete durante il caricamento'));
        xhr.ontimeout = () => reject(new Error('Timeout durante il caricamento'));

        const formData = new FormData();
        formData.append('file', file);
        formData.append('gameId', gameId);
        xhr.send(formData);
      });
    },

    /**
     * Set PDF visibility in public library (Admin Wizard)
     * PATCH /api/v1/pdfs/{pdfId}/visibility
     * @param pdfId PDF document ID (GUID format)
     * @param isPublic Whether the PDF should be publicly visible
     * @returns Result with success status and message
     */
    async setVisibility(
      pdfId: string,
      isPublic: boolean
    ): Promise<{ success: boolean; message: string; pdfId?: string }> {
      const result = await httpClient.patch<{ success: boolean; message: string; pdfId?: string }>(
        `/api/v1/pdfs/${encodeURIComponent(pdfId)}/visibility`,
        { isPublic }
      );
      return result ?? { success: false, message: 'No response from server' };
    },
  };
}

export type PdfClient = ReturnType<typeof createPdfClient>;
