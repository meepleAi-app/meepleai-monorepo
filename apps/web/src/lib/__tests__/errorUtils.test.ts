/**
 * Unit tests for error categorization utilities (PDF-06)
 */

import {
  categorizeError,
  ErrorCategory,
  extractCorrelationId,
  formatErrorMessage,
  getErrorIcon,
  getErrorTitle
} from '../errorUtils';
import { ApiError } from '../api';

describe('errorUtils', () => {
  describe('extractCorrelationId', () => {
    it('should extract correlation ID from response headers', () => {
      const response = new Response(null, {
        headers: { 'X-Correlation-Id': 'test-correlation-id-123' }
      });

      const correlationId = extractCorrelationId(response);

      expect(correlationId).toBe('test-correlation-id-123');
    });

    it('should return undefined when correlation ID header is missing', () => {
      const response = new Response(null, { headers: {} });

      const correlationId = extractCorrelationId(response);

      expect(correlationId).toBeUndefined();
    });
  });

  describe('categorizeError', () => {
    describe('Network errors', () => {
      it('should categorize network fetch errors', () => {
        const error = new TypeError('fetch failed');

        const result = categorizeError(error);

        expect(result.category).toBe(ErrorCategory.Network);
        expect(result.message).toContain('Connection lost');
        expect(result.canRetry).toBe(true);
        expect(result.suggestions.length).toBeGreaterThan(0);
      });
    });

    describe('Validation errors (400)', () => {
      it('should categorize 400 Bad Request errors', () => {
        const response = new Response(null, { status: 400 });
        const error = new Error('Bad request');

        const result = categorizeError(error, response);

        expect(result.category).toBe(ErrorCategory.Validation);
        expect(result.statusCode).toBe(400);
        expect(result.canRetry).toBe(false);
        expect(result.suggestions.length).toBeGreaterThan(0);
      });
    });

    describe('Forbidden errors (403)', () => {
      it('should categorize 403 Forbidden errors', () => {
        const response = new Response(null, { status: 403 });
        const error = new Error('Forbidden');

        const result = categorizeError(error, response);

        expect(result.category).toBe(ErrorCategory.Validation);
        expect(result.message).toContain('permission');
        expect(result.statusCode).toBe(403);
        expect(result.canRetry).toBe(false);
      });
    });

    describe('Not Found errors (404)', () => {
      it('should categorize 404 Not Found errors', () => {
        const response = new Response(null, { status: 404 });
        const error = new Error('Not found');

        const result = categorizeError(error, response);

        expect(result.category).toBe(ErrorCategory.Validation);
        expect(result.message).toContain('not found');
        expect(result.statusCode).toBe(404);
        expect(result.canRetry).toBe(false);
      });
    });

    describe('Server errors (5xx)', () => {
      it('should categorize 500 Internal Server Error', () => {
        const response = new Response(null, { status: 500 });
        const error = new Error('Internal server error');

        const result = categorizeError(error, response);

        expect(result.category).toBe(ErrorCategory.Server);
        expect(result.message).toContain('Server error');
        expect(result.statusCode).toBe(500);
        expect(result.canRetry).toBe(true);
      });

      it('should categorize 502 Bad Gateway', () => {
        const response = new Response(null, { status: 502 });
        const error = new Error('Bad gateway');

        const result = categorizeError(error, response);

        expect(result.category).toBe(ErrorCategory.Server);
        expect(result.canRetry).toBe(true);
      });

      it('should categorize 503 Service Unavailable', () => {
        const response = new Response(null, { status: 503 });
        const error = new Error('Service unavailable');

        const result = categorizeError(error, response);

        expect(result.category).toBe(ErrorCategory.Server);
        expect(result.canRetry).toBe(true);
      });
    });

    describe('Timeout errors (408)', () => {
      it('should categorize 408 Request Timeout', () => {
        const response = new Response(null, { status: 408 });
        const error = new Error('Timeout');

        const result = categorizeError(error, response);

        expect(result.category).toBe(ErrorCategory.Network);
        expect(result.message).toContain('timed out');
        expect(result.statusCode).toBe(408);
        expect(result.canRetry).toBe(true);
      });
    });

    describe('Rate limit errors (429)', () => {
      it('should categorize 429 Too Many Requests', () => {
        const response = new Response(null, { status: 429 });
        const error = new Error('Too many requests');

        const result = categorizeError(error, response);

        expect(result.category).toBe(ErrorCategory.Server);
        expect(result.message).toContain('Too many requests');
        expect(result.statusCode).toBe(429);
        expect(result.canRetry).toBe(true);
      });
    });

    describe('Processing errors', () => {
      it('should categorize corrupted file errors', () => {
        const error = new Error('File is corrupted and cannot be processed');

        const result = categorizeError(error);

        expect(result.category).toBe(ErrorCategory.Processing);
        expect(result.message).toContain('corrupted');
        expect(result.canRetry).toBe(false);
      });

      it('should categorize invalid PDF format errors', () => {
        const error = new Error('Invalid PDF format detected');

        const result = categorizeError(error);

        expect(result.category).toBe(ErrorCategory.Processing);
        expect(result.message).toContain('format');
        expect(result.canRetry).toBe(false);
      });

      it('should categorize file size errors', () => {
        const error = new Error('File size exceeds maximum limit');

        const result = categorizeError(error);

        expect(result.category).toBe(ErrorCategory.Validation);
        expect(result.message).toContain('too large');
        expect(result.canRetry).toBe(false);
      });

      it('should categorize parsing errors', () => {
        const error = new Error('PDF parsing failed');

        const result = categorizeError(error);

        expect(result.category).toBe(ErrorCategory.Processing);
        expect(result.message).toContain('extract text');
        expect(result.canRetry).toBe(true);
      });
    });

    describe('Correlation ID tracking', () => {
      it('should include correlation ID in categorized error', () => {
        const response = new Response(null, {
          status: 500,
          headers: { 'X-Correlation-Id': 'abc-123-xyz' }
        });
        const error = new Error('Server error');
        const correlationId = extractCorrelationId(response);

        const result = categorizeError(error, response, correlationId);

        expect(result.correlationId).toBe('abc-123-xyz');
      });

      it('should work without correlation ID', () => {
        const error = new Error('Some error');

        const result = categorizeError(error);

        expect(result.correlationId).toBeUndefined();
      });
    });

    describe('Unknown errors', () => {
      it('should categorize unknown errors', () => {
        const error = new Error('Something unexpected happened');

        const result = categorizeError(error);

        expect(result.category).toBe(ErrorCategory.Unknown);
        expect(result.message).toContain('unexpected');
        expect(result.canRetry).toBe(true);
      });

      it('should handle non-Error objects', () => {
        const error = 'String error';

        const result = categorizeError(error);

        expect(result.category).toBe(ErrorCategory.Unknown);
        expect(result.technicalMessage).toBe('String error');
      });
    });
  });

  describe('formatErrorMessage', () => {
    it('should format error message with correlation ID', () => {
      const categorizedError = {
        category: ErrorCategory.Server,
        message: 'Server error occurred',
        correlationId: 'test-id-123',
        canRetry: true,
        suggestions: []
      };

      const formatted = formatErrorMessage(categorizedError);

      expect(formatted).toContain('Server error occurred');
      expect(formatted).toContain('Error ID: test-id-123');
    });

    it('should format error message without correlation ID', () => {
      const categorizedError = {
        category: ErrorCategory.Network,
        message: 'Network error occurred',
        canRetry: true,
        suggestions: []
      };

      const formatted = formatErrorMessage(categorizedError);

      expect(formatted).toBe('Network error occurred');
      expect(formatted).not.toContain('Error ID');
    });
  });

  describe('getErrorIcon', () => {
    it('should return correct icon for each category', () => {
      expect(getErrorIcon(ErrorCategory.Validation)).toBe('❌');
      expect(getErrorIcon(ErrorCategory.Network)).toBe('⚠️');
      expect(getErrorIcon(ErrorCategory.Server)).toBe('❌');
      expect(getErrorIcon(ErrorCategory.Processing)).toBe('❌');
      expect(getErrorIcon(ErrorCategory.Unknown)).toBe('⚠️');
    });
  });

  describe('getErrorTitle', () => {
    it('should return correct title for each category', () => {
      expect(getErrorTitle(ErrorCategory.Validation)).toBe('Invalid File');
      expect(getErrorTitle(ErrorCategory.Network)).toBe('Connection Lost');
      expect(getErrorTitle(ErrorCategory.Server)).toBe('Upload Failed');
      expect(getErrorTitle(ErrorCategory.Processing)).toBe('Unable to Process PDF');
      expect(getErrorTitle(ErrorCategory.Unknown)).toBe('Unexpected Error');
    });
  });

  describe('Integration with ApiError', () => {
    it('should work with ApiError instances', () => {
      const response = new Response(null, {
        status: 500,
        headers: { 'X-Correlation-Id': 'api-error-123' }
      });
      const apiError = new ApiError('API request failed', 500, 'api-error-123', response);

      const result = categorizeError(apiError, response, apiError.correlationId);

      expect(result.category).toBe(ErrorCategory.Server);
      expect(result.correlationId).toBe('api-error-123');
      expect(result.statusCode).toBe(500);
      expect(result.canRetry).toBe(true);
    });
  });
});
