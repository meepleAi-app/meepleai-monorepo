/**
 * BDD Tests for upload-mocks fixture
 *
 * Given: Shared mock factories exist
 * When: Developer uses them in upload tests
 * Then: Mocks are created with correct defaults and customization
 */

import { createJsonResponse, createErrorResponse, setupUploadMocks } from './upload-mocks';
import {
  createMockAuthResponse,
  createMockGame,
  createMockPdfDocument,
  createMockRuleSpec,
} from './common-fixtures';

describe('upload-mocks fixture', () => {
  describe('Given auth mock factory', () => {
    describe('When created with default options', () => {
      it('Then returns auth response with default values', () => {
        const authMock = createMockAuthResponse();

        expect(authMock.user.id).toBe('user-1');
        expect(authMock.user.email).toBe('test@meepleai.dev');
        expect(authMock.user.role).toBe('User');
        expect(authMock.user.displayName).toBe('Test User');
        expect(authMock.expiresAt).toBeDefined();
      });
    });

    describe('When created with custom options', () => {
      it('Then returns auth response with custom values', () => {
        const authMock = createMockAuthResponse({
          id: 'user-custom',
          email: 'custom@example.com',
          role: 'Editor',
          displayName: 'Custom User',
        });

        expect(authMock.user.id).toBe('user-custom');
        expect(authMock.user.email).toBe('custom@example.com');
        expect(authMock.user.role).toBe('Editor');
        expect(authMock.user.displayName).toBe('Custom User');
      });
    });
  });

  describe('Given game mock factory', () => {
    describe('When created with default options', () => {
      it('Then returns game with default values', () => {
        const gameMock = createMockGame();

        expect(gameMock.id).toBe('game-1');
        expect(gameMock.title).toBe('Test Game');
        expect(gameMock.createdAt).toBeDefined();
      });
    });

    describe('When created with custom options', () => {
      it('Then returns game with custom values', () => {
        const gameMock = createMockGame({
          id: 'game-custom',
          title: 'Custom Game',
          createdAt: '2024-01-01T00:00:00Z',
        });

        expect(gameMock.id).toBe('game-custom');
        expect(gameMock.title).toBe('Custom Game');
        expect(gameMock.createdAt).toBe('2024-01-01T00:00:00Z');
      });
    });
  });

  describe('Given PDF mock factory', () => {
    describe('When created for status polling response', () => {
      it('Then includes processingStatus field', () => {
        const pdfMock = createMockPdfDocument({
          id: 'pdf-123',
          processingStatus: 'completed',
          processingError: null,
        });

        expect(pdfMock.id).toBe('pdf-123');
        expect(pdfMock.processingStatus).toBe('completed');
        expect(pdfMock.processingError).toBe(null);
      });
    });

    describe('When created for PDF list response', () => {
      it('Then includes status field', () => {
        const pdfMock = createMockPdfDocument({
          id: 'pdf-123',
          status: 'failed',
          logUrl: 'http://example.com/log',
        });

        expect(pdfMock.id).toBe('pdf-123');
        expect(pdfMock.status).toBe('failed');
        expect(pdfMock.logUrl).toBe('http://example.com/log');
      });
    });

    describe('When created with custom file size', () => {
      it('Then returns PDF with custom size', () => {
        const pdfMock = createMockPdfDocument({
          fileName: 'large.pdf',
          fileSizeBytes: 5242880, // 5 MB
        });

        expect(pdfMock.fileName).toBe('large.pdf');
        expect(pdfMock.fileSizeBytes).toBe(5242880);
      });
    });
  });

  describe('Given RuleSpec mock factory', () => {
    describe('When created with default options', () => {
      it('Then returns RuleSpec with default atoms', () => {
        const ruleSpecMock = createMockRuleSpec();

        expect(ruleSpecMock.gameId).toBe('game-1');
        expect(ruleSpecMock.version).toBe('v1');
        expect(ruleSpecMock.atoms).toBeDefined();
        expect(ruleSpecMock.atoms.length).toBeGreaterThan(0);
      });
    });

    describe('When created with custom atoms', () => {
      it('Then returns RuleSpec with custom atoms', () => {
        const ruleSpecMock = createMockRuleSpec({
          gameId: 'game-custom',
          version: 'v2',
          atoms: [
            { id: 'r1', text: 'Rule 1', section: 'Setup', page: '1', line: '5' },
            { id: 'r2', text: 'Rule 2', section: 'Gameplay', page: '2', line: '10' },
          ],
        });

        expect(ruleSpecMock.gameId).toBe('game-custom');
        expect(ruleSpecMock.version).toBe('v2');
        expect(ruleSpecMock.atoms).toHaveLength(2);
        expect(ruleSpecMock.atoms[0].section).toBe('Setup');
        expect(ruleSpecMock.atoms[1].section).toBe('Gameplay');
      });
    });
  });

  describe('Given JSON response helper', () => {
    describe('When creating successful response', () => {
      it('Then returns response with ok: true', async () => {
        const response = await createJsonResponse({ success: true }, 200);

        expect(response.ok).toBe(true);
        expect(response.status).toBe(200);
        expect(response.statusText).toBe('OK');
        expect(await response.json()).toEqual({ success: true });
      });
    });
  });

  describe('Given error response helper', () => {
    describe('When creating error response', () => {
      it('Then returns response with ok: false', async () => {
        const response = await createErrorResponse(500, { error: 'Server error' });

        expect(response.ok).toBe(false);
        expect(response.status).toBe(500);
        expect(response.statusText).toBe('Internal Server Error');
        expect(await response.json()).toEqual({ error: 'Server error' });
      });
    });

    describe('When creating 401 error', () => {
      it('Then uses Unauthorized status text', async () => {
        const response = await createErrorResponse(401, {});

        expect(response.statusText).toBe('Unauthorized');
      });
    });
  });

  describe('Given setupUploadMocks router', () => {
    describe('When configured with default mocks', () => {
      it('Then handles /auth/me endpoint', async () => {
        const mockFetch = setupUploadMocks();
        const response = await mockFetch('http://localhost/api/v1/auth/me', { method: 'GET' });

        expect(response.ok).toBe(true);
        const data = await response.json();
        expect(data.user.role).toBe('User');
      });

      it('Then handles /games GET endpoint', async () => {
        const mockFetch = setupUploadMocks();
        const response = await mockFetch('http://localhost/api/v1/games', { method: 'GET' });

        expect(response.ok).toBe(true);
        const data = await response.json();
        expect(Array.isArray(data)).toBe(true);
        expect(data[0].id).toBe('game-1');
      });

      it('Then handles /games POST endpoint', async () => {
        const mockFetch = setupUploadMocks();
        const response = await mockFetch('http://localhost/api/v1/games', { method: 'POST' });

        expect(response.ok).toBe(true);
        const data = await response.json();
        expect(data.id).toBe('game-new');
      });

      it('Then handles /games/{id}/pdfs endpoint', async () => {
        const mockFetch = setupUploadMocks();
        const response = await mockFetch('http://localhost/api/v1/games/game-1/pdfs', {
          method: 'GET',
        });

        expect(response.ok).toBe(true);
        const data = await response.json();
        expect(data.pdfs).toEqual([]);
      });

      it('Then handles /ingest/pdf endpoint', async () => {
        const mockFetch = setupUploadMocks();
        const response = await mockFetch('http://localhost/api/v1/ingest/pdf', { method: 'POST' });

        expect(response.ok).toBe(true);
        const data = await response.json();
        expect(data.documentId).toBe('pdf-123');
      });

      it('Then handles PDF status polling endpoint', async () => {
        const mockFetch = setupUploadMocks();
        const response = await mockFetch('http://localhost/api/v1/pdfs/pdf-123/text', {
          method: 'GET',
        });

        expect(response.ok).toBe(true);
        const data = await response.json();
        expect(data.processingStatus).toBe('completed');
      });

      it('Then handles /games/{id}/rulespec GET endpoint', async () => {
        const mockFetch = setupUploadMocks();
        const response = await mockFetch('http://localhost/api/v1/games/game-1/rulespec', {
          method: 'GET',
        });

        expect(response.ok).toBe(true);
        const data = await response.json();
        expect(data.gameId).toBe('game-1');
        expect(data.atoms).toBeDefined();
      });

      it('Then handles /games/{id}/rulespec PUT endpoint', async () => {
        const mockFetch = setupUploadMocks();
        const response = await mockFetch('http://localhost/api/v1/games/game-1/rulespec', {
          method: 'PUT',
        });

        expect(response.ok).toBe(true);
        const data = await response.json();
        expect(data.gameId).toBe('game-1');
      });
    });

    describe('When configured with error scenarios', () => {
      it('Then returns error for createGame when configured', async () => {
        const mockFetch = setupUploadMocks({
          createGameError: { status: 500, error: 'Database error' },
        });

        const response = await mockFetch('http://localhost/api/v1/games', { method: 'POST' });

        expect(response.ok).toBe(false);
        expect(response.status).toBe(500);
        const data = await response.json();
        expect(data.error).toBe('Database error');
      });

      it('Then returns error for upload when configured', async () => {
        const mockFetch = setupUploadMocks({
          uploadError: { status: 500, error: 'Upload failed' },
        });

        const response = await mockFetch('http://localhost/api/v1/ingest/pdf', { method: 'POST' });

        expect(response.ok).toBe(false);
        const data = await response.json();
        expect(data.error).toBe('Upload failed');
      });

      it('Then returns error for ruleSpec GET when configured', async () => {
        const mockFetch = setupUploadMocks({
          ruleSpecError: { status: 401, error: {} },
        });

        const response = await mockFetch('http://localhost/api/v1/games/game-1/rulespec', {
          method: 'GET',
        });

        expect(response.ok).toBe(false);
        expect(response.status).toBe(401);
      });

      it('Then returns error for publish when configured', async () => {
        const mockFetch = setupUploadMocks({
          publishRuleSpecError: { status: 500, error: 'Publish failed' },
        });

        const response = await mockFetch('http://localhost/api/v1/games/game-1/rulespec', {
          method: 'PUT',
        });

        expect(response.ok).toBe(false);
        const data = await response.json();
        expect(data.error).toBe('Publish failed');
      });
    });

    describe('When configured with status sequence for polling', () => {
      it('Then returns statuses in sequence', async () => {
        const mockFetch = setupUploadMocks({
          pdfStatusSequence: [
            { processingStatus: 'pending' },
            { processingStatus: 'processing' },
            { processingStatus: 'completed' },
          ],
        });

        const response1 = await mockFetch('http://localhost/api/v1/pdfs/pdf-123/text', {
          method: 'GET',
        });
        const data1 = await response1.json();
        expect(data1.processingStatus).toBe('pending');

        const response2 = await mockFetch('http://localhost/api/v1/pdfs/pdf-123/text', {
          method: 'GET',
        });
        const data2 = await response2.json();
        expect(data2.processingStatus).toBe('processing');

        const response3 = await mockFetch('http://localhost/api/v1/pdfs/pdf-123/text', {
          method: 'GET',
        });
        const data3 = await response3.json();
        expect(data3.processingStatus).toBe('completed');

        // Subsequent calls should return last status
        const response4 = await mockFetch('http://localhost/api/v1/pdfs/pdf-123/text', {
          method: 'GET',
        });
        const data4 = await response4.json();
        expect(data4.processingStatus).toBe('completed');
      });
    });

    describe('When called with unexpected endpoint', () => {
      it('Then throws error with helpful message', async () => {
        const mockFetch = setupUploadMocks();

        // Issue #1951: MockApiRouter returns Promise, so test async rejection
        await expect(
          mockFetch('http://localhost/api/v1/unknown/endpoint', { method: 'GET' })
        ).rejects.toThrow('MockApiRouter: No handler for GET /api/v1/unknown/endpoint');
      });
    });

    describe('When configured with null auth', () => {
      it('Then returns error for unauthenticated scenario', async () => {
        const mockFetch = setupUploadMocks({ auth: null });
        const response = await mockFetch('http://localhost/api/v1/auth/me', { method: 'GET' });

        // Issue #1951: Unauthenticated returns error object, not null
        expect(response.status).toBe(401);
        const data = await response.json();
        expect(data).toEqual({ error: 'Unauthorized' });
      });
    });

    describe('When configured with empty games list', () => {
      it('Then returns empty array', async () => {
        const mockFetch = setupUploadMocks({ games: [] });
        const response = await mockFetch('http://localhost/api/v1/games', { method: 'GET' });

        const data = await response.json();
        expect(data).toEqual([]);
      });
    });

    describe('When configured with PDFs containing failed PDF', () => {
      it('Then returns PDFs with status field', async () => {
        const mockFetch = setupUploadMocks({
          pdfs: {
            pdfs: [
              createMockPdfDocument({
                id: 'pdf-failed',
                fileName: 'failed.pdf',
                status: 'failed',
                logUrl: 'http://localhost/logs/pdf-failed',
              }),
            ],
          },
        });

        const response = await mockFetch('http://localhost/api/v1/games/game-1/pdfs', {
          method: 'GET',
        });
        const data = await response.json();

        expect(data.pdfs).toHaveLength(1);
        expect(data.pdfs[0].status).toBe('failed');
        expect(data.pdfs[0].logUrl).toBe('http://localhost/logs/pdf-failed');
      });
    });

    describe('When handling retry parse endpoint', () => {
      it('Then returns success response', async () => {
        const mockFetch = setupUploadMocks({
          retryParseResponse: { success: true },
        });

        const response = await mockFetch('http://localhost/api/v1/ingest/pdf/pdf-123/retry', {
          method: 'POST',
        });
        const data = await response.json();

        expect(response.ok).toBe(true);
        expect(data.success).toBe(true);
      });

      it('Then returns error when configured', async () => {
        const mockFetch = setupUploadMocks({
          retryParseError: { status: 500, error: 'Retry failed' },
        });

        const response = await mockFetch('http://localhost/api/v1/ingest/pdf/pdf-123/retry', {
          method: 'POST',
        });
        const data = await response.json();

        expect(response.ok).toBe(false);
        expect(data.error).toBe('Retry failed');
      });
    });
  });
});
