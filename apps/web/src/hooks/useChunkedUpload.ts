import { useState, useCallback, useRef } from 'react';

/**
 * Chunk size in bytes (10 MB - must match backend)
 */
const CHUNK_SIZE = 10 * 1024 * 1024;

/**
 * Maximum retries for each chunk upload
 */
const MAX_CHUNK_RETRIES = 3;

/**
 * Base delay for exponential backoff (ms)
 */
const RETRY_BASE_DELAY = 1000;

/**
 * Threshold for using chunked upload (30 MB)
 */
export const CHUNKED_UPLOAD_THRESHOLD = 30 * 1024 * 1024;

export interface ChunkedUploadProgress {
  status: 'idle' | 'initializing' | 'uploading' | 'completing' | 'completed' | 'error';
  currentChunk: number;
  totalChunks: number;
  progressPercentage: number;
  uploadedBytes: number;
  totalBytes: number;
  error?: string;
}

export interface ChunkedUploadResult {
  success: boolean;
  documentId?: string;
  fileName?: string;
  error?: string;
}

interface InitSessionResponse {
  sessionId: string;
  totalChunks: number;
  chunkSizeBytes: number;
  expiresAt: string;
}

interface UploadChunkResponse {
  success: boolean;
  receivedChunks: number;
  totalChunks: number;
  progressPercentage: number;
  isComplete: boolean;
  error?: string;
}

interface CompleteUploadResponse {
  success: boolean;
  documentId?: string;
  fileName?: string;
  error?: string;
  missingChunks?: number[];
}

/**
 * Hook for uploading large files using chunked upload API.
 * Automatically splits files into 10 MB chunks and uploads them sequentially.
 */
export function useChunkedUpload(apiBase: string) {
  const [progress, setProgress] = useState<ChunkedUploadProgress>({
    status: 'idle',
    currentChunk: 0,
    totalChunks: 0,
    progressPercentage: 0,
    uploadedBytes: 0,
    totalBytes: 0,
  });

  const abortControllerRef = useRef<AbortController | null>(null);

  /**
   * Check if a file should use chunked upload
   */
  const shouldUseChunkedUpload = useCallback((file: File): boolean => {
    return file.size > CHUNKED_UPLOAD_THRESHOLD;
  }, []);

  /**
   * Reset progress state
   */
  const reset = useCallback(() => {
    setProgress({
      status: 'idle',
      currentChunk: 0,
      totalChunks: 0,
      progressPercentage: 0,
      uploadedBytes: 0,
      totalBytes: 0,
    });
  }, []);

  /**
   * Cancel ongoing upload
   */
  const cancel = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      abortControllerRef.current = null;
    }
    setProgress(prev => ({
      ...prev,
      status: 'error',
      error: 'Upload cancelled',
    }));
  }, []);

  /**
   * Upload a file using chunked upload
   */
  const uploadChunked = useCallback(
    async (file: File, gameId: string): Promise<ChunkedUploadResult> => {
      abortControllerRef.current = new AbortController();
      const { signal } = abortControllerRef.current;

      try {
        // Step 1: Initialize session
        setProgress({
          status: 'initializing',
          currentChunk: 0,
          totalChunks: 0,
          progressPercentage: 0,
          uploadedBytes: 0,
          totalBytes: file.size,
        });

        const initResponse = await fetch(`${apiBase}/api/v1/ingest/pdf/chunked/init`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          signal,
          body: JSON.stringify({
            gameId,
            fileName: file.name,
            totalFileSize: file.size,
          }),
        });

        if (!initResponse.ok) {
          const error = await initResponse
            .json()
            .catch(() => ({ error: 'Failed to initialize upload' }));
          throw new Error(error.error || 'Failed to initialize upload session');
        }

        const initData: InitSessionResponse = await initResponse.json();
        const { sessionId, totalChunks } = initData;

        setProgress(prev => ({
          ...prev,
          status: 'uploading',
          totalChunks,
        }));

        // Step 2: Upload chunks with retry logic
        for (let chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++) {
          if (signal.aborted) {
            throw new Error('Upload cancelled');
          }

          const start = chunkIndex * CHUNK_SIZE;
          const end = Math.min(start + CHUNK_SIZE, file.size);
          const chunk = file.slice(start, end);

          let lastError: Error | null = null;
          let chunkData: UploadChunkResponse | null = null;

          // Retry loop for each chunk
          for (let attempt = 0; attempt < MAX_CHUNK_RETRIES; attempt++) {
            if (signal.aborted) {
              throw new Error('Upload cancelled');
            }

            try {
              const formData = new FormData();
              formData.append('sessionId', sessionId);
              formData.append('chunkIndex', chunkIndex.toString());
              formData.append('chunk', chunk, `chunk_${chunkIndex}`);

              const chunkResponse = await fetch(`${apiBase}/api/v1/ingest/pdf/chunked/chunk`, {
                method: 'POST',
                credentials: 'include',
                signal,
                body: formData,
              });

              if (!chunkResponse.ok) {
                const error = await chunkResponse
                  .json()
                  .catch(() => ({ error: 'Failed to upload chunk' }));
                throw new Error(error.error || `Failed to upload chunk ${chunkIndex + 1}`);
              }

              const responseData = (await chunkResponse.json()) as UploadChunkResponse;

              if (!responseData.success) {
                throw new Error(responseData.error || `Chunk ${chunkIndex + 1} upload failed`);
              }

              chunkData = responseData;

              // Success - break retry loop
              lastError = null;
              break;
            } catch (error) {
              lastError = error instanceof Error ? error : new Error('Unknown error');

              // Don't retry if cancelled or if it's the last attempt
              if (signal.aborted || attempt === MAX_CHUNK_RETRIES - 1) {
                break;
              }

              // Exponential backoff before retry
              const delay = RETRY_BASE_DELAY * Math.pow(2, attempt);
              await new Promise(resolve => setTimeout(resolve, delay));
            }
          }

          // If all retries failed, throw the last error
          if (lastError || !chunkData) {
            throw (
              lastError ||
              new Error(`Chunk ${chunkIndex + 1} upload failed after ${MAX_CHUNK_RETRIES} attempts`)
            );
          }

          setProgress({
            status: 'uploading',
            currentChunk: chunkIndex + 1,
            totalChunks,
            progressPercentage: chunkData.progressPercentage,
            uploadedBytes: end,
            totalBytes: file.size,
          });
        }

        // Step 3: Complete upload
        setProgress(prev => ({
          ...prev,
          status: 'completing',
        }));

        const completeResponse = await fetch(`${apiBase}/api/v1/ingest/pdf/chunked/complete`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          signal,
          body: JSON.stringify({ sessionId }),
        });

        if (!completeResponse.ok) {
          const error = await completeResponse
            .json()
            .catch(() => ({ error: 'Failed to complete upload' }));
          throw new Error(error.error || 'Failed to complete upload');
        }

        const completeData: CompleteUploadResponse = await completeResponse.json();

        if (!completeData.success) {
          if (completeData.missingChunks && completeData.missingChunks.length > 0) {
            throw new Error(
              `Upload incomplete. Missing chunks: ${completeData.missingChunks.join(', ')}`
            );
          }
          throw new Error(completeData.error || 'Failed to complete upload');
        }

        setProgress(prev => ({
          ...prev,
          status: 'completed',
          progressPercentage: 100,
        }));

        return {
          success: true,
          documentId: completeData.documentId,
          fileName: completeData.fileName,
        };
      } catch (error) {
        const errorMessage = error instanceof Error ? error.message : 'Upload failed';

        setProgress(prev => ({
          ...prev,
          status: 'error',
          error: errorMessage,
        }));

        return {
          success: false,
          error: errorMessage,
        };
      } finally {
        abortControllerRef.current = null;
      }
    },
    [apiBase]
  );

  return {
    progress,
    uploadChunked,
    shouldUseChunkedUpload,
    cancel,
    reset,
    isUploading:
      progress.status === 'initializing' ||
      progress.status === 'uploading' ||
      progress.status === 'completing',
  };
}
