/**
 * Tests for Mock API Presets
 *
 * These tests ensure that the MockApiPresets helper class correctly
 * configures MockApiRouter for common API scenarios.
 *
 * Coverage targets:
 * - Statements: 0% → 90%+
 * - Branches: 0% → 90%+
 * - Functions: 0% → 90%+
 */

import { MockApiRouter, createJsonResponse, createErrorResponse } from '../mock-api-router';
import { MockApiPresets } from '../mock-api-presets';

describe('MockApiPresets', () => {
  let router: MockApiRouter;

  beforeEach(() => {
    router = new MockApiRouter();
  });

  describe('auth preset', () => {
    describe('Default behavior', () => {
      it('should register GET /auth/me with default user', async () => {
        MockApiPresets.auth(router);

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data).toHaveProperty('user');
        expect(data.user).toMatchObject({
          id: 'user-1',
          email: 'user@example.com',
          role: 'Admin',
          displayName: 'Test User',
        });
        expect(data).toHaveProperty('expiresAt');
      });

      it('should return status 200', async () => {
        MockApiPresets.auth(router);

        const response = await router.handle('/auth/me');

        expect(response.status).toBe(200);
        expect(response.ok).toBe(true);
      });
    });

    describe('Custom options', () => {
      it('should use custom userId', async () => {
        MockApiPresets.auth(router, { userId: 'custom-user-123' });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data.user.id).toBe('custom-user-123');
      });

      it('should use custom email', async () => {
        MockApiPresets.auth(router, { email: 'custom@example.com' });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data.user.email).toBe('custom@example.com');
      });

      it('should use custom role', async () => {
        MockApiPresets.auth(router, { role: 'Editor' });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data.user.role).toBe('Editor');
      });

      it('should use custom displayName', async () => {
        MockApiPresets.auth(router, { displayName: 'Custom User' });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data.user.displayName).toBe('Custom User');
      });

      it('should use custom expiresAt', async () => {
        const customExpiry = '2025-12-31T23:59:59Z';
        MockApiPresets.auth(router, { expiresAt: customExpiry });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data.expiresAt).toBe(customExpiry);
      });

      it('should handle all custom options together', async () => {
        MockApiPresets.auth(router, {
          userId: 'user-999',
          email: 'admin@example.com',
          role: 'Admin',
          displayName: 'Admin User',
          expiresAt: '2025-12-31T00:00:00Z',
        });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data.user).toMatchObject({
          id: 'user-999',
          email: 'admin@example.com',
          role: 'Admin',
          displayName: 'Admin User',
        });
        expect(data.expiresAt).toBe('2025-12-31T00:00:00Z');
      });
    });

    describe('Unauthorized scenario', () => {
      it('should return 401 when unauthorized flag is true', async () => {
        MockApiPresets.auth(router, { unauthorized: true });

        const response = await router.handle('/auth/me');

        expect(response.status).toBe(401);
        expect(response.ok).toBe(false);
      });

      it('should return error object when unauthorized', async () => {
        MockApiPresets.auth(router, { unauthorized: true });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data).toHaveProperty('error');
        expect(data.error).toBe('Unauthorized');
      });
    });

    describe('Fluent API', () => {
      it('should return router for chaining', () => {
        const result = MockApiPresets.auth(router);

        expect(result).toBe(router);
      });
    });
  });

  describe('games preset', () => {
    describe('Default behavior', () => {
      it('should register GET /games with empty array', async () => {
        MockApiPresets.games(router);

        const response = await router.handle('/games');
        const data = await response.json();

        expect(Array.isArray(data)).toBe(true);
        expect(data).toHaveLength(0);
      });

      it('should register POST /games with default response', async () => {
        MockApiPresets.games(router);

        const response = await router.handle('/games', {
          method: 'POST',
          body: JSON.stringify({ title: 'Chess' }),
        });
        const data = await response.json();

        expect(data).toHaveProperty('id');
        expect(data).toHaveProperty('title');
        expect(data).toHaveProperty('createdAt');
        expect(response.status).toBe(201);
      });
    });

    describe('Custom games list', () => {
      it('should return custom games array', async () => {
        const customGames = [
          { id: 'game-1', title: 'Chess', createdAt: '2025-01-01T00:00:00Z' },
          { id: 'game-2', title: 'Catan', createdAt: '2025-01-02T00:00:00Z' },
        ];

        MockApiPresets.games(router, { games: customGames });

        const response = await router.handle('/games');
        const data = await response.json();

        expect(data).toEqual(customGames);
      });
    });

    describe('Custom create response', () => {
      it('should return custom create response', async () => {
        const customResponse = {
          id: 'custom-game-id',
          title: 'Monopoly',
          createdAt: '2025-05-01T00:00:00Z',
        };

        MockApiPresets.games(router, { createResponse: customResponse });

        const response = await router.handle('/games', { method: 'POST' });
        const data = await response.json();

        expect(data).toEqual(customResponse);
      });
    });

    describe('Create error scenario', () => {
      it('should return error when createError is provided', async () => {
        MockApiPresets.games(router, {
          createError: { status: 400, message: 'Invalid game name' },
        });

        const response = await router.handle('/games', { method: 'POST' });

        expect(response.status).toBe(400);
        expect(response.ok).toBe(false);
      });

      it('should include error message in response', async () => {
        MockApiPresets.games(router, {
          createError: { status: 403, message: 'Forbidden' },
        });

        const response = await router.handle('/games', { method: 'POST' });
        const data = await response.json();

        expect(data.error).toBe('Forbidden');
      });
    });

    describe('Fluent API', () => {
      it('should return router for chaining', () => {
        const result = MockApiPresets.games(router);

        expect(result).toBe(router);
      });
    });
  });

  describe('pdfs preset', () => {
    describe('Default behavior', () => {
      it('should register GET /games/:gameId/pdfs with empty array', async () => {
        MockApiPresets.pdfs(router);

        const response = await router.handle('/games/game-1/pdfs');
        const data = await response.json();

        expect(data).toHaveProperty('pdfs');
        expect(data.pdfs).toEqual([]);
      });
    });

    describe('Custom PDFs list', () => {
      it('should return custom PDFs array', async () => {
        const customPdfs = [
          {
            id: 'pdf-1',
            fileName: 'rules.pdf',
            fileSizeBytes: 1024,
            uploadedAt: '2025-01-01T00:00:00Z',
            uploadedByUserId: 'user-1',
            status: 'completed',
            logUrl: null,
          },
        ];

        MockApiPresets.pdfs(router, { pdfs: customPdfs });

        const response = await router.handle('/games/game-1/pdfs');
        const data = await response.json();

        expect(data.pdfs).toEqual(customPdfs);
      });

      it('should work with different gameId parameter', async () => {
        MockApiPresets.pdfs(router, { pdfs: [{ id: 'pdf-1', fileName: 'test.pdf' }] });

        const response = await router.handle('/games/game-999/pdfs');
        const data = await response.json();

        expect(data.pdfs).toHaveLength(1);
      });
    });

    describe('Fluent API', () => {
      it('should return router for chaining', () => {
        const result = MockApiPresets.pdfs(router);

        expect(result).toBe(router);
      });
    });
  });

  describe('ingest preset', () => {
    describe('Default behavior', () => {
      it('should register POST /ingest/pdf with default response', async () => {
        MockApiPresets.ingest(router);

        const response = await router.handle('/ingest/pdf', { method: 'POST' });
        const data = await response.json();

        expect(data).toMatchObject({
          documentId: 'pdf-123',
          fileName: 'test.pdf',
        });
        expect(response.status).toBe(201);
      });

      it('should register GET /pdfs/:documentId/text with default status', async () => {
        MockApiPresets.ingest(router);

        const response = await router.handle('/pdfs/pdf-123/text');
        const data = await response.json();

        expect(data).toMatchObject({
          id: 'pdf-123',
          fileName: 'test.pdf',
          processingStatus: 'completed',
          processingError: null,
        });
      });

      it('should register POST /ingest/pdf/:documentId/retry with success', async () => {
        MockApiPresets.ingest(router);

        const response = await router.handle('/ingest/pdf/pdf-123/retry', {
          method: 'POST',
        });
        const data = await response.json();

        expect(data.success).toBe(true);
      });
    });

    describe('Custom upload response', () => {
      it('should use custom upload response', async () => {
        MockApiPresets.ingest(router, {
          uploadResponse: { documentId: 'custom-pdf-id', fileName: 'custom.pdf' },
        });

        const response = await router.handle('/ingest/pdf', { method: 'POST' });
        const data = await response.json();

        expect(data.documentId).toBe('custom-pdf-id');
        expect(data.fileName).toBe('custom.pdf');
      });
    });

    describe('Upload error scenario', () => {
      it('should return error when uploadError is provided', async () => {
        MockApiPresets.ingest(router, {
          uploadError: { status: 413, message: 'File too large' },
        });

        const response = await router.handle('/ingest/pdf', { method: 'POST' });

        expect(response.status).toBe(413);
      });

      it('should include error message', async () => {
        MockApiPresets.ingest(router, {
          uploadError: { status: 400, message: 'Invalid PDF' },
        });

        const response = await router.handle('/ingest/pdf', { method: 'POST' });
        const data = await response.json();

        expect(data.error).toBe('Invalid PDF');
      });
    });

    describe('Status polling with multiple responses', () => {
      it('should cycle through status responses', async () => {
        MockApiPresets.ingest(router, {
          statusResponses: [
            {
              documentId: 'pdf-1',
              fileName: 'test.pdf',
              processingStatus: 'pending',
              processingError: null,
            },
            {
              documentId: 'pdf-1',
              fileName: 'test.pdf',
              processingStatus: 'processing',
              processingError: null,
            },
            {
              documentId: 'pdf-1',
              fileName: 'test.pdf',
              processingStatus: 'completed',
              processingError: null,
            },
          ],
        });

        // First poll
        const response1 = await router.handle('/pdfs/pdf-1/text');
        const data1 = await response1.json();
        expect(data1.processingStatus).toBe('pending');

        // Second poll
        const response2 = await router.handle('/pdfs/pdf-1/text');
        const data2 = await response2.json();
        expect(data2.processingStatus).toBe('processing');

        // Third poll
        const response3 = await router.handle('/pdfs/pdf-1/text');
        const data3 = await response3.json();
        expect(data3.processingStatus).toBe('completed');
      });

      it('should stick to last status after cycling', async () => {
        MockApiPresets.ingest(router, {
          statusResponses: [
            {
              documentId: 'pdf-1',
              fileName: 'test.pdf',
              processingStatus: 'completed',
              processingError: null,
            },
          ],
        });

        // Poll multiple times
        await router.handle('/pdfs/pdf-1/text');
        await router.handle('/pdfs/pdf-1/text');
        const response3 = await router.handle('/pdfs/pdf-1/text');
        const data3 = await response3.json();

        expect(data3.processingStatus).toBe('completed');
      });

      it('should handle failed processing status', async () => {
        MockApiPresets.ingest(router, {
          statusResponses: [
            {
              documentId: 'pdf-1',
              fileName: 'test.pdf',
              processingStatus: 'failed',
              processingError: 'Invalid PDF format',
            },
          ],
        });

        const response = await router.handle('/pdfs/pdf-1/text');
        const data = await response.json();

        expect(data.processingStatus).toBe('failed');
        expect(data.processingError).toBe('Invalid PDF format');
      });
    });

    describe('Retry scenarios', () => {
      it('should return success false when retrySuccess is false', async () => {
        MockApiPresets.ingest(router, { retrySuccess: false });

        const response = await router.handle('/ingest/pdf/pdf-1/retry', {
          method: 'POST',
        });
        const data = await response.json();

        expect(data.success).toBe(false);
      });

      it('should return error when retryError is provided', async () => {
        MockApiPresets.ingest(router, {
          retryError: { status: 404, message: 'Document not found' },
        });

        const response = await router.handle('/ingest/pdf/pdf-1/retry', {
          method: 'POST',
        });

        expect(response.status).toBe(404);
      });
    });

    describe('Fluent API', () => {
      it('should return router for chaining', () => {
        const result = MockApiPresets.ingest(router);

        expect(result).toBe(router);
      });
    });
  });

  describe('ruleSpec preset', () => {
    describe('Default behavior', () => {
      it('should register GET /games/:gameId/rulespec', async () => {
        MockApiPresets.ruleSpec(router);

        const response = await router.handle('/games/game-1/rulespec');
        const data = await response.json();

        expect(data).toHaveProperty('gameId');
        expect(data).toHaveProperty('version');
        expect(data).toHaveProperty('createdAt');
        expect(data).toHaveProperty('rules');
        expect(Array.isArray(data.rules)).toBe(true);
      });

      it('should register PUT /games/:gameId/rulespec', async () => {
        MockApiPresets.ruleSpec(router);

        const response = await router.handle('/games/game-1/rulespec', {
          method: 'PUT',
          body: JSON.stringify({ version: 'v2' }),
        });

        expect(response.status).toBe(200);
      });
    });

    describe('Custom ruleSpec', () => {
      it('should return custom ruleSpec', async () => {
        const customRuleSpec = {
          gameId: 'custom-game',
          version: 'v2',
          createdAt: '2025-01-01T00:00:00Z',
          rules: [
            { id: 'r1', text: 'Rule 1', section: 'Setup', page: '1', line: null },
            { id: 'r2', text: 'Rule 2', section: 'Gameplay', page: '2', line: null },
          ],
        };

        MockApiPresets.ruleSpec(router, { ruleSpec: customRuleSpec });

        const response = await router.handle('/games/custom-game/rulespec');
        const data = await response.json();

        expect(data).toEqual(customRuleSpec);
      });
    });

    describe('RuleSpec error scenario', () => {
      it('should return error when ruleSpecError is provided', async () => {
        MockApiPresets.ruleSpec(router, {
          ruleSpecError: { status: 404, message: 'RuleSpec not found' },
        });

        const response = await router.handle('/games/game-1/rulespec');

        expect(response.status).toBe(404);
      });
    });

    describe('Publish scenarios', () => {
      it('should return ruleSpec on successful publish', async () => {
        MockApiPresets.ruleSpec(router, { publishSuccess: true });

        const response = await router.handle('/games/game-1/rulespec', {
          method: 'PUT',
        });
        const data = await response.json();

        expect(data).toHaveProperty('gameId');
        expect(data).toHaveProperty('version');
      });

      it('should return error object on failed publish', async () => {
        MockApiPresets.ruleSpec(router, { publishSuccess: false });

        const response = await router.handle('/games/game-1/rulespec', {
          method: 'PUT',
        });
        const data = await response.json();

        expect(data.success).toBe(false);
        expect(response.status).toBe(500);
      });

      it('should return error when publishError is provided', async () => {
        MockApiPresets.ruleSpec(router, {
          publishError: { status: 400, message: 'Invalid rule format' },
        });

        const response = await router.handle('/games/game-1/rulespec', {
          method: 'PUT',
        });

        expect(response.status).toBe(400);
      });
    });

    describe('Fluent API', () => {
      it('should return router for chaining', () => {
        const result = MockApiPresets.ruleSpec(router);

        expect(result).toBe(router);
      });
    });
  });

  describe('uploadWorkflow preset', () => {
    it('should register all upload-related endpoints', async () => {
      MockApiPresets.uploadWorkflow(router);

      // Should have registered auth, games, pdfs, ingest, ruleSpec
      const routes = router.getRoutes();

      expect(routes.some((r) => r.pattern === '/auth/me')).toBe(true);
      expect(routes.some((r) => r.pattern === '/games')).toBe(true);
      expect(routes.some((r) => r.pattern === '/games/:gameId/pdfs')).toBe(true);
      expect(routes.some((r) => r.pattern === '/ingest/pdf')).toBe(true);
      expect(routes.some((r) => r.pattern === '/games/:gameId/rulespec')).toBe(true);
    });

    it('should pass options to individual presets', async () => {
      MockApiPresets.uploadWorkflow(router, {
        auth: { userId: 'custom-user' },
        games: { games: [{ id: 'game-1', title: 'Chess' }] },
      });

      const authResponse = await router.handle('/auth/me');
      const authData = await authResponse.json();
      expect(authData.user.id).toBe('custom-user');

      const gamesResponse = await router.handle('/games');
      const gamesData = await gamesResponse.json();
      expect(gamesData).toHaveLength(1);
    });

    it('should handle empty options', () => {
      expect(() => MockApiPresets.uploadWorkflow(router, {})).not.toThrow();
    });

    it('should return router for chaining', () => {
      const result = MockApiPresets.uploadWorkflow(router);

      expect(result).toBe(router);
    });
  });

  describe('admin preset', () => {
    describe('Default behavior', () => {
      it('should register GET /admin/requests with empty array', async () => {
        MockApiPresets.admin(router);

        const response = await router.handle('/admin/requests');
        const data = await response.json();

        expect(data).toHaveProperty('requests');
        expect(data.requests).toEqual([]);
      });

      it('should register GET /admin/stats with empty object', async () => {
        MockApiPresets.admin(router);

        const response = await router.handle('/admin/stats');
        const data = await response.json();

        expect(data).toEqual({});
      });

      it('should register POST /admin/export with success response', async () => {
        MockApiPresets.admin(router);

        const response = await router.handle('/admin/export', { method: 'POST' });
        const data = await response.json();

        expect(data).toHaveProperty('url');
        expect(data.url).toContain('blob:');
      });
    });

    describe('Custom options', () => {
      it('should return custom requests', async () => {
        const customRequests = [{ id: 'req-1' }, { id: 'req-2' }];
        MockApiPresets.admin(router, { requests: customRequests });

        const response = await router.handle('/admin/requests');
        const data = await response.json();

        expect(data.requests).toEqual(customRequests);
      });

      it('should return custom stats', async () => {
        const customStats = { totalUsers: 100, totalGames: 50 };
        MockApiPresets.admin(router, { stats: customStats });

        const response = await router.handle('/admin/stats');
        const data = await response.json();

        expect(data).toEqual(customStats);
      });

      it('should return export error when provided', async () => {
        MockApiPresets.admin(router, {
          exportError: { status: 403, message: 'Forbidden' },
        });

        const response = await router.handle('/admin/export', { method: 'POST' });

        expect(response.status).toBe(403);
      });
    });

    describe('Fluent API', () => {
      it('should return router for chaining', () => {
        const result = MockApiPresets.admin(router);

        expect(result).toBe(router);
      });
    });
  });

  describe('agents preset', () => {
    describe('Default behavior', () => {
      it('should register POST /agents/qa with default response', async () => {
        MockApiPresets.agents(router);

        const response = await router.handle('/agents/qa', {
          method: 'POST',
          body: JSON.stringify({ question: 'Test?' }),
        });
        const data = await response.json();

        expect(data).toMatchObject({
          answer: 'Test answer',
          snippets: [],
        });
      });

      it('should register POST /agents/explain with default response', async () => {
        MockApiPresets.agents(router);

        const response = await router.handle('/agents/explain', {
          method: 'POST',
        });
        const data = await response.json();

        expect(data).toMatchObject({
          answer: 'Test explanation',
          snippets: [],
        });
      });

      it('should register POST /agents/feedback with success', async () => {
        MockApiPresets.agents(router);

        const response = await router.handle('/agents/feedback', {
          method: 'POST',
        });
        const data = await response.json();

        expect(data.success).toBe(true);
      });
    });

    describe('Custom responses', () => {
      it('should use custom Q&A response', async () => {
        MockApiPresets.agents(router, {
          qaResponse: {
            answer: 'Custom answer',
            snippets: [{ text: 'snippet', page: 1 }],
          },
        });

        const response = await router.handle('/agents/qa', { method: 'POST' });
        const data = await response.json();

        expect(data.answer).toBe('Custom answer');
        expect(data.snippets).toHaveLength(1);
      });

      it('should use custom explain response', async () => {
        MockApiPresets.agents(router, {
          explainResponse: {
            answer: 'Custom explanation',
            snippets: [{ text: 'snippet' }],
          },
        });

        const response = await router.handle('/agents/explain', { method: 'POST' });
        const data = await response.json();

        expect(data.answer).toBe('Custom explanation');
      });

      it('should use custom feedback success', async () => {
        MockApiPresets.agents(router, { feedbackSuccess: false });

        const response = await router.handle('/agents/feedback', { method: 'POST' });
        const data = await response.json();

        expect(data.success).toBe(false);
      });
    });

    describe('Error scenarios', () => {
      it('should return Q&A error when provided', async () => {
        MockApiPresets.agents(router, {
          qaError: { status: 500, message: 'Internal error' },
        });

        const response = await router.handle('/agents/qa', { method: 'POST' });

        expect(response.status).toBe(500);
      });

      it('should return explain error when provided', async () => {
        MockApiPresets.agents(router, {
          explainError: { status: 400, message: 'Bad request' },
        });

        const response = await router.handle('/agents/explain', { method: 'POST' });

        expect(response.status).toBe(400);
      });
    });

    describe('Fluent API', () => {
      it('should return router for chaining', () => {
        const result = MockApiPresets.agents(router);

        expect(result).toBe(router);
      });
    });
  });

  describe('Preset chaining', () => {
    it('should allow calling multiple presets', () => {
      expect(() => {
        MockApiPresets.auth(router);
        MockApiPresets.games(router);
        MockApiPresets.pdfs(router);
        MockApiPresets.ingest(router);
        MockApiPresets.ruleSpec(router);
        MockApiPresets.admin(router);
        MockApiPresets.agents(router);
      }).not.toThrow();
    });

    it('should register all routes when multiple presets used', () => {
      MockApiPresets.auth(router);
      MockApiPresets.games(router);
      MockApiPresets.agents(router);

      const routes = router.getRoutes();

      expect(routes.some((r) => r.pattern === '/auth/me')).toBe(true);
      expect(routes.some((r) => r.pattern === '/games')).toBe(true);
      expect(routes.some((r) => r.pattern === '/agents/qa')).toBe(true);
    });
  });
});
