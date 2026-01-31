/**
 * Tests for PDFs, Admin, and Agents Mock API Presets
 *
 * Coverage: pdfs, admin, agents presets and chaining
 */

import { MockApiPresets } from '../mock-api-presets';
import { createTestRouter, expectFluentApi, expectErrorResponse } from './mock-api-presets.test-helpers';

describe('MockApiPresets - PDFs, Admin, and Agents', () => {
  describe('pdfs preset', () => {
    describe('Default behavior', () => {
      it('should register GET /games/:gameId/pdfs with empty array', async () => {
        const router = createTestRouter();
        MockApiPresets.pdfs(router);

        const response = await router.handle('/games/game-1/pdfs');
        const data = await response.json();

        expect(data).toHaveProperty('pdfs');
        expect(data.pdfs).toEqual([]);
      });
    });

    describe('Custom PDFs list', () => {
      it('should return custom PDFs array', async () => {
        const router = createTestRouter();
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
        const router = createTestRouter();
        MockApiPresets.pdfs(router, { pdfs: [{ id: 'pdf-1', fileName: 'test.pdf' }] });

        const response = await router.handle('/games/game-999/pdfs');
        const data = await response.json();

        expect(data.pdfs).toHaveLength(1);
      });
    });

    describe('Fluent API', () => {
      it('should return router for chaining', () => {
        const router = createTestRouter();
        expectFluentApi(MockApiPresets.pdfs, router);
      });
    });
  });

  describe('admin preset', () => {
    describe('Default behavior', () => {
      it('should register GET /admin/requests with empty array', async () => {
        const router = createTestRouter();
        MockApiPresets.admin(router);

        const response = await router.handle('/admin/requests');
        const data = await response.json();

        expect(data).toHaveProperty('requests');
        expect(data.requests).toEqual([]);
      });

      it('should register GET /admin/stats with empty object', async () => {
        const router = createTestRouter();
        MockApiPresets.admin(router);

        const response = await router.handle('/admin/stats');
        const data = await response.json();

        expect(data).toEqual({});
      });

      it('should register POST /admin/export with success response', async () => {
        const router = createTestRouter();
        MockApiPresets.admin(router);

        const response = await router.handle('/admin/export', { method: 'POST' });
        const data = await response.json();

        expect(data).toHaveProperty('url');
        expect(data.url).toContain('blob:');
      });
    });

    describe('Custom options', () => {
      it('should return custom requests', async () => {
        const router = createTestRouter();
        const customRequests = [{ id: 'req-1' }, { id: 'req-2' }];
        MockApiPresets.admin(router, { requests: customRequests });

        const response = await router.handle('/admin/requests');
        const data = await response.json();

        expect(data.requests).toEqual(customRequests);
      });

      it('should return custom stats', async () => {
        const router = createTestRouter();
        const customStats = { totalUsers: 100, totalGames: 50 };
        MockApiPresets.admin(router, { stats: customStats });

        const response = await router.handle('/admin/stats');
        const data = await response.json();

        expect(data).toEqual(customStats);
      });

      it('should return export error when provided', async () => {
        const router = createTestRouter();
        MockApiPresets.admin(router, {
          exportError: { status: 403, message: 'Forbidden' },
        });

        const response = await router.handle('/admin/export', { method: 'POST' });

        await expectErrorResponse(response, 403);
      });
    });

    describe('Fluent API', () => {
      it('should return router for chaining', () => {
        const router = createTestRouter();
        expectFluentApi(MockApiPresets.admin, router);
      });
    });
  });

  describe('agents preset', () => {
    describe('Default behavior', () => {
      it('should register POST /agents/qa with default response', async () => {
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
        MockApiPresets.agents(router, { feedbackSuccess: false });

        const response = await router.handle('/agents/feedback', { method: 'POST' });
        const data = await response.json();

        expect(data.success).toBe(false);
      });
    });

    describe('Error scenarios', () => {
      it('should return Q&A error when provided', async () => {
        const router = createTestRouter();
        MockApiPresets.agents(router, {
          qaError: { status: 500, message: 'Internal error' },
        });

        const response = await router.handle('/agents/qa', { method: 'POST' });

        await expectErrorResponse(response, 500);
      });

      it('should return explain error when provided', async () => {
        const router = createTestRouter();
        MockApiPresets.agents(router, {
          explainError: { status: 400, message: 'Bad request' },
        });

        const response = await router.handle('/agents/explain', { method: 'POST' });

        await expectErrorResponse(response, 400);
      });
    });

    describe('Fluent API', () => {
      it('should return router for chaining', () => {
        const router = createTestRouter();
        expectFluentApi(MockApiPresets.agents, router);
      });
    });
  });

  describe('Preset chaining', () => {
    it('should allow calling multiple presets', () => {
      const router = createTestRouter();
      expect(() => {
        MockApiPresets.auth(router);
        MockApiPresets.games(router);
        MockApiPresets.pdfs(router);
        MockApiPresets.admin(router);
        MockApiPresets.agents(router);
      }).not.toThrow();
    });

    it('should register all routes when multiple presets used', () => {
      const router = createTestRouter();
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
