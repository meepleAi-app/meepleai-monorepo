/**
 * Shared mock factories for upload workflow tests
 *
 * This module provides reusable mock factories to reduce duplication
 * across upload test files (game-selection, pdf-upload, review-edit, edge-cases)
 */

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
 * Helper to create JSON response
 */
export const createJsonResponse = (data: unknown, status = 200) =>
  Promise.resolve({
    ok: status >= 200 && status < 300,
    status,
    statusText: status >= 200 && status < 300 ? 'OK' : 'Error',
    json: () => Promise.resolve(data)
  } as Response);

/**
 * Helper to create error response
 */
export const createErrorResponse = (status: number, body: unknown = { error: 'Error' }, statusText?: string) =>
  Promise.resolve({
    ok: false,
    status,
    statusText:
      statusText ??
      (status === 401 ? 'Unauthorized' : status === 500 ? 'Internal Server Error' : 'Error'),
    json: () => Promise.resolve(body)
  } as Response);

/**
 * Sets up a comprehensive fetch mock router for upload workflow tests
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

  let statusIndex = 0;

  return jest.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === 'string' ? input : input.toString();
    const method = init?.method ?? 'GET';

    // Auth endpoint
    if (url.endsWith('/auth/me')) {
      return createJsonResponse(auth);
    }

    // Games list
    if (url.endsWith('/games') && method === 'GET') {
      return createJsonResponse(games);
    }

    // Create game
    if (url.endsWith('/games') && method === 'POST') {
      if (createGameError) {
        return createErrorResponse(createGameError.status, { error: createGameError.error });
      }
      return createJsonResponse(createGameResponse ?? createGameMock({ id: 'game-new', name: 'New Game' }));
    }

    // PDFs list for a game
    if (url.includes('/games/') && url.includes('/pdfs') && method === 'GET') {
      return createJsonResponse(pdfs);
    }

    // Upload PDF
    if (url.endsWith('/ingest/pdf') && method === 'POST') {
      if (uploadError) {
        return createErrorResponse(uploadError.status, { error: uploadError.error });
      }
      return createJsonResponse(uploadResponse);
    }

    // PDF status polling
    if (url.includes('/pdfs/') && url.endsWith('/text') && method === 'GET') {
      if (pdfStatusSequence.length > 0) {
        const nextStatus = pdfStatusSequence[statusIndex] ?? pdfStatusSequence[pdfStatusSequence.length - 1];
        if (statusIndex < pdfStatusSequence.length - 1) {
          statusIndex++;
        }
        return createJsonResponse({
          id: uploadResponse.documentId,
          fileName: uploadResponse.fileName ?? 'test.pdf',
          processingStatus: nextStatus.processingStatus,
          processingError: nextStatus.processingError ?? null
        });
      }
      // Default: return completed status
      return createJsonResponse({
        id: uploadResponse.documentId,
        fileName: uploadResponse.fileName ?? 'test.pdf',
        processingStatus: 'completed',
        processingError: null
      });
    }

    // Get RuleSpec
    if (url.includes('/games/') && url.endsWith('/rulespec') && method === 'GET') {
      if (ruleSpecError) {
        return createErrorResponse(ruleSpecError.status, ruleSpecError.error);
      }
      return createJsonResponse(ruleSpec);
    }

    // Publish RuleSpec
    if (url.includes('/games/') && url.endsWith('/rulespec') && method === 'PUT') {
      if (publishRuleSpecError) {
        return createErrorResponse(publishRuleSpecError.status, { error: publishRuleSpecError.error });
      }
      return createJsonResponse(publishRuleSpecResponse ?? ruleSpec);
    }

    // Retry parse
    if (url.includes('/ingest/pdf/') && url.endsWith('/retry') && method === 'POST') {
      if (retryParseError) {
        return createErrorResponse(retryParseError.status, { error: retryParseError.error });
      }
      return createJsonResponse(retryParseResponse ?? { success: true });
    }

    throw new Error(`Unexpected fetch call to ${url} with method ${method}`);
  }) as jest.MockedFunction<typeof fetch>;
}
