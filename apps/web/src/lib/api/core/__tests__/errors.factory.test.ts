/**
 * Errors - Error Classes toJSON Tests (Issue #2309)
 *
 * Coverage gap: 60.86% → 75%+
 * Tests: toJSON serialization for error classes
 */

import { ForbiddenError, ValidationError, RateLimitError } from '../errors';

describe('errors - Classes (Issue #2309)', () => {
  describe('Error Class toJSON', () => {
    it('should serialize ForbiddenError to JSON', () => {
      const error = new ForbiddenError({
        message: 'Access denied',
        correlationId: 'corr-123',
        endpoint: '/api/v1/admin',
      });

      const json = error.toJSON();

      expect(json.name).toBe('ForbiddenError');
      expect(json.statusCode).toBe(403);
      expect(json.correlationId).toBe('corr-123');
    });

    it('should serialize ValidationError with validationErrors', () => {
      const error = new ValidationError({
        message: 'Validation failed',
        validationErrors: { field1: ['error1'] },
      });

      const json = error.toJSON();

      expect(json.name).toBe('ValidationError');
      expect(json.statusCode).toBe(422);
      expect((json as any).validationErrors).toEqual({ field1: ['error1'] });
    });

    it('should serialize RateLimitError with retryAfter', () => {
      const error = new RateLimitError({
        message: 'Rate limited',
        retryAfter: 60000,
      });

      const json = error.toJSON();

      expect(json.name).toBe('RateLimitError');
      expect(json.statusCode).toBe(429);
      expect((json as any).retryAfter).toBe(60000);
    });
  });
});
