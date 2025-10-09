/**
 * MockApiRouter - Unit Tests
 *
 * Tests the core functionality of the MockApiRouter utility including:
 * - HTTP method registration (GET, POST, PUT, DELETE, PATCH)
 * - Pattern matching with route parameters
 * - Error handling for unmatched routes
 * - Fluent API chaining
 */

import { MockApiRouter, createJsonResponse, createErrorResponse } from './mock-api-router';

describe('MockApiRouter', () => {
  describe('Given simple routes without parameters', () => {
    describe('When registering GET routes', () => {
      it('Then handles exact path matches', async () => {
        const router = new MockApiRouter();
        const mockData = { message: 'Hello' };

        router.get('/api/hello', () => createJsonResponse(mockData));

        const response = await router.handle('/api/hello');
        const data = await response.json();

        expect(data).toEqual(mockData);
        expect(response.status).toBe(200);
        expect(response.ok).toBe(true);
      });
    });

    describe('When registering POST routes', () => {
      it('Then handles POST method correctly', async () => {
        const router = new MockApiRouter();
        const mockData = { id: '123' };

        router.post('/api/create', () => createJsonResponse(mockData, 201));

        const response = await router.handle('/api/create', { method: 'POST' });
        const data = await response.json();

        expect(data).toEqual(mockData);
        expect(response.status).toBe(201);
      });
    });

    describe('When registering multiple HTTP methods', () => {
      it('Then distinguishes between different methods on same path', async () => {
        const router = new MockApiRouter();

        router
          .get('/api/resource', () => createJsonResponse({ action: 'read' }))
          .post('/api/resource', () => createJsonResponse({ action: 'create' }))
          .put('/api/resource', () => createJsonResponse({ action: 'update' }))
          .delete('/api/resource', () => createJsonResponse({ action: 'delete' }))
          .patch('/api/resource', () => createJsonResponse({ action: 'patch' }));

        const getResponse = await router.handle('/api/resource', { method: 'GET' });
        const postResponse = await router.handle('/api/resource', { method: 'POST' });
        const putResponse = await router.handle('/api/resource', { method: 'PUT' });
        const deleteResponse = await router.handle('/api/resource', { method: 'DELETE' });
        const patchResponse = await router.handle('/api/resource', { method: 'PATCH' });

        expect(await getResponse.json()).toEqual({ action: 'read' });
        expect(await postResponse.json()).toEqual({ action: 'create' });
        expect(await putResponse.json()).toEqual({ action: 'update' });
        expect(await deleteResponse.json()).toEqual({ action: 'delete' });
        expect(await patchResponse.json()).toEqual({ action: 'patch' });
      });
    });
  });

  describe('Given routes with parameters', () => {
    describe('When pattern contains single parameter', () => {
      it('Then extracts parameter correctly', async () => {
        const router = new MockApiRouter();

        router.get('/games/:id', ({ params }) =>
          createJsonResponse({ gameId: params.id })
        );

        const response = await router.handle('/games/game-123');
        const data = await response.json();

        expect(data).toEqual({ gameId: 'game-123' });
      });
    });

    describe('When pattern contains multiple parameters', () => {
      it('Then extracts all parameters correctly', async () => {
        const router = new MockApiRouter();

        router.get('/games/:gameId/pdfs/:pdfId', ({ params }) =>
          createJsonResponse({
            gameId: params.gameId,
            pdfId: params.pdfId
          })
        );

        const response = await router.handle('/games/game-456/pdfs/pdf-789');
        const data = await response.json();

        expect(data).toEqual({
          gameId: 'game-456',
          pdfId: 'pdf-789'
        });
      });
    });

    describe('When pattern has parameters at different positions', () => {
      it('Then maintains correct parameter order', async () => {
        const router = new MockApiRouter();

        router.get('/users/:userId/games/:gameId/stats/:statId', ({ params }) =>
          createJsonResponse(params)
        );

        const response = await router.handle('/users/u-1/games/g-2/stats/s-3');
        const data = await response.json();

        expect(data).toEqual({
          userId: 'u-1',
          gameId: 'g-2',
          statId: 's-3'
        });
      });
    });
  });

  describe('Given full URL with protocol and host', () => {
    describe('When URL includes http://localhost:8080', () => {
      it('Then matches route pattern correctly', async () => {
        const router = new MockApiRouter();
        const mockData = { success: true };

        router.get('/auth/me', () => createJsonResponse(mockData));

        const response = await router.handle('http://localhost:8080/auth/me');
        const data = await response.json();

        expect(data).toEqual(mockData);
      });
    });

    describe('When URL includes parameters', () => {
      it('Then extracts parameters from full URL', async () => {
        const router = new MockApiRouter();

        router.get('/games/:id', ({ params }) =>
          createJsonResponse({ id: params.id })
        );

        const response = await router.handle('https://api.example.com/games/game-999');
        const data = await response.json();

        expect(data).toEqual({ id: 'game-999' });
      });
    });
  });

  describe('Given no matching route exists', () => {
    describe('When requesting unregistered endpoint', () => {
      it('Then throws error with helpful message', async () => {
        const router = new MockApiRouter();

        router
          .get('/auth/me', () => createJsonResponse({}))
          .get('/games', () => createJsonResponse([]))
          .post('/ingest/pdf', () => createJsonResponse({}));

        await expect(router.handle('/unknown/endpoint')).rejects.toThrow(
          'MockApiRouter: No handler for GET /unknown/endpoint'
        );
      });

      it('Then error message lists available routes', async () => {
        const router = new MockApiRouter();

        router
          .get('/auth/me', () => createJsonResponse({}))
          .post('/games', () => createJsonResponse({}));

        try {
          await router.handle('/missing');
          fail('Expected error to be thrown');
        } catch (error) {
          const errorMessage = (error as Error).message;
          expect(errorMessage).toContain('GET /auth/me');
          expect(errorMessage).toContain('POST /games');
        }
      });
    });

    describe('When wrong HTTP method used', () => {
      it('Then treats as unmatched route', async () => {
        const router = new MockApiRouter();

        router.get('/api/resource', () => createJsonResponse({}));

        await expect(router.handle('/api/resource', { method: 'POST' })).rejects.toThrow(
          'MockApiRouter: No handler for POST /api/resource'
        );
      });
    });
  });

  describe('Given fluent API usage', () => {
    describe('When chaining multiple route definitions', () => {
      it('Then all routes are registered correctly', async () => {
        const router = new MockApiRouter()
          .get('/route1', () => createJsonResponse({ route: 1 }))
          .post('/route2', () => createJsonResponse({ route: 2 }))
          .put('/route3', () => createJsonResponse({ route: 3 }));

        const response1 = await router.handle('/route1');
        const response2 = await router.handle('/route2', { method: 'POST' });
        const response3 = await router.handle('/route3', { method: 'PUT' });

        expect(await response1.json()).toEqual({ route: 1 });
        expect(await response2.json()).toEqual({ route: 2 });
        expect(await response3.json()).toEqual({ route: 3 });
      });
    });
  });

  describe('Given toMockImplementation helper', () => {
    describe('When converting to Jest mock', () => {
      it('Then works with Jest mock functions', async () => {
        const router = new MockApiRouter();
        const mockData = { userId: 'user-1' };

        router.get('/auth/me', () => createJsonResponse(mockData));

        const mockFetch = jest.fn(router.toMockImplementation());

        const response = await mockFetch('http://localhost:8080/auth/me');
        const data = await response.json();

        expect(data).toEqual(mockData);
        expect(mockFetch).toHaveBeenCalledWith('http://localhost:8080/auth/me');
      });
    });

    describe('When used as global.fetch replacement', () => {
      it('Then handles both string and URL input', async () => {
        const router = new MockApiRouter();
        router.get('/test', () => createJsonResponse({ ok: true }));

        const mockImpl = router.toMockImplementation();

        // Test with string
        const response1 = await mockImpl('/test');
        expect(await response1.json()).toEqual({ ok: true });

        // Test with URL object
        const response2 = await mockImpl(new URL('http://example.com/test'));
        expect(await response2.json()).toEqual({ ok: true });
      });
    });
  });

  describe('Given route context', () => {
    describe('When handler receives context', () => {
      it('Then context includes url, method, params, and init', async () => {
        const router = new MockApiRouter();
        let capturedContext;

        router.post('/games/:id', (context) => {
          capturedContext = context;
          return createJsonResponse({ ok: true });
        });

        await router.handle('/games/game-123', {
          method: 'POST',
          body: JSON.stringify({ name: 'Chess' })
        });

        expect(capturedContext).toMatchObject({
          params: { id: 'game-123' },
          url: '/games/game-123',
          method: 'POST',
          init: expect.objectContaining({
            method: 'POST'
          })
        });
      });
    });
  });

  describe('Given utility methods', () => {
    describe('When calling getRoutes', () => {
      it('Then returns list of registered routes', () => {
        const router = new MockApiRouter()
          .get('/route1', () => createJsonResponse({}))
          .post('/route2', () => createJsonResponse({}))
          .get('/games/:id', () => createJsonResponse({}));

        const routes = router.getRoutes();

        expect(routes).toEqual([
          { method: 'GET', pattern: '/route1' },
          { method: 'POST', pattern: '/route2' },
          { method: 'GET', pattern: '/games/:id' }
        ]);
      });
    });

    describe('When calling clear', () => {
      it('Then removes all routes', async () => {
        const router = new MockApiRouter();

        router.get('/route1', () => createJsonResponse({}));
        expect(router.getRoutes()).toHaveLength(1);

        router.clear();
        expect(router.getRoutes()).toHaveLength(0);

        await expect(router.handle('/route1')).rejects.toThrow();
      });
    });
  });

  describe('Given response helpers', () => {
    describe('When using createJsonResponse', () => {
      it('Then creates successful response with default status 200', async () => {
        const response = await createJsonResponse({ data: 'test' });

        expect(response.status).toBe(200);
        expect(response.ok).toBe(true);
        expect(response.statusText).toBe('OK');
        expect(await response.json()).toEqual({ data: 'test' });
      });

      it('Then creates custom status response', async () => {
        const response = await createJsonResponse({ id: '123' }, 201);

        expect(response.status).toBe(201);
        expect(response.ok).toBe(true);
      });
    });

    describe('When using createErrorResponse', () => {
      it('Then creates error response with correct status', async () => {
        const response = await createErrorResponse(404, { error: 'Not Found' });

        expect(response.status).toBe(404);
        expect(response.ok).toBe(false);
        expect(await response.json()).toEqual({ error: 'Not Found' });
      });

      it('Then uses default error messages', async () => {
        const response401 = await createErrorResponse(401);
        const response500 = await createErrorResponse(500);

        expect(response401.statusText).toBe('Unauthorized');
        expect(response500.statusText).toBe('Internal Server Error');
      });

      it('Then allows custom statusText', async () => {
        const response = await createErrorResponse(400, {}, 'Bad Input');

        expect(response.statusText).toBe('Bad Input');
      });
    });
  });

  describe('Given edge cases', () => {
    describe('When route pattern has special regex characters', () => {
      it('Then escapes them correctly', async () => {
        const router = new MockApiRouter();

        router.get('/api/v1.0/resource', () => createJsonResponse({ ok: true }));

        const response = await router.handle('/api/v1.0/resource');
        expect(await response.json()).toEqual({ ok: true });

        // Should NOT match v1X0 (dot should be literal)
        await expect(router.handle('/api/v1X0/resource')).rejects.toThrow();
      });
    });

    describe('When parameter name contains numbers', () => {
      it('Then extracts parameter correctly', async () => {
        const router = new MockApiRouter();

        router.get('/items/:item1/:item2', ({ params }) =>
          createJsonResponse(params)
        );

        const response = await router.handle('/items/first/second');
        expect(await response.json()).toEqual({ item1: 'first', item2: 'second' });
      });
    });

    describe('When URL path has trailing slash', () => {
      it('Then requires exact match including slash', async () => {
        const router = new MockApiRouter();

        router.get('/api/resource/', () => createJsonResponse({ withSlash: true }));
        router.get('/api/resource', () => createJsonResponse({ withoutSlash: true }));

        const withSlash = await router.handle('/api/resource/');
        const withoutSlash = await router.handle('/api/resource');

        expect(await withSlash.json()).toEqual({ withSlash: true });
        expect(await withoutSlash.json()).toEqual({ withoutSlash: true });
      });
    });

    describe('When method is lowercase', () => {
      it('Then normalizes to uppercase for matching', async () => {
        const router = new MockApiRouter();

        router.post('/api/create', () => createJsonResponse({ ok: true }));

        const response = await router.handle('/api/create', { method: 'post' });
        expect(await response.json()).toEqual({ ok: true });
      });
    });

    describe('When no routes registered', () => {
      it('Then error shows empty route list', async () => {
        const router = new MockApiRouter();

        try {
          await router.handle('/any/path');
          fail('Expected error');
        } catch (error) {
          expect((error as Error).message).toContain('(none)');
        }
      });
    });
  });
});
