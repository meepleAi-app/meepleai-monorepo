/**
 * HTTP Client Tests (FE-IMP-005)
 *
 * Tests for base HTTP client with error handling and Zod validation.
 */

import { z } from 'zod';
import { HttpClient, getApiBase, downloadFile } from '../core/httpClient';
import { setStoredApiKey, clearStoredApiKey } from '../core/apiKeyStore';
import {
  UnauthorizedError,
  NotFoundError,
  ServerError,
  SchemaValidationError,
} from '../core/errors';

describe('getApiBase', () => {
  let originalEnv: NodeJS.ProcessEnv;

  beforeEach(() => {
    originalEnv = process.env;
  });

  afterEach(() => {
    process.env = originalEnv;
  });

  it('should return environment variable when set', () => {
    process.env.NEXT_PUBLIC_API_BASE = 'https://api.meepleai.dev';
    expect(getApiBase()).toBe('https://api.meepleai.dev');
  });

  it('should return localhost when env not set', () => {
    delete process.env.NEXT_PUBLIC_API_BASE;
    expect(getApiBase()).toBe('http://localhost:5080');
  });

  it('should return localhost when env is undefined string', () => {
    process.env.NEXT_PUBLIC_API_BASE = 'undefined';
    expect(getApiBase()).toBe('http://localhost:5080');
  });

  it('should return localhost when env is null string', () => {
    process.env.NEXT_PUBLIC_API_BASE = 'null';
    expect(getApiBase()).toBe('http://localhost:5080');
  });

  it('should trim whitespace from env variable', () => {
    process.env.NEXT_PUBLIC_API_BASE = '  https://api.meepleai.dev  ';
    expect(getApiBase()).toBe('https://api.meepleai.dev');
  });
});

describe('HttpClient', () => {
  let mockFetch: jest.Mock;
  let client: HttpClient;
  let mockLocalStorage: { [key: string]: string };
  let mockSessionStorage: { [key: string]: string };

  beforeEach(() => {
    mockFetch = jest.fn();
    client = new HttpClient({ baseUrl: 'http://localhost:5080', fetchImpl: mockFetch });

    // Mock browser storage
    mockLocalStorage = {};
    mockSessionStorage = {};

    Object.defineProperty(window, 'localStorage', {
      value: {
        getItem: jest.fn((key: string) => mockLocalStorage[key] || null),
        setItem: jest.fn((key: string, value: string) => {
          mockLocalStorage[key] = value;
        }),
      },
      writable: true,
    });

    Object.defineProperty(window, 'sessionStorage', {
      value: {
        getItem: jest.fn((key: string) => mockSessionStorage[key] || null),
        setItem: jest.fn((key: string, value: string) => {
          mockSessionStorage[key] = value;
        }),
        removeItem: jest.fn((key: string) => {
          delete mockSessionStorage[key];
        }),
        clear: jest.fn(() => {
          mockSessionStorage = {};
        }),
      },
      writable: true,
    });
    if (typeof URL.createObjectURL !== 'function') {
      // @ts-expect-error - JSDOM does not implement these helpers
      URL.createObjectURL = () => 'blob:mock';
    }
    if (typeof URL.revokeObjectURL !== 'function') {
      // @ts-expect-error - JSDOM does not implement these helpers
      URL.revokeObjectURL = () => {};
    }

    // Mock crypto.randomUUID
    Object.defineProperty(global, 'crypto', {
      value: {
        randomUUID: jest.fn(() => 'test-uuid-123'),
      },
      writable: true,
    });
  });

  describe('GET requests', () => {
    it('should make GET request with correct headers', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ data: 'test' }),
        headers: new Headers(),
      });

      await client.get('/api/v1/test');

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5080/api/v1/test',
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
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        headers: new Headers(),
      });

      const result = await client.get('/api/v1/test');

      expect(result).toBeNull();
    });

    it('should validate response with Zod schema', async () => {
      const schema = z.object({ id: z.string(), name: z.string() });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ id: '123', name: 'Test' }),
        headers: new Headers(),
      });

      const result = await client.get('/api/v1/test', schema);

      expect(result).toEqual({ id: '123', name: 'Test' });
    });

    it('should throw SchemaValidationError on invalid response', async () => {
      const schema = z.object({ id: z.string(), name: z.string() });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ id: 123 }), // Invalid: missing name, wrong id type
        headers: new Headers(),
      });

      await expect(client.get('/api/v1/test', schema)).rejects.toThrow(
        SchemaValidationError
      );
    });

    it('should include Authorization header when an API key is stored', async () => {
      setStoredApiKey('mpl_test_demo');

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ ok: true }),
        headers: new Headers(),
      });

      await client.get('/api/v1/test');

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5080/api/v1/test',
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: 'ApiKey mpl_test_demo',
          }),
        })
      );

      clearStoredApiKey();
    });

    it('should throw error for non-401 failures', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 404,
        json: async () => ({ error: 'Not found' }),
        headers: new Headers({ 'X-Correlation-Id': 'test-404' }),
      });

      await expect(client.get('/api/v1/test')).rejects.toThrow(NotFoundError);
    });
  });

  describe('POST requests', () => {
    it('should make POST request with JSON body', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ success: true }),
        headers: new Headers(),
      });

      await client.post('/api/v1/test', { key: 'value' });

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5080/api/v1/test',
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
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({ error: 'Unauthorized' }),
        headers: new Headers(),
      });

      await expect(client.post('/api/v1/test', {})).rejects.toThrow(
        UnauthorizedError
      );
    });

    it('should handle 204 No Content responses', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 204,
        headers: new Headers(),
      });

      const result = await client.post('/api/v1/test', {});

      expect(result).toBeUndefined();
    });

    it('should validate response with Zod schema', async () => {
      const schema = z.object({ success: z.boolean() });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ success: true }),
        headers: new Headers(),
      });

      const result = await client.post('/api/v1/test', {}, schema);

      expect(result).toEqual({ success: true });
    });

    it('should handle empty body', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ success: true }),
        headers: new Headers(),
      });

      await client.post('/api/v1/test');

      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          body: JSON.stringify({}),
        })
      );
    });
  });

  describe('PUT requests', () => {
    it('should make PUT request with JSON body', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ updated: true }),
        headers: new Headers(),
      });

      await client.put('/api/v1/test', { key: 'updated' });

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5080/api/v1/test',
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
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({ error: 'Unauthorized' }),
        headers: new Headers(),
      });

      await expect(client.put('/api/v1/test', {})).rejects.toThrow(
        UnauthorizedError
      );
    });

    it('should validate response with Zod schema', async () => {
      const schema = z.object({ updated: z.boolean() });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ updated: true }),
        headers: new Headers(),
      });

      const result = await client.put('/api/v1/test', {}, schema);

      expect(result).toEqual({ updated: true });
    });
  });

  describe('DELETE requests', () => {
    it('should make DELETE request', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 204,
        headers: new Headers(),
      });

      await client.delete('/api/v1/test');

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:5080/api/v1/test',
        expect.objectContaining({
          method: 'DELETE',
          credentials: 'include',
        })
      );
    });

    it('should throw error for 401 responses', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({ error: 'Unauthorized' }),
        headers: new Headers(),
      });

      await expect(client.delete('/api/v1/test')).rejects.toThrow(
        UnauthorizedError
      );
    });

    it('should not attempt to parse response body', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 204,
        headers: new Headers(),
      });

      const result = await client.delete('/api/v1/test');

      expect(result).toBeUndefined();
    });
  });

  describe('postFile (file downloads)', () => {
    it('should handle file download with blob response', async () => {
      const blob = new Blob(['test content'], { type: 'application/json' });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        blob: async () => blob,
        headers: new Headers({
          'Content-Disposition': 'attachment; filename="export.json"',
        }),
      });

      const result = await client.postFile('/api/v1/export', { format: 'json' });

      expect(result.blob).toBe(blob);
      expect(result.filename).toBe('export.json');
    });

    it('should extract filename from Content-Disposition header', async () => {
      const blob = new Blob(['test'], { type: 'text/plain' });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        blob: async () => blob,
        headers: new Headers({
          'Content-Disposition': 'attachment; filename="test-file.txt"',
        }),
      });

      const result = await client.postFile('/api/v1/export', {});

      expect(result.filename).toBe('test-file.txt');
    });

    it('should use default filename if header missing', async () => {
      const blob = new Blob(['test'], { type: 'text/plain' });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        blob: async () => blob,
        headers: new Headers(),
      });

      const result = await client.postFile('/api/v1/export', {});

      expect(result.filename).toMatch(/^download-\d+$/);
    });

    it('should throw error for 401 responses', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({ error: 'Unauthorized' }),
        headers: new Headers(),
      });

      await expect(client.postFile('/api/v1/export', {})).rejects.toThrow(
        UnauthorizedError
      );
    });
  });

  describe('error handling', () => {
    it('should skip error logging when requested', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation();

      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Server error' }),
        headers: new Headers(),
      });

      await expect(
        client.get('/api/v1/test', undefined, { skipErrorLogging: true })
      ).rejects.toThrow(ServerError);

      // Logger should not be called
      expect(consoleErrorSpy).not.toHaveBeenCalled();

      consoleErrorSpy.mockRestore();
    });

    it('should extract correlation ID from response headers', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Server error' }),
        headers: new Headers({ 'X-Correlation-Id': 'test-correlation-xyz' }),
      });

      try {
        await client.get('/api/v1/test');
      } catch (error) {
        expect((error as ServerError).correlationId).toBe('test-correlation-xyz');
      }
    });
  });

  describe('correlation ID management', () => {
    it('should generate correlation ID on first request', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({}),
        headers: new Headers(),
      });

      await client.get('/api/v1/test');

      expect(mockSessionStorage['correlation_id']).toBe('test-uuid-123');
    });

    it('should reuse existing correlation ID', async () => {
      mockSessionStorage['correlation_id'] = 'existing-correlation-id';

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({}),
        headers: new Headers(),
      });

      await client.get('/api/v1/test');

      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          headers: expect.objectContaining({
            'X-Correlation-ID': 'existing-correlation-id',
          }),
        })
      );
    });
  });
});

describe('downloadFile', () => {
  let mockCreateElement: jest.SpyInstance;
  let mockAppendChild: jest.SpyInstance;
  let mockRemoveChild: jest.SpyInstance;
  let mockCreateObjectURL: jest.SpyInstance;
  let mockRevokeObjectURL: jest.SpyInstance;
  let mockLink: { href: string; download: string; click: jest.Mock };

  beforeEach(() => {
    mockLink = {
      href: '',
      download: '',
      click: jest.fn(),
    };

    mockCreateElement = jest.spyOn(document, 'createElement').mockReturnValue(mockLink as any);
    mockAppendChild = jest.spyOn(document.body, 'appendChild').mockImplementation();
    mockRemoveChild = jest.spyOn(document.body, 'removeChild').mockImplementation();
    mockCreateObjectURL = jest.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test-url');
    mockRevokeObjectURL = jest.spyOn(URL, 'revokeObjectURL').mockImplementation();
  });

  afterEach(() => {
    mockCreateElement.mockRestore();
    mockAppendChild.mockRestore();
    mockRemoveChild.mockRestore();
    mockCreateObjectURL.mockRestore();
    mockRevokeObjectURL.mockRestore();
  });

  it('should trigger browser download', () => {
    const blob = new Blob(['test content'], { type: 'text/plain' });

    downloadFile(blob, 'test.txt');

    expect(mockCreateObjectURL).toHaveBeenCalledWith(blob);
    expect(mockLink.href).toBe('blob:test-url');
    expect(mockLink.download).toBe('test.txt');
    expect(mockAppendChild).toHaveBeenCalledWith(mockLink);
    expect(mockLink.click).toHaveBeenCalled();
    expect(mockRemoveChild).toHaveBeenCalledWith(mockLink);
    expect(mockRevokeObjectURL).toHaveBeenCalledWith('blob:test-url');
  });
});
