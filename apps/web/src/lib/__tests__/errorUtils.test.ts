/**
 * Tests for Error Utilities (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: Error categorization, message formatting, correlation ID extraction
 */

import {
  categorizeError,
  extractCorrelationId,
  formatErrorMessage,
  getErrorIcon,
  getErrorTitle,
  ErrorCategory,
  type CategorizedError,
} from '../errorUtils';

describe('errorUtils', () => {
  const mockCorrelationId = 'test-correlation-123';

  describe('extractCorrelationId', () => {
    it('should extract correlation ID from headers', () => {
      const mockResponse = {
        headers: new Headers({ 'X-Correlation-Id': mockCorrelationId }),
      } as Response;

      const result = extractCorrelationId(mockResponse);
      expect(result).toBe(mockCorrelationId);
    });

    it('should return undefined when header missing', () => {
      const mockResponse = {
        headers: new Headers(),
      } as Response;

      const result = extractCorrelationId(mockResponse);
      expect(result).toBeUndefined();
    });
  });

  describe('categorizeError', () => {
    it('should categorize network fetch errors', () => {
      const error = new TypeError('fetch failed');

      const result = categorizeError(error);

      expect(result).toMatchObject({
        category: ErrorCategory.Network,
        message: expect.stringContaining('Connection lost'),
        technicalMessage: 'fetch failed',
        canRetry: true,
        suggestions: expect.arrayContaining([expect.stringContaining('internet')]),
      });
    });

    it('should categorize 400 validation errors', () => {
      const error = new Error('Invalid input');
      const mockResponse = { status: 400 } as Response;

      const result = categorizeError(error, mockResponse, mockCorrelationId);

      expect(result).toMatchObject({
        category: ErrorCategory.Validation,
        message: expect.stringContaining('Invalid file'),
        statusCode: 400,
        correlationId: mockCorrelationId,
        canRetry: false,
        suggestions: expect.arrayContaining([expect.stringContaining('file size')]),
      });
    });

    it('should categorize 403 forbidden errors', () => {
      const error = new Error('Forbidden');
      const mockResponse = { status: 403 } as Response;

      const result = categorizeError(error, mockResponse);

      expect(result).toMatchObject({
        category: ErrorCategory.Validation,
        message: expect.stringContaining('permission'),
        statusCode: 403,
        canRetry: false,
        suggestions: expect.arrayContaining([expect.stringContaining('administrator')]),
      });
    });

    it('should categorize 404 not found errors', () => {
      const error = new Error('Not found');
      const mockResponse = { status: 404 } as Response;

      const result = categorizeError(error, mockResponse);

      expect(result).toMatchObject({
        category: ErrorCategory.Validation,
        message: expect.stringContaining('not found'),
        statusCode: 404,
        canRetry: false,
      });
    });

    it('should categorize 408 timeout errors', () => {
      const error = new Error('Timeout');
      const mockResponse = { status: 408 } as Response;

      const result = categorizeError(error, mockResponse);

      expect(result).toMatchObject({
        category: ErrorCategory.Network,
        message: expect.stringContaining('timed out'),
        statusCode: 408,
        canRetry: true,
      });
    });

    it('should categorize 429 rate limit errors', () => {
      const error = new Error('Rate limit');
      const mockResponse = { status: 429 } as Response;

      const result = categorizeError(error, mockResponse);

      expect(result).toMatchObject({
        category: ErrorCategory.Server,
        message: expect.stringContaining('Too many requests'),
        statusCode: 429,
        canRetry: true,
      });
    });

    it('should categorize 500 server errors', () => {
      const error = new Error('Internal server error');
      const mockResponse = { status: 500 } as Response;

      const result = categorizeError(error, mockResponse, mockCorrelationId);

      expect(result).toMatchObject({
        category: ErrorCategory.Server,
        message: expect.stringContaining('Server error'),
        statusCode: 500,
        correlationId: mockCorrelationId,
        canRetry: true,
        suggestions: expect.arrayContaining([expect.stringContaining('Wait')]),
      });
    });

    it('should categorize 502/503/504 server errors', () => {
      const error502 = new Error('Bad gateway');
      const response502 = { status: 502 } as Response;
      const result502 = categorizeError(error502, response502);
      expect(result502.category).toBe(ErrorCategory.Server);
      expect(result502.canRetry).toBe(true);

      const error503 = new Error('Service unavailable');
      const response503 = { status: 503 } as Response;
      const result503 = categorizeError(error503, response503);
      expect(result503.category).toBe(ErrorCategory.Server);

      const error504 = new Error('Gateway timeout');
      const response504 = { status: 504 } as Response;
      const result504 = categorizeError(error504, response504);
      expect(result504.category).toBe(ErrorCategory.Server);
    });

    it('should categorize corrupted PDF errors', () => {
      const error = new Error('File is corrupted');

      const result = categorizeError(error);

      expect(result).toMatchObject({
        category: ErrorCategory.Processing,
        message: expect.stringContaining('corrupted'),
        canRetry: false,
        suggestions: expect.arrayContaining([expect.stringContaining('Re-download')]),
      });
    });

    it('should categorize invalid PDF format errors', () => {
      const error = new Error('Invalid PDF format');

      const result = categorizeError(error);

      expect(result).toMatchObject({
        category: ErrorCategory.Processing,
        message: expect.stringContaining('corrupted or in an unsupported format'),
        canRetry: false,
      });
    });

    it('should categorize file too large errors', () => {
      const error = new Error('File too large');

      const result = categorizeError(error);

      expect(result).toMatchObject({
        category: ErrorCategory.Validation,
        message: expect.stringContaining('too large'),
        canRetry: false,
        suggestions: expect.arrayContaining([expect.stringContaining('Compress')]),
      });
    });

    it('should categorize size exceeded errors', () => {
      const error = new Error('Size exceeds maximum');

      const result = categorizeError(error);

      expect(result.category).toBe(ErrorCategory.Validation);
      expect(result.canRetry).toBe(false);
    });

    it('should categorize parsing errors', () => {
      const error = new Error('Parsing failed');

      const result = categorizeError(error);

      expect(result).toMatchObject({
        category: ErrorCategory.Processing,
        message: expect.stringContaining('extract text'),
        canRetry: true,
      });
    });

    it('should categorize extraction errors', () => {
      const error = new Error('Text extraction failed');

      const result = categorizeError(error);

      expect(result.category).toBe(ErrorCategory.Processing);
      expect(result.canRetry).toBe(true);
    });

    it('should categorize unknown errors as default', () => {
      const error = new Error('Something weird happened');

      const result = categorizeError(error);

      expect(result).toMatchObject({
        category: ErrorCategory.Unknown,
        message: expect.stringContaining('unexpected error'),
        canRetry: true,
        suggestions: expect.arrayContaining([expect.stringContaining('Try again')]),
      });
    });

    it('should handle string errors', () => {
      const result = categorizeError('String error message');

      expect(result.category).toBe(ErrorCategory.Unknown);
      expect(result.technicalMessage).toBe('String error message');
    });

    it('should handle null/undefined errors', () => {
      const resultNull = categorizeError(null);
      expect(resultNull.category).toBe(ErrorCategory.Unknown);

      const resultUndefined = categorizeError(undefined);
      expect(resultUndefined.category).toBe(ErrorCategory.Unknown);
    });
  });

  describe('formatErrorMessage', () => {
    it('should format message without correlation ID', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Validation,
        message: 'Invalid file',
        canRetry: false,
        suggestions: [],
      };

      const result = formatErrorMessage(error);

      expect(result).toBe('Invalid file');
    });

    it('should format message with correlation ID', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Server,
        message: 'Server error',
        correlationId: mockCorrelationId,
        canRetry: true,
        suggestions: [],
      };

      const result = formatErrorMessage(error);

      expect(result).toContain('Server error');
      expect(result).toContain(`Error ID: ${mockCorrelationId}`);
      expect(result).toContain('for support');
    });
  });

  describe('getErrorIcon', () => {
    it('should return correct icons for each category', () => {
      expect(getErrorIcon(ErrorCategory.Validation)).toBe('❌');
      expect(getErrorIcon(ErrorCategory.Network)).toBe('⚠️');
      expect(getErrorIcon(ErrorCategory.Server)).toBe('❌');
      expect(getErrorIcon(ErrorCategory.Processing)).toBe('❌');
      expect(getErrorIcon(ErrorCategory.Unknown)).toBe('⚠️');
    });

    it('should return default icon for unknown category', () => {
      const result = getErrorIcon('invalid-category' as ErrorCategory);
      expect(result).toBe('❌');
    });
  });

  describe('getErrorTitle', () => {
    it('should return correct titles for each category', () => {
      expect(getErrorTitle(ErrorCategory.Validation)).toBe('Invalid File');
      expect(getErrorTitle(ErrorCategory.Network)).toBe('Connection Lost');
      expect(getErrorTitle(ErrorCategory.Server)).toBe('Upload Failed');
      expect(getErrorTitle(ErrorCategory.Processing)).toBe('Unable to Process PDF');
      expect(getErrorTitle(ErrorCategory.Unknown)).toBe('Unexpected Error');
    });

    it('should return default title for unknown category', () => {
      const result = getErrorTitle('invalid-category' as ErrorCategory);
      expect(result).toBe('Error');
    });
  });
});
