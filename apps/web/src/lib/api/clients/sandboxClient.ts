/**
 * Sandbox API Client (RAG Sandbox Dashboard)
 *
 * Client for admin RAG sandbox endpoints.
 * Covers: documents by game, delete PDF, chunk preview, pipeline metrics, apply config.
 */

import { z } from 'zod';

import type { HttpClient } from '../core/httpClient';

// --- Zod Schemas ---

export const PdfDocumentDtoSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  originalFileName: z.string(),
  contentType: z.string().nullable().optional(),
  fileSizeBytes: z.number(),
  processingState: z.string(),
  progressPercentage: z.number(),
  pageCount: z.number().nullable().optional(),
  chunkCount: z.number(),
  embeddingCount: z.number(),
  isActiveForRag: z.boolean(),
  errorMessage: z.string().nullable().optional(),
  uploadedAt: z.string(),
  processedAt: z.string().nullable().optional(),
  sharedGameId: z.string().uuid().nullable().optional(),
});

export type PdfDocumentDto = z.infer<typeof PdfDocumentDtoSchema>;

export const PdfDocumentListSchema = z.array(PdfDocumentDtoSchema);

export const PdfDeleteResultSchema = z.object({
  success: z.boolean(),
  message: z.string(),
  deletedVectorCount: z.number().optional(),
});

export type PdfDeleteResult = z.infer<typeof PdfDeleteResultSchema>;

export const ChunkPreviewDtoSchema = z.object({
  embeddingId: z.string().uuid(),
  textContent: z.string(),
  chunkIndex: z.number(),
  pageNumber: z.number(),
  model: z.string(),
  createdAt: z.string(),
});

export type ChunkPreviewDto = z.infer<typeof ChunkPreviewDtoSchema>;

export const ChunkPreviewListSchema = z.array(ChunkPreviewDtoSchema);

export const PipelineStepMetricDtoSchema = z.object({
  stepName: z.string(),
  stepOrder: z.number(),
  startedAt: z.string().nullable().optional(),
  completedAt: z.string().nullable().optional(),
  duration: z.string().nullable().optional(),
  status: z.string(),
});

export const PdfPipelineMetricsDtoSchema = z.object({
  pdfId: z.string().uuid(),
  fileName: z.string(),
  processingState: z.string(),
  progressPercentage: z.number(),
  uploadedAt: z.string(),
  processedAt: z.string().nullable().optional(),
  totalDuration: z.string().nullable().optional(),
  steps: z.array(PipelineStepMetricDtoSchema),
});

export type PipelineStepMetricDto = z.infer<typeof PipelineStepMetricDtoSchema>;
export type PdfPipelineMetricsDto = z.infer<typeof PdfPipelineMetricsDtoSchema>;

export const ApplySandboxConfigResultSchema = z.object({
  sessionKey: z.string(),
  expiresAt: z.string(),
});

export type ApplySandboxConfigResult = z.infer<typeof ApplySandboxConfigResultSchema>;

export interface ApplySandboxConfigRequest {
  gameId: string;
  strategy?: string | null;
  denseWeight?: number | null;
  topK?: number | null;
  rerankingEnabled?: boolean | null;
  temperature?: number | null;
  maxTokens?: number | null;
  model?: string | null;
  systemPromptOverride?: string | null;
  chunkingStrategy?: string | null;
  chunkSize?: number | null;
  chunkOverlap?: number | null;
}

export interface CreateSandboxClientParams {
  httpClient: HttpClient;
}

export type SandboxClient = ReturnType<typeof createSandboxClient>;

/**
 * Sandbox API client for admin RAG sandbox dashboard
 */
export function createSandboxClient({ httpClient }: CreateSandboxClientParams) {
  const BASE = '/api/v1/admin/sandbox';

  return {
    /**
     * Get all PDF documents for a shared game
     */
    async getDocumentsByGame(gameId: string): Promise<PdfDocumentDto[]> {
      const result = await httpClient.get(
        `${BASE}/shared-games/${encodeURIComponent(gameId)}/documents`,
        PdfDocumentListSchema
      );
      return result ?? [];
    },

    /**
     * Delete a PDF document (admin hard delete with vector cleanup).
     * httpClient.delete returns void; success = no error thrown.
     */
    async deletePdf(pdfId: string): Promise<void> {
      await httpClient.delete(`${BASE}/pdfs/${encodeURIComponent(pdfId)}`);
    },

    /**
     * Get chunk previews for a PDF document
     */
    async getChunksPreview(pdfId: string, limit = 20): Promise<ChunkPreviewDto[]> {
      const result = await httpClient.get(
        `${BASE}/pdfs/${encodeURIComponent(pdfId)}/chunks/preview?limit=${limit}`,
        ChunkPreviewListSchema
      );
      return result ?? [];
    },

    /**
     * Get pipeline processing metrics for a PDF document
     */
    async getPipelineMetrics(pdfId: string): Promise<PdfPipelineMetricsDto | null> {
      return httpClient.get(
        `${BASE}/pdfs/${encodeURIComponent(pdfId)}/pipeline-metrics`,
        PdfPipelineMetricsDtoSchema
      );
    },

    /**
     * Apply sandbox configuration to Redis with 24h TTL
     */
    async applyConfig(request: ApplySandboxConfigRequest): Promise<ApplySandboxConfigResult> {
      return httpClient.post(`${BASE}/apply-config`, request, ApplySandboxConfigResultSchema);
    },
  };
}
