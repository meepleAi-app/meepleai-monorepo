/**
 * Shared mock factories for upload workflow tests
 *
 * This module provides reusable mock factories to reduce duplication
 * across upload test files (game-selection, pdf-upload, review-edit, edge-cases)
 *
 * REFACTORED: Now uses MockApiRouter for cleaner, more maintainable mocks
 * REFACTORED: Auth and Game mocks now use common-fixtures for consistency
 */

import { MockApiRouter, createJsonResponse, createErrorResponse } from '../utils/mock-api-router';
import {
  createMockAuthResponse,
  createMockGame,
  createMockPdfDocument,
  createMockRuleSpec,
  createMockRuleAtom,
  type MockUser,
  type MockPdfDocument,
  type MockRuleSpec,
} from './common-fixtures';

// Re-export response helpers
export { createJsonResponse, createErrorResponse };

export interface UploadMocksConfig {
  auth?: ReturnType<typeof createMockAuthResponse> | null;
  games?: ReturnType<typeof createMockGame>[];
  pdfs?: { pdfs: MockPdfDocument[] };
  uploadResponse?: { documentId: string; fileName?: string };
  pdfStatusSequence?: Array<{ processingStatus: string; processingError?: string | null }>;
  ruleSpec?: MockRuleSpec | null;
  createGameResponse?: ReturnType<typeof createMockGame>;
  createGameError?: { status: number; error: string };
  uploadError?: { status: number; error: string; correlationId?: string };
  ruleSpecError?: { status: number; error: unknown };
  publishRuleSpecResponse?: MockRuleSpec;
  publishRuleSpecError?: { status: number; error: string };
  retryParseResponse?: { success: boolean };
  retryParseError?: { status: number; error: string };
  onUploadCapture?: (formData: FormData) => void; // Callback to capture upload FormData
  pollingDelayMs?: number; // Delay in ms for polling responses to simulate network latency
}

/**
 * Sets up a comprehensive fetch mock router for upload workflow tests
 *
 * REFACTORED: Now uses MockApiRouter for cleaner route management
 *
 * @param config Configuration object with mocks for different endpoints
 * @returns Mock fetch implementation function
 */
export function setupUploadMocks(config: UploadMocksConfig = {}) {
  const {
    auth = createMockAuthResponse(),
    games = [createMockGame()],
    pdfs = { pdfs: [] },
    uploadResponse = { documentId: 'pdf-123', fileName: 'test.pdf' },
    pdfStatusSequence = [],
    ruleSpec = createMockRuleSpec(),
    createGameResponse,
    createGameError,
    uploadError,
    ruleSpecError,
    publishRuleSpecResponse,
    publishRuleSpecError,
    retryParseResponse,
    retryParseError,
    onUploadCapture, // Callback to capture FormData
    pollingDelayMs = 0, // NEW: Default 0ms (instant), tests can override for realistic timing
  } = config;

  const router = new MockApiRouter();

  // Auth endpoint
  if (auth === null) {
    router.get('/api/v1/auth/me', () => createErrorResponse(401, { error: 'Unauthorized' }));
  } else {
    router.get('/api/v1/auth/me', () => createJsonResponse(auth));
  }

  // Games endpoints
  router.get('/api/v1/games', () => createJsonResponse(games));

  if (createGameError) {
    router.post('/api/v1/games', () =>
      createErrorResponse(createGameError.status, { error: createGameError.error })
    );
  } else {
    router.post('/api/v1/games', () =>
      createJsonResponse(
        createGameResponse ?? createMockGame({ id: 'game-new', title: 'New Game' }),
        201
      )
    );
  }

  // PDFs list for a game (using route parameters)
  router.get('/api/v1/games/:gameId/pdfs', () => createJsonResponse(pdfs));

  // Upload PDF
  if (uploadError) {
    router.post('/api/v1/ingest/pdf', ({ init }) => {
      // Capture FormData if callback provided
      if (onUploadCapture && init?.body instanceof FormData) {
        onUploadCapture(init.body);
      }
      return createErrorResponse(
        uploadError.status,
        { error: uploadError.error },
        undefined,
        uploadError.correlationId
      );
    });
  } else {
    router.post('/api/v1/ingest/pdf', ({ init }) => {
      // Capture FormData if callback provided
      if (onUploadCapture && init?.body instanceof FormData) {
        onUploadCapture(init.body);
      }
      return createJsonResponse(uploadResponse, 201);
    });
  }

  // PDF status polling (stateful with closure)
  let statusIndex = 0;
  router.get('/api/v1/pdfs/:documentId/text', async ({ params }) => {
    // Simulate network latency if configured
    if (pollingDelayMs > 0) {
      await new Promise(resolve => setTimeout(resolve, pollingDelayMs));
    }

    if (pdfStatusSequence.length > 0) {
      const nextStatus =
        pdfStatusSequence[statusIndex] ?? pdfStatusSequence[pdfStatusSequence.length - 1];
      if (statusIndex < pdfStatusSequence.length - 1) {
        statusIndex++;
      }
      return createJsonResponse({
        id: params.documentId,
        fileName: uploadResponse.fileName ?? 'test.pdf',
        processingStatus: nextStatus.processingStatus,
        processingError: nextStatus.processingError ?? null,
      });
    }
    // Default: return completed status
    return createJsonResponse({
      id: params.documentId,
      fileName: uploadResponse.fileName ?? 'test.pdf',
      processingStatus: 'completed',
      processingError: null,
    });
  });

  // Get RuleSpec
  if (ruleSpecError) {
    router.get('/api/v1/games/:gameId/rulespec', () =>
      createErrorResponse(ruleSpecError.status, ruleSpecError.error)
    );
  } else {
    router.get('/api/v1/games/:gameId/rulespec', () => createJsonResponse(ruleSpec));
  }

  // Publish RuleSpec
  if (publishRuleSpecError) {
    router.put('/api/v1/games/:gameId/rulespec', () =>
      createErrorResponse(publishRuleSpecError.status, { error: publishRuleSpecError.error })
    );
  } else {
    router.put('/api/v1/games/:gameId/rulespec', () =>
      createJsonResponse(publishRuleSpecResponse ?? ruleSpec)
    );
  }

  // Retry parse
  if (retryParseError) {
    router.post('/api/v1/ingest/pdf/:documentId/retry', () =>
      createErrorResponse(retryParseError.status, { error: retryParseError.error })
    );
  } else {
    router.post('/api/v1/ingest/pdf/:documentId/retry', () =>
      createJsonResponse(retryParseResponse ?? { success: true })
    );
  }

  return vi.fn(router.toMockImplementation()) as MockedFunction<typeof fetch>;
}
