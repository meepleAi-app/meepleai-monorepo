/**
 * Tests for useRateLimitHandler hook
 *
 * Tests countdown timer, error handling, and auto-reset functionality
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { useRateLimitHandler } from '../useRateLimitHandler';
import { RateLimitError } from '@/lib/api/core/errors';

describe('useRateLimitHandler', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  it('should initialize with default state', () => {
    const { result } = renderHook(() => useRateLimitHandler());

    expect(result.current.isRateLimited).toBe(false);
    expect(result.current.remainingSeconds).toBe(0);
    expect(result.current.message).toBeNull();
    expect(result.current.error).toBeNull();
  });

  it('should handle rate limit error and set state', () => {
    const { result } = renderHook(() => useRateLimitHandler());

    const error = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 60,
    });

    act(() => {
      result.current.handleError(error);
    });

    expect(result.current.isRateLimited).toBe(true);
    expect(result.current.remainingSeconds).toBe(60);
    expect(result.current.message).toBe('Too many requests. Please wait 60 seconds.');
    expect(result.current.error).toBe(error);
  });

  it('should countdown remaining seconds', async () => {
    const { result } = renderHook(() => useRateLimitHandler());

    const error = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 5,
    });

    act(() => {
      result.current.handleError(error);
    });

    expect(result.current.remainingSeconds).toBe(5);

    // Advance 1 second
    act(() => {
      jest.advanceTimersByTime(1000);
    });

    await waitFor(() => {
      expect(result.current.remainingSeconds).toBe(4);
    });

    // Advance another 2 seconds
    act(() => {
      jest.advanceTimersByTime(2000);
    });

    await waitFor(() => {
      expect(result.current.remainingSeconds).toBe(2);
    });
  });

  it('should auto-reset when countdown reaches 0', async () => {
    const { result } = renderHook(() => useRateLimitHandler());

    const error = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 2,
    });

    act(() => {
      result.current.handleError(error);
    });

    expect(result.current.isRateLimited).toBe(true);
    expect(result.current.remainingSeconds).toBe(2);

    // Advance to countdown expiration
    act(() => {
      jest.advanceTimersByTime(2000);
    });

    await waitFor(() => {
      expect(result.current.isRateLimited).toBe(false);
      expect(result.current.remainingSeconds).toBe(0);
      expect(result.current.error).toBeNull();
      expect(result.current.message).toBeNull();
    });
  });

  it('should clear previous timer when new error is handled', async () => {
    const { result } = renderHook(() => useRateLimitHandler());

    const error1 = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 60,
    });

    act(() => {
      result.current.handleError(error1);
    });

    expect(result.current.remainingSeconds).toBe(60);

    // Advance 10 seconds
    act(() => {
      jest.advanceTimersByTime(10000);
    });

    await waitFor(() => {
      expect(result.current.remainingSeconds).toBe(50);
    });

    // Handle new error with different retry time
    const error2 = new RateLimitError({
      message: 'Too many requests again',
      retryAfter: 30,
    });

    act(() => {
      result.current.handleError(error2);
    });

    // Should reset to new error's retry time
    expect(result.current.remainingSeconds).toBe(30);
    expect(result.current.message).toBe('Too many requests again. Please wait 30 seconds.');
  });

  it('should manually reset rate limit state', () => {
    const { result } = renderHook(() => useRateLimitHandler());

    const error = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 60,
    });

    act(() => {
      result.current.handleError(error);
    });

    expect(result.current.isRateLimited).toBe(true);
    expect(result.current.remainingSeconds).toBe(60);

    act(() => {
      result.current.reset();
    });

    expect(result.current.isRateLimited).toBe(false);
    expect(result.current.remainingSeconds).toBe(0);
    expect(result.current.error).toBeNull();
    expect(result.current.message).toBeNull();
  });

  it('should update message as countdown progresses', async () => {
    const { result } = renderHook(() => useRateLimitHandler());

    const error = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 65, // More than 1 minute
    });

    act(() => {
      result.current.handleError(error);
    });

    expect(result.current.message).toBe('Too many requests. Please wait 2 minutes.');

    // Advance to under 1 minute
    act(() => {
      jest.advanceTimersByTime(10000); // 10 seconds
    });

    await waitFor(() => {
      expect(result.current.remainingSeconds).toBe(55);
      expect(result.current.message).toBe('Too many requests. Please wait 55 seconds.');
    });
  });

  it('should handle error with 0 retry after', () => {
    const { result } = renderHook(() => useRateLimitHandler());

    const error = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 0,
    });

    act(() => {
      result.current.handleError(error);
    });

    expect(result.current.isRateLimited).toBe(false);
    expect(result.current.remainingSeconds).toBe(0);
    expect(result.current.message).toBe('You can now retry your request.');
  });

  it('should cleanup timer on unmount', () => {
    const { result, unmount } = renderHook(() => useRateLimitHandler());

    const error = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 60,
    });

    act(() => {
      result.current.handleError(error);
    });

    expect(result.current.isRateLimited).toBe(true);

    // Unmount should cleanup timer
    unmount();

    // Verify no pending timers (would throw if timer wasn't cleared)
    expect(jest.getTimerCount()).toBe(0);
  });

  it('should handle rapid successive errors', async () => {
    const { result } = renderHook(() => useRateLimitHandler());

    const error1 = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 30,
    });

    const error2 = new RateLimitError({
      message: 'Still too many requests',
      retryAfter: 45,
    });

    const error3 = new RateLimitError({
      message: 'Way too many requests',
      retryAfter: 60,
    });

    act(() => {
      result.current.handleError(error1);
      result.current.handleError(error2);
      result.current.handleError(error3);
    });

    // Should use the last error's values
    expect(result.current.remainingSeconds).toBe(60);
    expect(result.current.message).toBe('Way too many requests. Please wait 1 minute.');
  });

  it('should handle error with 1 second retry after', () => {
    const { result } = renderHook(() => useRateLimitHandler());

    const error = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 1,
    });

    act(() => {
      result.current.handleError(error);
    });

    expect(result.current.message).toBe('Too many requests. Please wait 1 second.');
  });

  it('should format minutes correctly for 60 seconds', () => {
    const { result } = renderHook(() => useRateLimitHandler());

    const error = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 60,
    });

    act(() => {
      result.current.handleError(error);
    });

    expect(result.current.message).toBe('Too many requests. Please wait 1 minute.');
  });
});
