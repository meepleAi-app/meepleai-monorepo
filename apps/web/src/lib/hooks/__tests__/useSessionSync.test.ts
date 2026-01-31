/**
 * useSessionSync Hook Tests (Issue #3163)
 *
 * Tests for SSE-based session sync hook
 *
 * Note: Full SSE behavior is tested in E2E tests (toolkit-realtime-sync.spec.ts)
 * These unit tests verify hook initialization and basic structure.
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useSessionSync } from '../useSessionSync';

describe('useSessionSync', () => {
  const mockSessionId = 'session-123';

  beforeEach(() => {
    // Mock EventSource to prevent actual connections in tests
    global.EventSource = vi.fn().mockImplementation(() => ({
      close: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      readyState: EventSource.CONNECTING,
    })) as unknown as typeof EventSource;
  });

  it('should initialize with default state', () => {
    const { result } = renderHook(() =>
      useSessionSync({
        sessionId: mockSessionId,
      })
    );

    expect(result.current.scores).toEqual([]);
    expect(result.current.error).toBeNull();
    expect(result.current.isConnected).toBe(false);
  });

  it('should accept all callback props', () => {
    const onScoreUpdate = vi.fn();
    const onPaused = vi.fn();
    const onResumed = vi.fn();
    const onFinalized = vi.fn();
    const onError = vi.fn();

    const { result } = renderHook(() =>
      useSessionSync({
        sessionId: mockSessionId,
        onScoreUpdate,
        onPaused,
        onResumed,
        onFinalized,
        onError,
      })
    );

    // Verify hook returns expected structure
    expect(result.current).toHaveProperty('scores');
    expect(result.current).toHaveProperty('isConnected');
    expect(result.current).toHaveProperty('error');
  });

  it('should create EventSource with correct URL', () => {
    renderHook(() =>
      useSessionSync({
        sessionId: mockSessionId,
      })
    );

    expect(global.EventSource).toHaveBeenCalledWith(
      expect.stringContaining(`/api/v1/sessions/${mockSessionId}/stream`),
      expect.objectContaining({ withCredentials: true })
    );
  });

  it('should use custom API base URL if provided', () => {
    const customBaseUrl = 'http://localhost:8080';

    renderHook(() =>
      useSessionSync({
        sessionId: mockSessionId,
        apiBaseUrl: customBaseUrl,
      })
    );

    expect(global.EventSource).toHaveBeenCalledWith(
      expect.stringContaining(customBaseUrl),
      expect.any(Object)
    );
  });

  it('should cleanup EventSource on unmount', () => {
    const mockClose = vi.fn();

    global.EventSource = vi.fn().mockImplementation(() => ({
      close: mockClose,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      readyState: EventSource.OPEN,
    })) as unknown as typeof EventSource;

    const { unmount } = renderHook(() =>
      useSessionSync({
        sessionId: mockSessionId,
      })
    );

    unmount();

    expect(mockClose).toHaveBeenCalled();
  });
});
