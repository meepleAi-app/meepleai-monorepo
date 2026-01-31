/**
 * Unit tests for API contract types
 * Tests type utilities, error handling, and helper functions
 */

import { ApiError, createApiError } from '../api';

describe('API Types', () => {
  describe('ApiError', () => {
    it('should create ApiError with message', () => {
      const error = new ApiError('Test error');

      expect(error).toBeInstanceOf(Error);
      expect(error.name).toBe('ApiError');
      expect(error.message).toBe('Test error');
      expect(error.statusCode).toBeUndefined();
      expect(error.correlationId).toBeUndefined();
    });

    it('should create ApiError with status code', () => {
      const error = new ApiError('Not found', 404);

      expect(error.message).toBe('Not found');
      expect(error.statusCode).toBe(404);
      expect(error.correlationId).toBeUndefined();
    });

    it('should create ApiError with correlation ID', () => {
      const error = new ApiError('Server error', 500, 'abc-123');

      expect(error.message).toBe('Server error');
      expect(error.statusCode).toBe(500);
      expect(error.correlationId).toBe('abc-123');
    });

    it('should create ApiError with response object', () => {
      const mockResponse = new Response(null, { status: 400 });
      const error = new ApiError('Bad request', 400, 'def-456', mockResponse);

      expect(error.message).toBe('Bad request');
      expect(error.statusCode).toBe(400);
      expect(error.correlationId).toBe('def-456');
      expect(error.response).toBe(mockResponse);
    });

    it('should inherit from Error prototype', () => {
      const error = new ApiError('Test');

      expect(error.toString()).toContain('ApiError');
      expect(error.stack).toBeDefined();
    });
  });

  describe('createApiError', () => {
    it('should create ApiError from response with correlation ID', async () => {
      const mockHeaders = {
        get: vi.fn((header: string) =>
          header === 'X-Correlation-Id' ? 'test-correlation-id' : null
        ),
      };

      const mockResponse = {
        status: 404,
        headers: mockHeaders,
        json: async () => ({ error: 'Custom error message' }),
      } as unknown as Response;

      const error = await createApiError('/api/test', mockResponse);

      expect(error).toBeInstanceOf(ApiError);
      expect(error.message).toBe('Custom error message');
      expect(error.statusCode).toBe(404);
      expect(error.correlationId).toBe('test-correlation-id');
      expect(error.response).toBe(mockResponse);
    });

    it('should handle response without correlation ID', async () => {
      const mockResponse = new Response(JSON.stringify({ error: 'No correlation' }), {
        status: 500,
      });

      const error = await createApiError('/api/fail', mockResponse);

      expect(error.message).toBe('No correlation');
      expect(error.statusCode).toBe(500);
      expect(error.correlationId).toBeUndefined();
    });

    it('should use default message when JSON parsing fails', async () => {
      const mockResponse = new Response('Invalid JSON', { status: 500 });

      const error = await createApiError('/api/broken', mockResponse);

      expect(error.message).toBe('API /api/broken 500');
      expect(error.statusCode).toBe(500);
    });

    it('should handle response without error property in body', async () => {
      const mockResponse = new Response(JSON.stringify({ data: 'something else' }), {
        status: 400,
      });

      const error = await createApiError('/api/bad', mockResponse);

      expect(error.message).toBe('API /api/bad 400');
      expect(error.statusCode).toBe(400);
    });

    it('should handle response with no body', async () => {
      const mockResponse = new Response(null, { status: 204 });

      const error = await createApiError('/api/empty', mockResponse);

      expect(error.message).toBe('API /api/empty 204');
      expect(error.statusCode).toBe(204);
    });

    it('should handle response without headers.get method', async () => {
      const mockResponse = {
        status: 500,
        headers: null,
        json: async () => ({ error: 'No headers object' }),
      } as unknown as Response;

      const error = await createApiError('/api/no-headers', mockResponse);

      expect(error.message).toBe('No headers object');
      expect(error.statusCode).toBe(500);
      expect(error.correlationId).toBeUndefined();
    });

    it('should default to 500 if status is not a number', async () => {
      const mockResponse = {
        status: 'invalid' as unknown,
        headers: { get: () => null },
        json: async () => ({ error: 'Invalid status' }),
      } as unknown as Response;

      const error = await createApiError('/api/invalid', mockResponse);

      expect(error.statusCode).toBe(500);
    });

    it('should handle empty error message in response', async () => {
      const mockResponse = new Response(JSON.stringify({ error: '' }), { status: 400 });

      const error = await createApiError('/api/empty-error', mockResponse);

      // Empty error string is falsy, so should use default message
      expect(error.message).toBe('API /api/empty-error 400');
    });

    it('should preserve original response object', async () => {
      const mockResponse = new Response(JSON.stringify({ error: 'Test' }), { status: 403 });

      const error = await createApiError('/api/preserve', mockResponse);

      expect(error.response).toBe(mockResponse);
      expect(error.response?.status).toBe(403);
    });
  });

  describe('Type Exports', () => {
    it('should export all required types', () => {
      // This test ensures all types are properly exported and accessible
      // TypeScript will fail compilation if types are missing

      const chatMessageResponse: import('../api').ChatMessageResponse = {
        id: '1',
        chatId: '2',
        userId: null,
        level: 'user',
        content: 'test',
        sequenceNumber: 1,
        createdAt: '2024-01-01',
        updatedAt: null,
        isDeleted: false,
        deletedAt: null,
        deletedByUserId: null,
        isInvalidated: false,
        metadataJson: null,
      };

      const exportFormat: import('../api').ExportFormat = 'pdf';

      const cacheStats: import('../api').CacheStats = {
        totalHits: 100,
        totalMisses: 10,
        hitRate: 0.9,
        totalKeys: 50,
        cacheSizeBytes: 1024,
        topQuestions: [],
      };

      const validationResult: import('../api').ValidationResult = {
        valid: true,
        errors: [],
      };

      const apiResponse: import('../api').ApiResponse<string> = {
        data: 'test',
      };

      const paginatedResponse: import('../api').PaginatedResponse<number> = {
        items: [1, 2, 3],
        total: 3,
        page: 1,
        pageSize: 10,
        hasMore: false,
      };

      // If we reach here, all types are properly defined
      expect(chatMessageResponse).toBeDefined();
      expect(exportFormat).toBeDefined();
      expect(cacheStats).toBeDefined();
      expect(validationResult).toBeDefined();
      expect(apiResponse).toBeDefined();
      expect(paginatedResponse).toBeDefined();
    });
  });
});
