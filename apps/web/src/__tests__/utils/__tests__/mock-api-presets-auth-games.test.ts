/**
 * Tests for Auth and Games Mock API Presets
 *
 * Coverage: auth, games presets
 */

import { MockApiPresets } from '../mock-api-presets';
import { createTestRouter, expectFluentApi, expectErrorResponse, expectSuccessResponse } from './mock-api-presets.test-helpers';

describe('MockApiPresets - Auth and Games', () => {
  describe('auth preset', () => {
    describe('Default behavior', () => {
      it('should register GET /auth/me with default user', async () => {
        const router = createTestRouter();
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
        const router = createTestRouter();
        MockApiPresets.auth(router);

        const response = await router.handle('/auth/me');

        expect(response.status).toBe(200);
        expect(response.ok).toBe(true);
      });
    });

    describe('Custom options', () => {
      it('should use custom userId', async () => {
        const router = createTestRouter();
        MockApiPresets.auth(router, { userId: 'custom-user-123' });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data.user.id).toBe('custom-user-123');
      });

      it('should use custom email', async () => {
        const router = createTestRouter();
        MockApiPresets.auth(router, { email: 'custom@example.com' });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data.user.email).toBe('custom@example.com');
      });

      it('should use custom role', async () => {
        const router = createTestRouter();
        MockApiPresets.auth(router, { role: 'Editor' });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data.user.role).toBe('Editor');
      });

      it('should use custom displayName', async () => {
        const router = createTestRouter();
        MockApiPresets.auth(router, { displayName: 'Custom User' });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data.user.displayName).toBe('Custom User');
      });

      it('should use custom expiresAt', async () => {
        const router = createTestRouter();
        const customExpiry = '2025-12-31T23:59:59Z';
        MockApiPresets.auth(router, { expiresAt: customExpiry });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data.expiresAt).toBe(customExpiry);
      });

      it('should handle all custom options together', async () => {
        const router = createTestRouter();
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
        const router = createTestRouter();
        MockApiPresets.auth(router, { unauthorized: true });

        const response = await router.handle('/auth/me');

        await expectErrorResponse(response, 401, 'Unauthorized');
      });

      it('should return error object when unauthorized', async () => {
        const router = createTestRouter();
        MockApiPresets.auth(router, { unauthorized: true });

        const response = await router.handle('/auth/me');
        const data = await response.json();

        expect(data).toHaveProperty('error');
        expect(data.error).toBe('Unauthorized');
      });
    });

    describe('Fluent API', () => {
      it('should return router for chaining', () => {
        const router = createTestRouter();
        expectFluentApi(MockApiPresets.auth, router);
      });
    });
  });

  describe('games preset', () => {
    describe('Default behavior', () => {
      it('should register GET /games with empty array', async () => {
        const router = createTestRouter();
        MockApiPresets.games(router);

        const response = await router.handle('/games');
        const data = await response.json();

        expect(Array.isArray(data)).toBe(true);
        expect(data).toHaveLength(0);
      });

      it('should register POST /games with default response', async () => {
        const router = createTestRouter();
        MockApiPresets.games(router);

        const response = await router.handle('/games', {
          method: 'POST',
          body: JSON.stringify({ title: 'Chess' }),
        });
        const data = await expectSuccessResponse(response, 201);

        expect(data).toHaveProperty('id');
        expect(data).toHaveProperty('title');
        expect(data).toHaveProperty('createdAt');
      });
    });

    describe('Custom games list', () => {
      it('should return custom games array', async () => {
        const router = createTestRouter();
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
        const router = createTestRouter();
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
        const router = createTestRouter();
        MockApiPresets.games(router, {
          createError: { status: 400, message: 'Invalid game name' },
        });

        const response = await router.handle('/games', { method: 'POST' });

        await expectErrorResponse(response, 400, 'Invalid game name');
      });

      it('should include error message in response', async () => {
        const router = createTestRouter();
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
        const router = createTestRouter();
        expectFluentApi(MockApiPresets.games, router);
      });
    });
  });
});
