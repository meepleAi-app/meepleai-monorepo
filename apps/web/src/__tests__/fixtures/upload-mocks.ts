/**
 * Shared mock factories for upload workflow tests
 *
 * This module provides reusable mock factories to reduce duplication
 * across upload test files (game-selection, pdf-upload, review-edit, edge-cases)
 *
 * REFACTORED: Now uses MockApiRouter for cleaner, more maintainable mocks
 */

import { MockApiRouter, createJsonResponse, createErrorResponse } from '../utils/mock-api-router';

export interface AuthMockOptions {
  userId?: string;
  email?: string;
  role?: 'Admin' | 'Editor' | 'Viewer';
  displayName?: string;
  expiresAt?: string;
}

export interface GameMockOptions {
  id?: string;
  name?: string;
  createdAt?: string;
}

export interface PdfMockOptions {
  id?: string;
  fileName?: string;
  fileSizeBytes?: number;
  uploadedAt?: string;
  uploadedByUserId?: string;
  processingStatus?: 'pending' | 'processing' | 'completed' | 'failed';
  processingError?: string | null;
  status?: string;
  logUrl?: string | null;
}

export interface RuleSpecMockOptions {
  gameId?: string;
  version?: string;
  createdAt?: string;
  rules?: Array<{
    id: string;
    text: string;
    section?: string | null;
    page?: string | null;
    line?: string | null;
  }>;
}

// Re-export response helpers for backward compatibility
export { createJsonResponse, createErrorResponse };

export interface UploadMocksConfig {
  auth?: ReturnType<typeof createAuthMock> | null;
  games?: ReturnType<typeof createGameMock>[];
  pdfs?: { pdfs: ReturnType<typeof createPdfMock>[] };
  uploadResponse?: { documentId: string; fileName?: string };
  pdfStatusSequence?: Array<{ processingStatus: string; processingError?: string | null }>;
  ruleSpec?: ReturnType<typeof createRuleSpecMock> | null;
  createGameResponse?: ReturnType<typeof createGameMock>;
  createGameError?: { status: number; error: string };
  uploadError?: { status: number; error: string };
  ruleSpecError?: { status: number; error: unknown };
  publishRuleSpecResponse?: ReturnType<typeof createRuleSpecMock>;
  publishRuleSpecError?: { status: number; error: string };
  retryParseResponse?: { success: boolean };
  retryParseError?: { status: number; error: string };
}

/**
 * Creates a mock auth response
 */
export function createAuthMock(options: AuthMockOptions = {}) {
  return {
    user: {
      id: options.userId ?? 'user-1',
      email: options.email ?? 'user@example.com',
      role: options.role ?? 'Admin',
      displayName: options.displayName ?? 'Test User'
    },
    expiresAt: options.expiresAt ?? new Date().toISOString()
  };
}

/**
 * Creates a mock game object
 */
export function createGameMock(options: GameMockOptions = {}) {
  return {
    id: options.id ?? 'game-1',
    name: options.name ?? 'Test Game',
    createdAt: options.createdAt ?? new Date().toISOString()
  };
}

/**
 * Creates a mock PDF document
 */
export function createPdfMock(options: PdfMockOptions = {}): {
  id: string;
  fileName: string;
  fileSizeBytes: number;
  uploadedAt: string;
  uploadedByUserId: string;
  status?: string;
  logUrl?: string | null;
  processingStatus?: string;
  processingError?: string | null;
} {
  const baseResponse = {
    id: options.id ?? 'pdf-123',
    fileName: options.fileName ?? 'test.pdf',
    fileSizeBytes: options.fileSizeBytes ?? 1024,
    uploadedAt: options.uploadedAt ?? new Date().toISOString(),
    uploadedByUserId: options.uploadedByUserId ?? 'user-1'
  };

  // If status is provided (for PDF list response)
  if (options.status !== undefined) {
    return {
      ...baseResponse,
      status: options.status,
      logUrl: options.logUrl ?? null
    };
  }

  // If processingStatus is provided (for status polling response)
  if (options.processingStatus !== undefined) {
    return {
      ...baseResponse,
      processingStatus: options.processingStatus,
      processingError: options.processingError ?? null
    };
  }

  // Default: return basic response
  return baseResponse;
}

/**
 * Creates a mock RuleSpec object
 */
export function createRuleSpecMock(options: RuleSpecMockOptions = {}) {
  return {
    gameId: options.gameId ?? 'game-1',
    version: options.version ?? 'v1',
    createdAt: options.createdAt ?? new Date().toISOString(),
    rules: options.rules ?? [
      { id: 'r1', text: 'Test rule', section: null, page: null, line: null }
    ]
  };
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
    auth = createAuthMock(),
    games = [createGameMock()],
    pdfs = { pdfs: [] },
    uploadResponse = { documentId: 'pdf-123', fileName: 'test.pdf' },
    pdfStatusSequence = [],
    ruleSpec = createRuleSpecMock(),
    createGameResponse,
    createGameError,
    uploadError,
    ruleSpecError,
    publishRuleSpecResponse,
    publishRuleSpecError,
    retryParseResponse,
    retryParseError
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
      createJsonResponse(createGameResponse ?? createGameMock({ id: 'game-new', name: 'New Game' }), 201)
    );
  }

  // PDFs list for a game (using route parameters)
  router.get('/api/v1/games/:gameId/pdfs', () => createJsonResponse(pdfs));

  // Upload PDF
  if (uploadError) {
    router.post('/api/v1/ingest/pdf', () =>
      createErrorResponse(uploadError.status, { error: uploadError.error })
    );
  } else {
    router.post('/api/v1/ingest/pdf', () => createJsonResponse(uploadResponse, 201));
  }

  // PDF status polling (stateful with closure)
  let statusIndex = 0;
  router.get('/api/v1/pdfs/:documentId/text', ({ params }) => {
    if (pdfStatusSequence.length > 0) {
      const nextStatus = pdfStatusSequence[statusIndex] ?? pdfStatusSequence[pdfStatusSequence.length - 1];
      if (statusIndex < pdfStatusSequence.length - 1) {
        statusIndex++;
      }
      return createJsonResponse({
        id: params.documentId,
        fileName: uploadResponse.fileName ?? 'test.pdf',
        processingStatus: nextStatus.processingStatus,
        processingError: nextStatus.processingError ?? null
      });
    }
    // Default: return completed status
    return createJsonResponse({
      id: params.documentId,
      fileName: uploadResponse.fileName ?? 'test.pdf',
      processingStatus: 'completed',
      processingError: null
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

  return jest.fn(router.toMockImplementation()) as jest.MockedFunction<typeof fetch>;
}
