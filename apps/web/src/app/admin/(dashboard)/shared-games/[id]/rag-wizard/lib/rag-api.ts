/**
 * RAG Wizard API Client
 *
 * API functions for the SharedGame RAG wizard.
 * Uses raw fetch with FormData for multipart/form-data file uploads
 * (the HttpClient only supports JSON body).
 *
 * Endpoints:
 * - POST /api/v1/admin/shared-games/{id}/rag — single upload + process
 * - POST /api/v1/admin/shared-games/batch-rag — batch upload + process
 */

import { getApiBase } from '@/lib/api';

// ── Types matching backend DTOs ────────────────────────────────────────

export interface AddRagResult {
  pdfDocumentId: string;
  sharedGameDocumentId: string;
  processingJobId: string | null;
  autoApproved: boolean;
  streamUrl: string;
}

export interface BatchItemResult {
  sharedGameId: string;
  fileName: string;
  result: AddRagResult | null;
  error: string | null;
}

export interface BatchRagResult {
  results: BatchItemResult[];
  successCount: number;
  failureCount: number;
}

// ── Document type enum matching backend SharedGameDocumentType ──────────

export type DocumentType = 'Rulebook' | 'Errata' | 'Homerule';

// ── API Functions ───────────────────────────────────────────────────────

/**
 * Upload a PDF and start RAG processing for a shared game.
 * Uses XHR for upload progress tracking.
 *
 * POST /api/v1/admin/shared-games/{sharedGameId}/rag (multipart/form-data)
 */
export async function addRagToSharedGame(
  sharedGameId: string,
  file: File,
  documentType: DocumentType,
  version: string,
  tags?: string[],
  onProgress?: (percent: number) => void
): Promise<AddRagResult> {
  const baseUrl = getApiBase();
  const url = `${baseUrl}/api/v1/admin/shared-games/${sharedGameId}/rag`;

  return new Promise<AddRagResult>((resolve, reject) => {
    const xhr = new XMLHttpRequest();

    if (onProgress) {
      xhr.upload.addEventListener('progress', e => {
        if (e.lengthComputable) {
          onProgress(Math.round((e.loaded / e.total) * 100));
        }
      });
    }

    xhr.addEventListener('load', () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          const response = JSON.parse(xhr.responseText) as AddRagResult;
          resolve(response);
        } catch {
          reject(new Error('Risposta server non valida'));
        }
      } else {
        try {
          const errorResponse = JSON.parse(xhr.responseText) as { error?: string; details?: Record<string, string> };
          const message = errorResponse.details
            ? Object.values(errorResponse.details).join(', ')
            : errorResponse.error ?? 'Upload fallito';
          reject(new Error(message));
        } catch {
          reject(new Error(`Upload fallito: ${xhr.statusText}`));
        }
      }
    });

    xhr.addEventListener('error', () => {
      reject(new Error("Errore di rete durante l'upload"));
    });

    xhr.addEventListener('timeout', () => {
      reject(new Error("Timeout durante l'upload"));
    });

    const formData = new FormData();
    formData.append('file', file);
    formData.append('documentType', documentType);
    formData.append('version', version);
    if (tags && tags.length > 0) {
      formData.append('tags', tags.join(','));
    }

    xhr.open('POST', url);
    xhr.withCredentials = true;
    xhr.send(formData);
  });
}

/**
 * Batch upload PDFs and start RAG processing for multiple shared games.
 *
 * POST /api/v1/admin/shared-games/batch-rag (multipart/form-data)
 */
export async function batchAddRag(
  items: Array<{
    sharedGameId: string;
    file: File;
    documentType: DocumentType;
    version: string;
  }>
): Promise<BatchRagResult> {
  const baseUrl = getApiBase();
  const url = `${baseUrl}/api/v1/admin/shared-games/batch-rag`;

  const formData = new FormData();
  for (const item of items) {
    formData.append('sharedGameIds', item.sharedGameId);
    formData.append('files', item.file);
    formData.append('documentTypes', item.documentType);
    formData.append('versions', item.version);
  }

  const response = await fetch(url, {
    method: 'POST',
    credentials: 'include',
    body: formData,
  });

  if (!response.ok) {
    const errorBody = await response.json().catch(() => null) as { error?: string } | null;
    throw new Error(errorBody?.error ?? `Batch upload fallito: ${response.statusText}`);
  }

  return (await response.json()) as BatchRagResult;
}
