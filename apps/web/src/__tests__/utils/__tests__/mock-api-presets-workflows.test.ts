/**
 * Tests for Workflow Mock API Presets (ingest, ruleSpec, uploadWorkflow)
 *
 * Coverage: ingest, ruleSpec, uploadWorkflow presets (complex multi-endpoint workflows)
 */

import { MockApiPresets } from '../mock-api-presets';
import { createTestRouter, expectFluentApi, expectRoutesRegistered } from './mock-api-presets.test-helpers';

describe('MockApiPresets - Workflows', () => {
  describe('ingest preset', () => {
    describe('Default behavior', () => {
      it('should register POST /ingest/pdf with default response', async () => {
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
        MockApiPresets.ingest(router, {
          uploadError: { status: 413, message: 'File too large' },
        });

        const response = await router.handle('/ingest/pdf', { method: 'POST' });

        expect(response.status).toBe(413);
      });

      it('should include error message', async () => {
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
        MockApiPresets.ingest(router, { retrySuccess: false });

        const response = await router.handle('/ingest/pdf/pdf-1/retry', {
          method: 'POST',
        });
        const data = await response.json();

        expect(data.success).toBe(false);
      });

      it('should return error when retryError is provided', async () => {
        const router = createTestRouter();
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
        const router = createTestRouter();
        expectFluentApi(MockApiPresets.ingest, router);
      });
    });
  });

  describe('ruleSpec preset', () => {
    describe('Default behavior', () => {
      it('should register GET /games/:gameId/rulespec', async () => {
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
        MockApiPresets.ruleSpec(router, {
          ruleSpecError: { status: 404, message: 'RuleSpec not found' },
        });

        const response = await router.handle('/games/game-1/rulespec');

        expect(response.status).toBe(404);
      });
    });

    describe('Publish scenarios', () => {
      it('should return ruleSpec on successful publish', async () => {
        const router = createTestRouter();
        MockApiPresets.ruleSpec(router, { publishSuccess: true });

        const response = await router.handle('/games/game-1/rulespec', {
          method: 'PUT',
        });
        const data = await response.json();

        expect(data).toHaveProperty('gameId');
        expect(data).toHaveProperty('version');
      });

      it('should return error object on failed publish', async () => {
        const router = createTestRouter();
        MockApiPresets.ruleSpec(router, { publishSuccess: false });

        const response = await router.handle('/games/game-1/rulespec', {
          method: 'PUT',
        });
        const data = await response.json();

        expect(data.success).toBe(false);
        expect(response.status).toBe(500);
      });

      it('should return error when publishError is provided', async () => {
        const router = createTestRouter();
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
        const router = createTestRouter();
        expectFluentApi(MockApiPresets.ruleSpec, router);
      });
    });
  });

  describe('uploadWorkflow preset', () => {
    it('should register all upload-related endpoints', async () => {
      const router = createTestRouter();
      MockApiPresets.uploadWorkflow(router);

      // Should have registered auth, games, pdfs, ingest, ruleSpec
      expect(
        expectRoutesRegistered(router, [
          '/auth/me',
          '/games',
          '/games/:gameId/pdfs',
          '/ingest/pdf',
          '/games/:gameId/rulespec',
        ])
      ).toBe(true);
    });

    it('should pass options to individual presets', async () => {
      const router = createTestRouter();
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
      const router = createTestRouter();
      expect(() => MockApiPresets.uploadWorkflow(router, {})).not.toThrow();
    });

    it('should return router for chaining', () => {
      const router = createTestRouter();
      expectFluentApi(MockApiPresets.uploadWorkflow, router);
    });
  });
});
