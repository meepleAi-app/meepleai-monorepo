/**
 * HTTP Client Request Tests
 *
 * Tests for HTTP methods (GET, POST, PUT, DELETE, postFile) and configuration.
 */

import { z } from 'zod';
import { HttpClient, getApiBase } from '../core/httpClient';
import { setStoredApiKey, clearStoredApiKey } from '../core/apiKeyStore';
import { UnauthorizedError, NotFoundError, SchemaValidationError } from '../core/errors';
import {
  setupTestEnvironment,
  createSuccessResponse,
  createErrorResponse,
  createBlobResponse,
  createNoContentResponse,
  type TestSetup,
} from './httpClient.test-helpers';

describe('getApiBase', () => {
  let originalEnv: NodeJS.ProcessEnv;

  beforeEach(() => {
    originalEnv = process.env;
  });

  afterEach(() => {
    process.env = originalEnv;
  });

  describe('development mode', () => {
    beforeEach(() => {
      process.env.NODE_ENV = 'development';
    });

    it('should return empty string in browser to use Next.js proxy (Issue #2366)', () => {
      // Mock window object (browser environment)
      global.window = {} as any;

      expect(getApiBase()).toBe('');

      // Cleanup
      delete (global as any).window;
    });

    it('should return localhost in Node.js/SSR environment', () => {
      // window is undefined in Node.js/test environment
      expect(getApiBase()).toBe('http://localhost:8080');
    });

    it('should return empty string in browser even when NEXT_PUBLIC_API_BASE is set', () => {
      global.window = {} as any;
      process.env.NEXT_PUBLIC_API_BASE = 'https://api.meepleai.dev';

      expect(getApiBase()).toBe('');

      delete (global as any).window;
    });
  });

  describe('production mode', () => {
    beforeEach(() => {
      process.env.NODE_ENV = 'production';
    });

    it('should return environment variable when set', () => {
      process.env.NEXT_PUBLIC_API_BASE = 'https://api.meepleai.dev';
      expect(getApiBase()).toBe('https://api.meepleai.dev');
    });

    it('should return localhost when env not set', () => {
      delete process.env.NEXT_PUBLIC_API_BASE;
      expect(getApiBase()).toBe('http://localhost:8080');
    });

    it('should return localhost when env is undefined string', () => {
      process.env.NEXT_PUBLIC_API_BASE = 'undefined';
      expect(getApiBase()).toBe('http://localhost:8080');
    });

    it('should return localhost when env is null string', () => {
      process.env.NEXT_PUBLIC_API_BASE = 'null';
      expect(getApiBase()).toBe('http://localhost:8080');
    });

    it('should trim whitespace from env variable', () => {
      process.env.NEXT_PUBLIC_API_BASE = '  https://api.meepleai.dev  ';
      expect(getApiBase()).toBe('https://api.meepleai.dev');
    });
  });
});

describe('HttpClient - HTTP Methods', () => {
  let setup: TestSetup;

  beforeEach(() => {
    setup = setupTestEnvironment();
  });

  describe('GET requests', () => {
    it('should make GET request with correct headers', async () => {
      setup.mockFetch.mockResolvedValueOnce(createSuccessResponse({ data: 'test' }));

      await setup.client.get('/api/v1/test');

      expect(setup.mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/test',
        expect.objectContaining({
          method: 'GET',
          credentials: 'include',
          headers: expect.objectContaining({
            'X-Correlation-ID': 'test-uuid-123',
          }),
        })
      );
    });

    it('should return null for 401 responses', async () => {
      setup.mockFetch.mockResolvedValueOnce(createErrorResponse(401, { error: 'Unauthorized' }));

      const result = await setup.client.get('/api/v1/test');

      expect(result).toBeNull();
    });

    it('should validate response with Zod schema', async () => {
      const schema = z.object({ id: z.string(), name: z.string() });

      setup.mockFetch.mockResolvedValueOnce(createSuccessResponse({ id: '123', name: 'Test' }));

      const result = await setup.client.get('/api/v1/test', schema);

      expect(result).toEqual({ id: '123', name: 'Test' });
    });

    it('should throw SchemaValidationError on invalid response', async () => {
      const schema = z.object({ id: z.string(), name: z.string() });

      setup.mockFetch.mockResolvedValueOnce(
        createSuccessResponse({ id: 123 }) // Invalid: missing name, wrong id type
      );

      await expect(setup.client.get('/api/v1/test', schema)).rejects.toThrow(SchemaValidationError);
    });

    it('should include Authorization header when an API key is stored', async () => {
      await setStoredApiKey('mpl_test_demo');

      setup.mockFetch.mockResolvedValueOnce(createSuccessResponse({ ok: true }));

      await setup.client.get('/api/v1/test');

      expect(setup.mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/test',
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: 'ApiKey mpl_test_demo',
          }),
        })
      );

      clearStoredApiKey();
    });

    it('should throw error for non-401 failures', async () => {
      setup.mockFetch.mockResolvedValueOnce(
        createErrorResponse(404, { error: 'Not found' }, 'test-404')
      );

      await expect(setup.client.get('/api/v1/test')).rejects.toThrow(NotFoundError);
    });
  });

  describe('POST requests', () => {
    it('should make POST request with JSON body', async () => {
      setup.mockFetch.mockResolvedValueOnce(createSuccessResponse({ success: true }));

      await setup.client.post('/api/v1/test', { key: 'value' });

      expect(setup.mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/test',
        expect.objectContaining({
          method: 'POST',
          credentials: 'include',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
          }),
          body: JSON.stringify({ key: 'value' }),
        })
      );
    });

    it('should throw error for 401 responses', async () => {
      setup.mockFetch.mockResolvedValueOnce(createErrorResponse(401, { error: 'Unauthorized' }));

      await expect(setup.client.post('/api/v1/test', {})).rejects.toThrow(UnauthorizedError);
    });

    it('should handle 204 No Content responses', async () => {
      setup.mockFetch.mockResolvedValueOnce(createNoContentResponse());

      const result = await setup.client.post('/api/v1/test', {});

      expect(result).toBeUndefined();
    });

    it('should validate response with Zod schema', async () => {
      const schema = z.object({ success: z.boolean() });

      setup.mockFetch.mockResolvedValueOnce(createSuccessResponse({ success: true }));

      const result = await setup.client.post('/api/v1/test', {}, schema);

      expect(result).toEqual({ success: true });
    });

    it('should handle empty body', async () => {
      setup.mockFetch.mockResolvedValueOnce(createSuccessResponse({ success: true }));

      await setup.client.post('/api/v1/test');

      expect(setup.mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          body: JSON.stringify({}),
        })
      );
    });
  });

  describe('PUT requests', () => {
    it('should make PUT request with JSON body', async () => {
      setup.mockFetch.mockResolvedValueOnce(createSuccessResponse({ updated: true }));

      await setup.client.put('/api/v1/test', { key: 'updated' });

      expect(setup.mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/test',
        expect.objectContaining({
          method: 'PUT',
          credentials: 'include',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
          }),
          body: JSON.stringify({ key: 'updated' }),
        })
      );
    });

    it('should throw error for 401 responses', async () => {
      setup.mockFetch.mockResolvedValueOnce(createErrorResponse(401, { error: 'Unauthorized' }));

      await expect(setup.client.put('/api/v1/test', {})).rejects.toThrow(UnauthorizedError);
    });

    it('should validate response with Zod schema', async () => {
      const schema = z.object({ updated: z.boolean() });

      setup.mockFetch.mockResolvedValueOnce(createSuccessResponse({ updated: true }));

      const result = await setup.client.put('/api/v1/test', {}, schema);

      expect(result).toEqual({ updated: true });
    });
  });

  describe('DELETE requests', () => {
    it('should make DELETE request', async () => {
      setup.mockFetch.mockResolvedValueOnce(createNoContentResponse());

      await setup.client.delete('/api/v1/test');

      expect(setup.mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/test',
        expect.objectContaining({
          method: 'DELETE',
          credentials: 'include',
        })
      );
    });

    it('should throw error for 401 responses', async () => {
      setup.mockFetch.mockResolvedValueOnce(createErrorResponse(401, { error: 'Unauthorized' }));

      await expect(setup.client.delete('/api/v1/test')).rejects.toThrow(UnauthorizedError);
    });

    it('should not attempt to parse response body', async () => {
      setup.mockFetch.mockResolvedValueOnce(createNoContentResponse());

      const result = await setup.client.delete('/api/v1/test');

      expect(result).toBeUndefined();
    });
  });

  describe('postFile (file downloads)', () => {
    it('should handle file download with blob response', async () => {
      const blob = new Blob(['test content'], { type: 'application/json' });

      setup.mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        blob: async () => blob,
        headers: new Headers({
          'Content-Disposition': 'attachment; filename="export.json"',
        }),
      });

      const result = await setup.client.postFile('/api/v1/export', { format: 'json' });

      expect(result.blob).toBe(blob);
      expect(result.filename).toBe('export.json');
    });

    it('should extract filename from Content-Disposition header', async () => {
      setup.mockFetch.mockResolvedValueOnce(createBlobResponse('test', 'test-file.txt'));

      const result = await setup.client.postFile('/api/v1/export', {});

      expect(result.filename).toBe('test-file.txt');
    });

    it('should use default filename if header missing', async () => {
      setup.mockFetch.mockResolvedValueOnce(createBlobResponse('test'));

      const result = await setup.client.postFile('/api/v1/export', {});

      expect(result.filename).toMatch(/^download-\d+$/);
    });

    it('should throw error for 401 responses', async () => {
      setup.mockFetch.mockResolvedValueOnce(createErrorResponse(401, { error: 'Unauthorized' }));

      await expect(setup.client.postFile('/api/v1/export', {})).rejects.toThrow(UnauthorizedError);
    });
  });
});
