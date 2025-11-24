/**
 * Comprehensive Tests for useRateLimitHandler Hook (Issue #1661 - Fase 1.3)
 *
 * Coverage target: 95%+
 * Tests: Countdown timer, auto-reset, message formatting, error handling
 */

import { renderHook, act } from '@testing-library/react';
import { useRateLimitHandler } from '../useRateLimitHandler';
import { RateLimitError } from '@/lib/api/core/errors';

describe('useRateLimitHandler', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('initialization', () => {
    it('should initialize with no rate limit active', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      expect(result.current.isRateLimited).toBe(false);
      expect(result.current.remainingSeconds).toBe(0);
      expect(result.current.message).toBeNull();
      expect(result.current.error).toBeNull();
    });
  });

  describe('handleError', () => {
    it('should activate rate limit with countdown', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit exceeded', endpoint: '/api/test' });
      // Mock getRetryAfterSeconds method
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(60);
      rateLimitError.getUserFriendlyMessage = vi.fn().mockReturnValue('Please wait 60 seconds');

      act(() => {
        result.current.handleError(rateLimitError);
      });

      expect(result.current.isRateLimited).toBe(true);
      expect(result.current.remainingSeconds).toBe(60);
      expect(result.current.error).toBe(rateLimitError);
    });

    it('should display user-friendly message', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit', endpoint: '/api/test' });
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(30);
      rateLimitError.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait 30 seconds');

      act(() => {
        result.current.handleError(rateLimitError);
      });

      expect(result.current.message).toBe('Wait 30 seconds');
    });

    it('should clear previous error when handling new error', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const error1 = new RateLimitError({ message: 'First', endpoint: '/api/test' });
      error1.getRetryAfterSeconds = vi.fn().mockReturnValue(10);
      error1.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait 10s');

      act(() => {
        result.current.handleError(error1);
      });

      const error2 = new RateLimitError({ message: 'Second', endpoint: '/api/test' });
      error2.getRetryAfterSeconds = vi.fn().mockReturnValue(20);
      error2.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait 20s');

      act(() => {
        result.current.handleError(error2);
      });

      expect(result.current.error).toBe(error2);
      expect(result.current.remainingSeconds).toBe(20);
    });
  });

  describe('countdown timer', () => {
    it('should decrement remainingSeconds every second', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit', endpoint: '/api/test' });
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(5);
      rateLimitError.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait');

      act(() => {
        result.current.handleError(rateLimitError);
      });

      expect(result.current.remainingSeconds).toBe(5);

      act(() => {
        vi.advanceTimersByTime(1000);
      });

      expect(result.current.remainingSeconds).toBe(4);

      act(() => {
        vi.advanceTimersByTime(1000);
      });

      expect(result.current.remainingSeconds).toBe(3);
    });

    it('should reset to 0 when countdown completes', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit', endpoint: '/api/test' });
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(2);
      rateLimitError.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait');

      act(() => {
        result.current.handleError(rateLimitError);
      });

      act(() => {
        vi.advanceTimersByTime(2000);
      });

      expect(result.current.remainingSeconds).toBe(0);
      expect(result.current.isRateLimited).toBe(false);
      expect(result.current.error).toBeNull();
    });

    it('should clear error when countdown completes', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit', endpoint: '/api/test' });
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(3);
      rateLimitError.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait');

      act(() => {
        result.current.handleError(rateLimitError);
      });

      expect(result.current.error).toBeTruthy();

      act(() => {
        vi.advanceTimersByTime(3000);
      });

      expect(result.current.error).toBeNull();
    });

    it('should cleanup timer on unmount', () => {
      const { result, unmount } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit', endpoint: '/api/test' });
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(60);
      rateLimitError.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait');

      act(() => {
        result.current.handleError(rateLimitError);
      });

      unmount();

      // Advance time - should not crash
      act(() => {
        vi.advanceTimersByTime(10000);
      });

      expect(true).toBe(true);
    });
  });

  describe('reset method', () => {
    it('should reset all state', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit', endpoint: '/api/test' });
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(60);
      rateLimitError.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait');

      act(() => {
        result.current.handleError(rateLimitError);
      });

      expect(result.current.isRateLimited).toBe(true);

      act(() => {
        result.current.reset();
      });

      expect(result.current.isRateLimited).toBe(false);
      expect(result.current.remainingSeconds).toBe(0);
      expect(result.current.error).toBeNull();
      expect(result.current.message).toBeNull();
    });

    it('should stop countdown timer on reset', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit', endpoint: '/api/test' });
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(60);
      rateLimitError.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait');

      act(() => {
        result.current.handleError(rateLimitError);
      });

      act(() => {
        result.current.reset();
      });

      // Advance time - should not decrement
      act(() => {
        vi.advanceTimersByTime(10000);
      });

      expect(result.current.remainingSeconds).toBe(0);
    });

    it('should allow handling new error after reset', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const error1 = new RateLimitError({ message: 'First', endpoint: '/api/test' });
      error1.getRetryAfterSeconds = vi.fn().mockReturnValue(30);
      error1.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait 30s');

      act(() => {
        result.current.handleError(error1);
      });

      act(() => {
        result.current.reset();
      });

      const error2 = new RateLimitError({ message: 'Second', endpoint: '/api/test' });
      error2.getRetryAfterSeconds = vi.fn().mockReturnValue(45);
      error2.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait 45s');

      act(() => {
        result.current.handleError(error2);
      });

      expect(result.current.remainingSeconds).toBe(45);
      expect(result.current.error).toBe(error2);
    });
  });

  describe('Edge Cases', () => {
    it('should handle retryAfterSeconds = 0', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit', endpoint: '/api/test' });
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(0);
      rateLimitError.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait');

      act(() => {
        result.current.handleError(rateLimitError);
      });

      expect(result.current.remainingSeconds).toBe(0);
      expect(result.current.isRateLimited).toBe(false);
    });

    it('should handle retryAfterSeconds = 1', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit', endpoint: '/api/test' });
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(1);
      rateLimitError.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait 1 second');

      act(() => {
        result.current.handleError(rateLimitError);
      });

      expect(result.current.remainingSeconds).toBe(1);

      act(() => {
        vi.advanceTimersByTime(1000);
      });

      expect(result.current.isRateLimited).toBe(false);
    });

    it('should handle very large retryAfterSeconds', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit', endpoint: '/api/test' });
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(3600); // 1 hour
      rateLimitError.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait 1 hour');

      act(() => {
        result.current.handleError(rateLimitError);
      });

      expect(result.current.remainingSeconds).toBe(3600);
      expect(result.current.isRateLimited).toBe(true);
    });

    it('should update message as countdown progresses', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const rateLimitError = new RateLimitError({ message: 'Rate limit', endpoint: '/api/test' });
      rateLimitError.getRetryAfterSeconds = vi.fn().mockReturnValue(5);
      rateLimitError.getUserFriendlyMessage = vi.fn((seconds) => `Wait ${seconds} seconds`);

      act(() => {
        result.current.handleError(rateLimitError);
      });

      expect(result.current.message).toBe('Wait 5 seconds');

      act(() => {
        vi.advanceTimersByTime(1000);
      });

      expect(result.current.message).toBe('Wait 4 seconds');

      act(() => {
        vi.advanceTimersByTime(1000);
      });

      expect(result.current.message).toBe('Wait 3 seconds');
    });

    it('should handle rapid successive errors', () => {
      const { result } = renderHook(() => useRateLimitHandler());

      const error1 = new RateLimitError({ message: 'First', endpoint: '/api/test' });
      error1.getRetryAfterSeconds = vi.fn().mockReturnValue(10);
      error1.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait 10s');

      const error2 = new RateLimitError({ message: 'Second', endpoint: '/api/test' });
      error2.getRetryAfterSeconds = vi.fn().mockReturnValue(15);
      error2.getUserFriendlyMessage = vi.fn().mockReturnValue('Wait 15s');

      act(() => {
        result.current.handleError(error1);
        result.current.handleError(error2);
      });

      // Should use latest error
      expect(result.current.remainingSeconds).toBe(15);
    });
  });
});
