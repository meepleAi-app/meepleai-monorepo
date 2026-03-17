import { logger } from '@/lib/logger';

/**
 * Upload Utility - Issue #4141
 *
 * Chunked upload for large PDF files with progress tracking and retry logic.
 */

const CHUNK_SIZE = 5 * 1024 * 1024; // 5MB
const MAX_RETRIES = 3;
const RETRY_DELAY = 1000; // 1 second

/**
 * Upload large files in chunks with progress callback
 *
 * @param file - File to upload
 * @param sessionId - Session identifier for chunked upload
 * @param onProgress - Callback for progress updates (0-100)
 * @returns Promise resolving to the uploaded document ID (Guid)
 * @throws Error if upload fails after max retries
 */
export async function uploadChunks(
  file: File,
  sessionId: string,
  onProgress: (percent: number) => void
): Promise<string> {
  const totalChunks = Math.ceil(file.size / CHUNK_SIZE);
  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  let uploadedChunks = 0;

  for (let chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++) {
    const start = chunkIndex * CHUNK_SIZE;
    const end = Math.min(start + CHUNK_SIZE, file.size);
    const chunk = file.slice(start, end);

    let success = false;
    let lastError: Error | null = null;

    // Retry logic for each chunk
    for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
      try {
        await uploadChunk(chunk, chunkIndex, totalChunks, sessionId, API_BASE);
        success = true;
        break;
      } catch (error) {
        lastError = error as Error;
        logger.error(
          `Chunk ${chunkIndex + 1}/${totalChunks} failed (attempt ${attempt}/${MAX_RETRIES}):`,
          error
        );

        if (attempt < MAX_RETRIES) {
          // Exponential backoff
          await delay(RETRY_DELAY * attempt);
        }
      }
    }

    if (!success) {
      throw new Error(
        `Failed to upload chunk ${chunkIndex + 1}/${totalChunks} after ${MAX_RETRIES} attempts: ${lastError?.message}`
      );
    }

    uploadedChunks++;
    const progress = Math.round((uploadedChunks / totalChunks) * 100);
    onProgress(progress);
  }

  // Finalize upload and get document ID
  const documentId = await finalizeUpload(sessionId, file.name, API_BASE);
  return documentId;
}

/**
 * Upload a single chunk
 */
async function uploadChunk(
  chunk: Blob,
  chunkIndex: number,
  totalChunks: number,
  sessionId: string,
  apiBase: string
): Promise<void> {
  const formData = new FormData();
  formData.append('chunk', chunk);
  formData.append('chunkIndex', chunkIndex.toString());
  formData.append('totalChunks', totalChunks.toString());
  formData.append('sessionId', sessionId);

  const response = await fetch(`${apiBase}/api/v1/documents/upload-chunk`, {
    method: 'POST',
    body: formData,
    credentials: 'include',
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(`HTTP ${response.status}: ${errorText || response.statusText}`);
  }
}

/**
 * Finalize chunked upload and get document ID
 */
async function finalizeUpload(
  sessionId: string,
  fileName: string,
  apiBase: string
): Promise<string> {
  const response = await fetch(`${apiBase}/api/v1/documents/finalize-upload`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    credentials: 'include',
    body: JSON.stringify({ sessionId, fileName }),
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(`Failed to finalize upload: HTTP ${response.status} - ${errorText}`);
  }

  const result = await response.json();
  return result.documentId;
}

/**
 * Delay utility for retry backoff
 */
function delay(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}
