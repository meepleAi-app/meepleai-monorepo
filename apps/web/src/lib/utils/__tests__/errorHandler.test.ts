/**
 * Tests for Error Handler Utilities (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: Type-safe error processing, message extraction, error details
 */

import { ApiError } from '@/lib/api';
import { getErrorMessage, getErrorDetails } from '../errorHandler';

describe('errorHandler utilities', () => {
  describe('getErrorMessage', () => {
    it('should extract message from ApiError', () => {
      const apiError = new ApiError({
        message: 'API failed',
        statusCode: 500,
        endpoint: '/api/v1/test',
      });
      const result = getErrorMessage(apiError);
      expect(result).toBe('API failed');
    });

    it('should extract message from Error', () => {
      const error = new Error('Something broke');
      const result = getErrorMessage(error);
      expect(result).toBe('Something broke');
    });

    it('should handle string errors', () => {
      const result = getErrorMessage('String error message');
      expect(result).toBe('String error message');
    });

    it('should use fallback for unknown types', () => {
      const result = getErrorMessage(null);
      expect(result).toBe('An unexpected error occurred');
    });

    it('should use custom fallback message', () => {
      const result = getErrorMessage(undefined, 'Custom fallback');
      expect(result).toBe('Custom fallback');
    });

    it('should handle number errors', () => {
      const result = getErrorMessage(404);
      expect(result).toBe('An unexpected error occurred');
    });

    it('should handle object errors', () => {
      const result = getErrorMessage({ code: 'ERR_001' });
      expect(result).toBe('An unexpected error occurred');
    });
  });

  describe('getErrorDetails', () => {
    it('should extract full details from ApiError', () => {
      const apiError = new ApiError({
        message: 'API failed',
        statusCode: 500,
        endpoint: '/api/v1/test',
        correlationId: 'corr-123',
      });
      const result = getErrorDetails(apiError);

      expect(result).toEqual({
        message: 'API failed',
        statusCode: 500,
        correlationId: 'corr-123',
      });
    });

    it('should handle ApiError without correlationId', () => {
      const apiError = new ApiError({
        message: 'API failed',
        statusCode: 404,
        endpoint: '/api/v1/test',
      });
      const result = getErrorDetails(apiError);

      expect(result).toEqual({
        message: 'API failed',
        statusCode: 404,
        correlationId: undefined,
      });
    });

    it('should extract message from Error', () => {
      const error = new Error('Generic error');
      const result = getErrorDetails(error);

      expect(result).toEqual({
        message: 'Generic error',
      });
    });

    it('should handle string errors', () => {
      const result = getErrorDetails('String error');

      expect(result).toEqual({
        message: 'String error',
      });
    });

    it('should handle null errors', () => {
      const result = getErrorDetails(null);

      expect(result).toEqual({
        message: 'null',
      });
    });

    it('should handle undefined errors', () => {
      const result = getErrorDetails(undefined);

      expect(result).toEqual({
        message: 'undefined',
      });
    });

    it('should handle object errors', () => {
      const result = getErrorDetails({ code: 'ERR_001', details: 'Something' });

      expect(result).toEqual({
        message: '[object Object]',
      });
    });
  });
});
