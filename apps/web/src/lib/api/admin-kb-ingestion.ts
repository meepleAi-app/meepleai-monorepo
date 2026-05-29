/**
 * Admin KB Ingestion Log API Client (Issue #1650 Task 5)
 *
 * Fetches the processing pipeline status (job + steps + logs) for a PDF document.
 * Returns null when no job exists (legacy PDFs predating the ingestion pipeline).
 */

import { apiClient } from '@/lib/api/client';
import {
  IngestionLogResponseSchema,
  type IngestionLog,
} from '@/lib/api/schemas/ingestion-log.schemas';

/**
 * GET /api/v1/admin/kb/docs/{docId}/ingestion-log
 * Returns the latest ProcessingJob+Steps+Logs for the given PDF document, or
 * null when no job exists (legacy PDFs predating the pipeline).
 *
 * @param docId The PDF document ID (UUID)
 * @returns IngestionLog when a job exists, null for legacy documents
 */
export async function fetchKbDocIngestionLog(docId: string): Promise<IngestionLog | null> {
  return apiClient.get<IngestionLog | null>(
    `/api/v1/admin/kb/docs/${docId}/ingestion-log`,
    IngestionLogResponseSchema
  );
}

/**
 * POST /api/v1/admin/queue/jobs/{jobId}/retry
 * Re-enqueues a failed processing job. Backend handler is RetryJobCommand.
 * Issue #1650.
 */
export async function retryIngestionJob(jobId: string): Promise<void> {
  await apiClient.post<void>(`/api/v1/admin/queue/${jobId}/retry`, {});
}
