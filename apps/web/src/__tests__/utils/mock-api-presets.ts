/**
 * MockApiRouter Preset Helpers
 *
 * Pre-configured route handlers for common API endpoints across MeepleAI tests.
 * These presets reduce boilerplate and ensure consistency in mock responses.
 *
 * @example
 * ```typescript
 * const router = new MockApiRouter();
 * MockApiPresets.auth(router, { userId: 'user-1', role: 'Admin' });
 * MockApiPresets.games(router, [{ id: 'game-1', name: 'Chess' }]);
 * ```
 */

import { MockApiRouter, createJsonResponse, createErrorResponse } from './mock-api-router';

export interface AuthPresetOptions {
  userId?: string;
  email?: string;
  role?: 'Admin' | 'Editor' | 'Viewer';
  displayName?: string;
  expiresAt?: string;
  unauthorized?: boolean;
}

export interface GamePresetOptions {
  games?: Array<{
    id: string;
    name: string;
    createdAt?: string;
  }>;
  createResponse?: {
    id: string;
    name: string;
    createdAt?: string;
  };
  createError?: { status: number; message: string };
}

export interface PdfPresetOptions {
  gameId?: string;
  pdfs?: Array<{
    id: string;
    fileName: string;
    fileSizeBytes?: number;
    uploadedAt?: string;
    uploadedByUserId?: string;
    status?: string;
    logUrl?: string | null;
  }>;
}

export interface IngestPresetOptions {
  uploadResponse?: {
    documentId: string;
    fileName?: string;
  };
  uploadError?: { status: number; message: string };
  statusResponses?: Array<{
    documentId: string;
    fileName: string;
    processingStatus: 'pending' | 'processing' | 'completed' | 'failed';
    processingError?: string | null;
  }>;
  retrySuccess?: boolean;
  retryError?: { status: number; message: string };
}

export interface RuleSpecPresetOptions {
  gameId?: string;
  ruleSpec?: {
    gameId: string;
    version: string;
    createdAt?: string;
    rules: Array<{
      id: string;
      text: string;
      section?: string | null;
      page?: string | null;
      line?: string | null;
    }>;
  };
  ruleSpecError?: { status: number; message: string };
  publishSuccess?: boolean;
  publishError?: { status: number; message: string };
}

/**
 * Preset collection for common MeepleAI API endpoints
 */
export class MockApiPresets {
  /**
   * Register authentication endpoints
   *
   * Routes:
   * - GET /auth/me
   */
  static auth(router: MockApiRouter, options: AuthPresetOptions = {}): MockApiRouter {
    const {
      userId = 'user-1',
      email = 'user@example.com',
      role = 'Admin',
      displayName = 'Test User',
      expiresAt = new Date(Date.now() + 86400000).toISOString(),
      unauthorized = false
    } = options;

    if (unauthorized) {
      router.get('/auth/me', () => createErrorResponse(401, { error: 'Unauthorized' }));
    } else {
      router.get('/auth/me', () =>
        createJsonResponse({
          user: {
            id: userId,
            email,
            role,
            displayName
          },
          expiresAt
        })
      );
    }

    return router;
  }

  /**
   * Register game management endpoints
   *
   * Routes:
   * - GET /games
   * - POST /games
   */
  static games(router: MockApiRouter, options: GamePresetOptions = {}): MockApiRouter {
    const {
      games = [],
      createResponse = { id: 'game-new', name: 'New Game', createdAt: new Date().toISOString() },
      createError
    } = options;

    // List games
    router.get('/games', () => createJsonResponse(games));

    // Create game
    if (createError) {
      router.post('/games', () =>
        createErrorResponse(createError.status, { error: createError.message })
      );
    } else {
      router.post('/games', () => createJsonResponse(createResponse, 201));
    }

    return router;
  }

  /**
   * Register PDF list endpoints for a specific game
   *
   * Routes:
   * - GET /games/:gameId/pdfs
   */
  static pdfs(router: MockApiRouter, options: PdfPresetOptions = {}): MockApiRouter {
    const { pdfs = [] } = options;

    router.get('/games/:gameId/pdfs', () => createJsonResponse({ pdfs }));

    return router;
  }

  /**
   * Register PDF ingest and processing endpoints
   *
   * Routes:
   * - POST /ingest/pdf
   * - GET /pdfs/:documentId/text
   * - POST /ingest/pdf/:documentId/retry
   */
  static ingest(router: MockApiRouter, options: IngestPresetOptions = {}): MockApiRouter {
    const {
      uploadResponse = { documentId: 'pdf-123', fileName: 'test.pdf' },
      uploadError,
      statusResponses = [],
      retrySuccess = true,
      retryError
    } = options;

    // Upload PDF
    if (uploadError) {
      router.post('/ingest/pdf', () =>
        createErrorResponse(uploadError.status, { error: uploadError.message })
      );
    } else {
      router.post('/ingest/pdf', () => createJsonResponse(uploadResponse, 201));
    }

    // PDF status polling
    let statusIndex = 0;
    router.get('/pdfs/:documentId/text', ({ params }) => {
      if (statusResponses.length > 0) {
        const response = statusResponses[statusIndex] ?? statusResponses[statusResponses.length - 1];
        if (statusIndex < statusResponses.length - 1) {
          statusIndex++;
        }
        return createJsonResponse({
          ...response,
          id: params.documentId
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

    // Retry parse
    if (retryError) {
      router.post('/ingest/pdf/:documentId/retry', () =>
        createErrorResponse(retryError.status, { error: retryError.message })
      );
    } else {
      router.post('/ingest/pdf/:documentId/retry', () =>
        createJsonResponse({ success: retrySuccess })
      );
    }

    return router;
  }

  /**
   * Register RuleSpec endpoints
   *
   * Routes:
   * - GET /games/:gameId/rulespec
   * - PUT /games/:gameId/rulespec
   */
  static ruleSpec(router: MockApiRouter, options: RuleSpecPresetOptions = {}): MockApiRouter {
    const {
      ruleSpec = {
        gameId: 'game-1',
        version: 'v1',
        createdAt: new Date().toISOString(),
        rules: [
          { id: 'r1', text: 'Test rule', section: null, page: null, line: null }
        ]
      },
      ruleSpecError,
      publishSuccess = true,
      publishError
    } = options;

    // Get RuleSpec
    if (ruleSpecError) {
      router.get('/games/:gameId/rulespec', () =>
        createErrorResponse(ruleSpecError.status, { error: ruleSpecError.message })
      );
    } else {
      router.get('/games/:gameId/rulespec', () => createJsonResponse(ruleSpec));
    }

    // Publish RuleSpec
    if (publishError) {
      router.put('/games/:gameId/rulespec', () =>
        createErrorResponse(publishError.status, { error: publishError.message })
      );
    } else {
      router.put('/games/:gameId/rulespec', () =>
        createJsonResponse(publishSuccess ? ruleSpec : { success: false }, publishSuccess ? 200 : 500)
      );
    }

    return router;
  }

  /**
   * Register a complete upload workflow preset
   *
   * This combines auth, games, pdfs, ingest, and ruleSpec presets
   * for a typical upload test scenario.
   */
  static uploadWorkflow(
    router: MockApiRouter,
    options: {
      auth?: AuthPresetOptions;
      games?: GamePresetOptions;
      pdfs?: PdfPresetOptions;
      ingest?: IngestPresetOptions;
      ruleSpec?: RuleSpecPresetOptions;
    } = {}
  ): MockApiRouter {
    this.auth(router, options.auth);
    this.games(router, options.games);
    this.pdfs(router, options.pdfs);
    this.ingest(router, options.ingest);
    this.ruleSpec(router, options.ruleSpec);

    return router;
  }

  /**
   * Register admin dashboard endpoints
   *
   * Routes:
   * - GET /admin/requests
   * - GET /admin/stats
   * - POST /admin/export
   */
  static admin(
    router: MockApiRouter,
    options: {
      requests?: unknown[];
      stats?: unknown;
      exportError?: { status: number; message: string };
    } = {}
  ): MockApiRouter {
    const { requests = [], stats = {}, exportError } = options;

    router.get('/admin/requests', () => createJsonResponse({ requests }));
    router.get('/admin/stats', () => createJsonResponse(stats));

    if (exportError) {
      router.post('/admin/export', () =>
        createErrorResponse(exportError.status, { error: exportError.message })
      );
    } else {
      router.post('/admin/export', () =>
        createJsonResponse({ url: 'blob:mock-csv-url' })
      );
    }

    return router;
  }

  /**
   * Register chat/agent endpoints
   *
   * Routes:
   * - POST /agents/qa
   * - POST /agents/explain
   * - POST /agents/feedback
   */
  static agents(
    router: MockApiRouter,
    options: {
      qaResponse?: { answer: string; snippets?: unknown[] };
      explainResponse?: { answer: string; snippets?: unknown[] };
      qaError?: { status: number; message: string };
      explainError?: { status: number; message: string };
      feedbackSuccess?: boolean;
    } = {}
  ): MockApiRouter {
    const {
      qaResponse = { answer: 'Test answer', snippets: [] },
      explainResponse = { answer: 'Test explanation', snippets: [] },
      qaError,
      explainError,
      feedbackSuccess = true
    } = options;

    // Q&A endpoint
    if (qaError) {
      router.post('/agents/qa', () =>
        createErrorResponse(qaError.status, { error: qaError.message })
      );
    } else {
      router.post('/agents/qa', () => createJsonResponse(qaResponse));
    }

    // Explain endpoint
    if (explainError) {
      router.post('/agents/explain', () =>
        createErrorResponse(explainError.status, { error: explainError.message })
      );
    } else {
      router.post('/agents/explain', () => createJsonResponse(explainResponse));
    }

    // Feedback endpoint
    router.post('/agents/feedback', () =>
      createJsonResponse({ success: feedbackSuccess })
    );

    return router;
  }
}
