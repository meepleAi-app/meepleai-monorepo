/**
 * MSW handlers for gamebook paragraph endpoints (issue #1303).
 *
 * Covers backend routes shipped by #747 sequence:
 *   GET /api/v1/photo-batches/{batchId}/paragraphs/{pageNumber}
 *   GET /api/v1/photo-batches/{batchId}/paragraphs/by-paragraph/{paragraphNumber}
 *
 * Each handler records the request URL so request-capture assertions can
 * verify the FE hit the right route (AC-1, AC-2, AC-9).
 */

import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ---------------------------------------------------------------------------
// Request capture — exported so tests can assert which routes were hit
// ---------------------------------------------------------------------------

interface CapturedRequest {
  readonly path: string;
  readonly url: string;
  readonly searchParams: Record<string, string>;
}

const capturedRequests: CapturedRequest[] = [];

export function getCapturedGamebookRequests(): readonly CapturedRequest[] {
  return capturedRequests;
}

export function resetCapturedGamebookRequests(): void {
  capturedRequests.length = 0;
}

function captureRequest(rawUrl: string): void {
  const url = new URL(rawUrl);
  const searchParams: Record<string, string> = {};
  url.searchParams.forEach((value, key) => {
    searchParams[key] = value;
  });
  capturedRequests.push({
    path: url.pathname,
    url: rawUrl,
    searchParams,
  });
}

// ---------------------------------------------------------------------------
// Handlers
// ---------------------------------------------------------------------------

/**
 * Handler order matters: MSW matches top-to-bottom and `:pageNumber`
 * happily binds to the literal string `"by-paragraph"`, so the more
 * specific `/by-paragraph/:paragraphNumber` route MUST be declared first.
 */
export const gamebookHandlers = [
  /**
   * New (#747 PR-B): GET /paragraphs/by-paragraph/{paragraphNumber}
   *
   * Returned shape mirrors `ParagraphDto` with `paragraphNumber` echoed back
   * and `pageNumber` set to the physical page that contained the match.
   */
  http.get(
    `${API_BASE}/api/v1/photo-batches/:batchId/paragraphs/by-paragraph/:paragraphNumber`,
    ({ request, params }) => {
      captureRequest(request.url);
      const paragraphNumber = Number(params.paragraphNumber);
      return HttpResponse.json({
        // Mock: physical page 7 always carries the requested paragraph.
        pageNumber: 7,
        text: `OCR text for paragraph ${paragraphNumber}`,
        fallbackUsed: false,
        fallbackMethod: null,
        paragraphNumber,
      });
    }
  ),

  /**
   * Legacy: GET /paragraphs/{pageNumber}
   */
  http.get(
    `${API_BASE}/api/v1/photo-batches/:batchId/paragraphs/:pageNumber`,
    ({ request, params }) => {
      captureRequest(request.url);
      const pageNumber = Number(params.pageNumber);
      return HttpResponse.json({
        pageNumber,
        text: `OCR text for page ${pageNumber}`,
        fallbackUsed: false,
        fallbackMethod: null,
        paragraphNumber: null,
      });
    }
  ),
];
