/**
 * Issue #1674: Per-doc embeddings viewer API client.
 *
 * Wraps GET /api/v1/admin/kb/docs/{docId}/embeddings/meta.
 * Audit Level 1 emitted server-side via [AuditableAction] attribute.
 */

import { apiClient } from '@/lib/api/client';
import {
  DocumentEmbeddingsMetaDtoSchema,
  type DocumentEmbeddingsMetaDto,
} from '@/lib/api/schemas/admin-kb-embeddings.schemas';

export type { DocumentEmbeddingsMetaDto };

export async function getDocumentEmbeddingsMeta(
  docId: string,
  options?: { signal?: AbortSignal }
): Promise<DocumentEmbeddingsMetaDto | null> {
  return apiClient.get<DocumentEmbeddingsMetaDto>(
    `/api/v1/admin/kb/docs/${docId}/embeddings/meta`,
    DocumentEmbeddingsMetaDtoSchema,
    { signal: options?.signal }
  );
}

export function getDocumentChunksExportUrl(docId: string): string {
  return `/api/v1/admin/kb/docs/${docId}/chunks/export`;
}
