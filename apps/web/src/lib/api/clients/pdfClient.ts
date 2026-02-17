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
  PdfMetricsSchema,
  type PdfMetrics,
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
     * Get detailed processing metrics with per-state timing and ETA
     * Issue #4219: Duration metrics and ETA calculation
     * @param documentId Document ID (GUID format)
     * @returns Metrics with timing, progress, and ETA
     */
    async getMetrics(documentId: string): Promise<PdfMetrics | null> {
      return httpClient.get(
        `/api/v1/documents/${encodeURIComponent(documentId)}/metrics`,
        PdfMetricsSchema
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

    // ========== Admin PDF Management ==========

    /**
     * Get admin PDF list with filters and sorting
     * PDF Storage Management Hub
     */
    async getAdminPdfList(params?: {
      status?: string;
      state?: string;
      minSizeBytes?: number;
      maxSizeBytes?: number;
      uploadedAfter?: string;
      uploadedBefore?: string;
      gameId?: string;
      sortBy?: string;
      sortOrder?: string;
      pageSize?: number;
      page?: number;
    }): Promise<AdminPdfListResult> {
      const searchParams = new URLSearchParams();
      if (params?.status) searchParams.set('status', params.status);
      if (params?.state) searchParams.set('state', params.state);
      if (params?.minSizeBytes) searchParams.set('minSizeBytes', String(params.minSizeBytes));
      if (params?.maxSizeBytes) searchParams.set('maxSizeBytes', String(params.maxSizeBytes));
      if (params?.uploadedAfter) searchParams.set('uploadedAfter', params.uploadedAfter);
      if (params?.uploadedBefore) searchParams.set('uploadedBefore', params.uploadedBefore);
      if (params?.gameId) searchParams.set('gameId', params.gameId);
      if (params?.sortBy) searchParams.set('sortBy', params.sortBy);
      if (params?.sortOrder) searchParams.set('sortOrder', params.sortOrder);
      if (params?.pageSize) searchParams.set('pageSize', String(params.pageSize));
      if (params?.page) searchParams.set('page', String(params.page));

      const qs = searchParams.toString();
      const url = `/api/v1/admin/pdfs${qs ? `?${qs}` : ''}`;
      const result = await httpClient.get<AdminPdfListResult>(url);
      return result ?? { items: [], total: 0, page: 1, pageSize: 50 };
    },

    /**
     * Delete a PDF document (admin)
     */
    async adminDeletePdf(pdfId: string): Promise<void> {
      return httpClient.delete(`/api/v1/pdf/${encodeURIComponent(pdfId)}`);
    },

    /**
     * Bulk delete PDF documents (admin)
     */
    async bulkDeletePdfs(pdfIds: string[]): Promise<BulkDeleteResult> {
      const result = await httpClient.post<BulkDeleteResult>(
        '/api/v1/admin/pdfs/bulk/delete',
        { pdfIds }
      );
      return result ?? { totalRequested: pdfIds.length, successCount: 0, failedCount: pdfIds.length, items: [] };
    },

    /**
     * Get storage health metrics (admin)
     */
    async getStorageHealth(): Promise<PdfStorageHealth> {
      const result = await httpClient.get<PdfStorageHealth>('/api/v1/admin/pdfs/storage/health');
      return result ?? {
        postgres: { totalDocuments: 0, totalChunks: 0, estimatedChunksSizeMB: 0 },
        qdrant: { vectorCount: 0, memoryBytes: 0, memoryFormatted: '0 B', isAvailable: false },
        fileStorage: { totalFiles: 0, totalSizeBytes: 0, totalSizeFormatted: '0 B', sizeByState: {} },
        overallHealth: 'critical',
        measuredAt: new Date().toISOString(),
      };
    },

    /**
     * Get PDF status distribution (admin)
     */
    async getStatusDistribution(): Promise<PdfStatusDistribution> {
      const result = await httpClient.get<PdfStatusDistribution>('/api/v1/admin/pdfs/analytics/distribution');
      return result ?? { countByState: {}, totalDocuments: 0, topBySize: [] };
    },

    /**
     * Reindex a specific document (admin)
     */
    async reindexDocument(pdfId: string): Promise<void> {
      return httpClient.post(`/api/v1/admin/pdfs/${encodeURIComponent(pdfId)}/reindex`, {});
    },

    /**
     * Purge stale documents stuck in processing (admin)
     */
    async purgeStaleDocuments(): Promise<{ purgedCount: number }> {
      const result = await httpClient.post<{ purgedCount: number }>(
        '/api/v1/admin/pdfs/maintenance/purge-stale',
        {}
      );
      return result ?? { purgedCount: 0 };
    },

    /**
     * Cleanup orphaned chunks and vectors (admin)
     */
    async cleanupOrphans(): Promise<{ orphanedChunks: number; orphanedVectors: number }> {
      const result = await httpClient.post<{ orphanedChunks: number; orphanedVectors: number }>(
        '/api/v1/admin/pdfs/maintenance/cleanup-orphans',
        {}
      );
      return result ?? { orphanedChunks: 0, orphanedVectors: 0 };
    },
  };
}

// ========== Admin PDF Types ==========

export interface AdminPdfListItem {
  id: string;
  fileName: string;
  gameTitle: string | null;
  gameId: string | null;
  processingStatus: string;
  processingState: string;
  progressPercentage: number;
  fileSizeBytes: number;
  pageCount: number | null;
  chunkCount: number;
  processingError: string | null;
  errorCategory: string | null;
  retryCount: number;
  uploadedAt: string;
  processedAt: string | null;
}

export interface AdminPdfListResult {
  items: AdminPdfListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface BulkDeleteResult {
  totalRequested: number;
  successCount: number;
  failedCount: number;
  items: Array<{ pdfId: string; success: boolean; error?: string }>;
}

export interface PdfStorageHealth {
  postgres: {
    totalDocuments: number;
    totalChunks: number;
    estimatedChunksSizeMB: number;
  };
  qdrant: {
    vectorCount: number;
    memoryBytes: number;
    memoryFormatted: string;
    isAvailable: boolean;
  };
  fileStorage: {
    totalFiles: number;
    totalSizeBytes: number;
    totalSizeFormatted: string;
    sizeByState: Record<string, number>;
  };
  overallHealth: 'healthy' | 'warning' | 'critical';
  measuredAt: string;
}

export interface PdfStatusDistribution {
  countByState: Record<string, number>;
  totalDocuments: number;
  topBySize: Array<{ id: string; fileName: string; fileSizeBytes: number }>;
}

export type PdfClient = ReturnType<typeof createPdfClient>;
