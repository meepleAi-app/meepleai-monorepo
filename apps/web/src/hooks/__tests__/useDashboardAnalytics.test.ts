/**
 * useDashboardAnalytics Hook Unit Tests
 * Issue #3982 - Dashboard Business Metrics Tracking & Validation
 *
 * Coverage areas:
 * - Page view tracking on mount
 * - Time-on-page tracking on unmount
 * - Mobile viewport detection
 * - Mobile bounce detection (no interaction within 10s)
 * - Click-through tracking
 * - Interaction marking
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';

// Mock analytics module
const mockTrackEvent = vi.fn();
const mockTrackTiming = vi.fn();

vi.mock('@/lib/analytics', () => ({
  trackEvent: (...args: unknown[]) => mockTrackEvent(...args),
  trackTiming: (...args: unknown[]) => mockTrackTiming(...args),
}));

import { useDashboardAnalytics } from '../useDashboardAnalytics';

// ============================================================================
// Helpers
// ============================================================================

function setViewportWidth(width: number) {
  Object.defineProperty(window, 'innerWidth', {
    writable: true,
    configurable: true,
    value: width,
  });
}

// ============================================================================
// Tests
// ============================================================================

describe('useDashboardAnalytics', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    setViewportWidth(1024); // Desktop by default
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ============================================================================
  // Page View Tracking
  // ============================================================================

  describe('Page View Tracking', () => {
    it('tracks dashboard_view on mount', () => {
      renderHook(() => useDashboardAnalytics());

      expect(mockTrackEvent).toHaveBeenCalledWith(
        'dashboard_view',
        expect.objectContaining({
          source: 'dashboard_hub',
          is_mobile: false,
        })
      );
    });

    it('tracks desktop view with viewport width', () => {
      setViewportWidth(1440);
      renderHook(() => useDashboardAnalytics());

      expect(mockTrackEvent).toHaveBeenCalledWith(
        'dashboard_view',
        expect.objectContaining({
          is_mobile: false,
          viewport_width: 1440,
        })
      );
    });

    it('tracks mobile view when viewport < 640px', () => {
      setViewportWidth(375);
      renderHook(() => useDashboardAnalytics());

      expect(mockTrackEvent).toHaveBeenCalledWith(
        'dashboard_view',
        expect.objectContaining({
          is_mobile: true,
          viewport_width: 375,
        })
      );
    });

    it('fires dashboard_mobile_view event for mobile viewports', () => {
      setViewportWidth(375);
      renderHook(() => useDashboardAnalytics());

      expect(mockTrackEvent).toHaveBeenCalledWith(
        'dashboard_mobile_view',
        expect.objectContaining({
          viewport_width: 375,
        })
      );
    });

    it('does not fire dashboard_mobile_view for desktop', () => {
      setViewportWidth(1024);
      renderHook(() => useDashboardAnalytics());

      expect(mockTrackEvent).not.toHaveBeenCalledWith(
        'dashboard_mobile_view',
        expect.anything()
      );
    });
  });

  // ============================================================================
  // Time-on-Page Tracking
  // ============================================================================

  describe('Time-on-Page Tracking', () => {
    it('tracks time_on_page on unmount', () => {
      const { unmount } = renderHook(() => useDashboardAnalytics());

      // Advance time by 30 seconds
      vi.advanceTimersByTime(30_000);

      unmount();

      expect(mockTrackTiming).toHaveBeenCalledWith(
        'Dashboard',
        'time_on_page',
        expect.any(Number)
      );
    });

    it('time_on_page value reflects actual duration', () => {
      const now = Date.now();
      vi.setSystemTime(now);

      const { unmount } = renderHook(() => useDashboardAnalytics());

      // Advance 60 seconds
      vi.advanceTimersByTime(60_000);
      vi.setSystemTime(now + 60_000);

      unmount();

      // The duration should be approximately 60000ms
      const timingCall = mockTrackTiming.mock.calls.find(
        (call) => call[0] === 'Dashboard' && call[1] === 'time_on_page'
      );
      expect(timingCall).toBeDefined();
      expect(timingCall![2]).toBeGreaterThanOrEqual(59_000);
    });
  });

  // ============================================================================
  // Mobile Bounce Detection
  // ============================================================================

  describe('Mobile Bounce Detection', () => {
    it('fires dashboard_mobile_bounce after 10s without interaction on mobile', () => {
      setViewportWidth(375);
      renderHook(() => useDashboardAnalytics());

      // Advance past bounce timeout
      vi.advanceTimersByTime(10_001);

      expect(mockTrackEvent).toHaveBeenCalledWith(
        'dashboard_mobile_bounce',
        expect.objectContaining({
          viewport_width: 375,
          timeout_ms: 10_000,
        })
      );
    });

    it('does not fire mobile bounce if user interacts before 10s', () => {
      setViewportWidth(375);
      renderHook(() => useDashboardAnalytics());

      // Simulate interaction at 5s
      vi.advanceTimersByTime(5_000);
      act(() => {
        window.dispatchEvent(new Event('click'));
      });

      // Advance past bounce timeout
      vi.advanceTimersByTime(6_000);

      expect(mockTrackEvent).not.toHaveBeenCalledWith(
        'dashboard_mobile_bounce',
        expect.anything()
      );
    });

    it('does not fire mobile bounce on desktop', () => {
      setViewportWidth(1024);
      renderHook(() => useDashboardAnalytics());

      vi.advanceTimersByTime(15_000);

      expect(mockTrackEvent).not.toHaveBeenCalledWith(
        'dashboard_mobile_bounce',
        expect.anything()
      );
    });

    it('clears bounce timer on unmount', () => {
      setViewportWidth(375);
      const { unmount } = renderHook(() => useDashboardAnalytics());

      // Unmount before 10s
      vi.advanceTimersByTime(3_000);
      unmount();

      // Advance past bounce timeout
      vi.advanceTimersByTime(15_000);

      expect(mockTrackEvent).not.toHaveBeenCalledWith(
        'dashboard_mobile_bounce',
        expect.anything()
      );
    });
  });

  // ============================================================================
  // Interaction Tracking
  // ============================================================================

  describe('Interaction Tracking', () => {
    it('tracks first click interaction', () => {
      renderHook(() => useDashboardAnalytics());

      act(() => {
        window.dispatchEvent(new Event('click'));
      });

      expect(mockTrackEvent).toHaveBeenCalledWith(
        'dashboard_interaction',
        expect.objectContaining({
          is_mobile: false,
        })
      );
    });

    it('tracks first scroll interaction', () => {
      renderHook(() => useDashboardAnalytics());

      act(() => {
        window.dispatchEvent(new Event('scroll'));
      });

      expect(mockTrackEvent).toHaveBeenCalledWith(
        'dashboard_interaction',
        expect.objectContaining({
          is_mobile: false,
        })
      );
    });

    it('only tracks first interaction (not duplicates)', () => {
      renderHook(() => useDashboardAnalytics());

      act(() => {
        window.dispatchEvent(new Event('click'));
        window.dispatchEvent(new Event('click'));
        window.dispatchEvent(new Event('scroll'));
      });

      const interactionCalls = mockTrackEvent.mock.calls.filter(
        (call) => call[0] === 'dashboard_interaction'
      );
      expect(interactionCalls).toHaveLength(1);
    });
  });

  // ============================================================================
  // Click-Through Tracking
  // ============================================================================

  describe('Click-Through Tracking', () => {
    it('returns trackClickThrough function', () => {
      const { result } = renderHook(() => useDashboardAnalytics());

      expect(typeof result.current.trackClickThrough).toBe('function');
    });

    it('fires dashboard_click_through event', () => {
      const { result } = renderHook(() => useDashboardAnalytics());

      act(() => {
        result.current.trackClickThrough('/library', 'library_snapshot');
      });

      expect(mockTrackEvent).toHaveBeenCalledWith(
        'dashboard_click_through',
        expect.objectContaining({
          destination: '/library',
          source: 'library_snapshot',
          is_mobile: false,
        })
      );
    });

    it('includes time_on_page_ms in click-through event', () => {
      const now = Date.now();
      vi.setSystemTime(now);

      const { result } = renderHook(() => useDashboardAnalytics());

      vi.advanceTimersByTime(5_000);
      vi.setSystemTime(now + 5_000);

      act(() => {
        result.current.trackClickThrough('/library', 'library_snapshot');
      });

      expect(mockTrackEvent).toHaveBeenCalledWith(
        'dashboard_click_through',
        expect.objectContaining({
          time_on_page_ms: expect.any(Number),
        })
      );
    });

    it('marks user as interacted (prevents bounce)', () => {
      setViewportWidth(375);
      const { result } = renderHook(() => useDashboardAnalytics());

      // Click through at 3s
      vi.advanceTimersByTime(3_000);
      act(() => {
        result.current.trackClickThrough('/library', 'library_snapshot');
      });

      // Advance past bounce timeout
      vi.advanceTimersByTime(10_000);

      expect(mockTrackEvent).not.toHaveBeenCalledWith(
        'dashboard_mobile_bounce',
        expect.anything()
      );
    });
  });

  // ============================================================================
  // Cleanup Tests
  // ============================================================================

  describe('Cleanup', () => {
    it('removes event listeners on unmount', () => {
      const removeEventListenerSpy = vi.spyOn(window, 'removeEventListener');
      const { unmount } = renderHook(() => useDashboardAnalytics());

      unmount();

      expect(removeEventListenerSpy).toHaveBeenCalledWith('click', expect.any(Function));
      expect(removeEventListenerSpy).toHaveBeenCalledWith('scroll', expect.any(Function));
      expect(removeEventListenerSpy).toHaveBeenCalledWith('keydown', expect.any(Function));
      expect(removeEventListenerSpy).toHaveBeenCalledWith('beforeunload', expect.any(Function));

      removeEventListenerSpy.mockRestore();
    });
  });
});
