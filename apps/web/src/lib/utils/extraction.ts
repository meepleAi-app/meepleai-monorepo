/**
 * Extraction Utility - Issue #4141
 *
 * Polling utility for PDF extraction status with configurable timeout and exponential backoff.
 */

export type ExtractionStatus = 'pending' | 'processing' | 'completed' | 'failed';

export interface PdfUploadResult {
  pdfDocumentId: string;
  status: ExtractionStatus;
  qualityScore: number;
  extractedTitle: string;
  errorMessage?: string;
}

const BASE_INTERVAL = 2000; // 2 seconds
const MAX_BACKOFF = 10000; // 10 seconds max interval

/**
 * Poll PDF extraction status with exponential backoff
 *
 * @param pdfId - PDF document ID to poll
 * @param maxRetries - Maximum number of polling attempts (default: 30)
 * @param interval - Base polling interval in milliseconds (default: 2000)
 * @returns Promise resolving to extraction result
 * @throws Error if max retries exceeded or extraction fails
 */
export async function pollExtractionStatus(
  pdfId: string,
  maxRetries: number = 30,
  interval: number = BASE_INTERVAL
): Promise<PdfUploadResult> {
  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
  let attempts = 0;

  while (attempts < maxRetries) {
    try {
      const result = await checkExtractionStatus(pdfId, API_BASE);

      // Terminal states
      if (result.status === 'completed') {
        return result;
      }

      if (result.status === 'failed') {
        throw new Error(
          `PDF extraction failed: ${result.errorMessage || 'Unknown error'}`
        );
      }

      // Continue polling for pending/processing states
      attempts++;

      // Linear backoff with cap (2s, 4s, 6s, 8s, 10s max, then 10s...)
      const currentInterval = Math.min(interval * (attempts + 1), MAX_BACKOFF);
      await delay(currentInterval);
    } catch (error) {
      // If this is the last attempt, throw the error
      if (attempts >= maxRetries - 1) {
        throw error;
      }

      // Otherwise, retry with linear backoff
      attempts++;
      const currentInterval = Math.min(interval * (attempts + 1), MAX_BACKOFF);
      await delay(currentInterval);
    }
  }

  // Max retries exceeded
  throw new Error(
    `PDF extraction timeout: exceeded ${maxRetries} attempts (${maxRetries * interval}ms)`
  );
}

/**
 * Check extraction status for a PDF document
 */
async function checkExtractionStatus(
  pdfId: string,
  apiBase: string
): Promise<PdfUploadResult> {
  const response = await fetch(
    `${apiBase}/api/v1/documents/${pdfId}/extraction-status`,
    {
      method: 'GET',
      credentials: 'include',
    }
  );

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(
      `HTTP ${response.status}: ${errorText || response.statusText}`
    );
  }

  const result = await response.json();
  return result as PdfUploadResult;
}

/**
 * Delay utility for polling intervals
 */
function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
