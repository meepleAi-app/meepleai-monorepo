/**
 * Tests for useScrollProgress hook
 * Issue #3451: Breadcrumbs and Scroll Progress
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useScrollProgress } from '../hooks/useScrollProgress';

describe('useScrollProgress', () => {
  let originalInnerHeight: number;
  let originalScrollHeight: number;

  beforeEach(() => {
    // Store original values
    originalInnerHeight = window.innerHeight;

    // Mock window.innerHeight
    Object.defineProperty(window, 'innerHeight', {
      writable: true,
      configurable: true,
      value: 1000,
    });

    // Mock document.documentElement.scrollHeight
    originalScrollHeight = document.documentElement.scrollHeight;
    Object.defineProperty(document.documentElement, 'scrollHeight', {
      writable: true,
      configurable: true,
      value: 3000, // Total scrollable height
    });

    // Mock window.scrollY
    Object.defineProperty(window, 'scrollY', {
      writable: true,
      configurable: true,
      value: 0,
    });

    // Reset any event listeners
    vi.spyOn(window, 'addEventListener');
    vi.spyOn(window, 'removeEventListener');
  });

  afterEach(() => {
    // Restore original values
    Object.defineProperty(window, 'innerHeight', {
      writable: true,
      configurable: true,
      value: originalInnerHeight,
    });

    vi.restoreAllMocks();
  });

  // =========================================================================
  // Initial State Tests
  // =========================================================================

  describe('Initial state', () => {
    it('should return 0 when at top of page', () => {
      Object.defineProperty(window, 'scrollY', { value: 0, configurable: true });
      const { result } = renderHook(() => useScrollProgress());
      expect(result.current).toBe(0);
    });

    it('should calculate initial progress based on current scroll position', () => {
      Object.defineProperty(window, 'scrollY', { value: 1000, configurable: true });
      const { result } = renderHook(() => useScrollProgress());
      // 1000 / (3000 - 1000) = 50%
      expect(result.current).toBe(50);
    });
  });

  // =========================================================================
  // Scroll Progress Calculation Tests
  // =========================================================================

  describe('Progress calculation', () => {
    it('should return 0 at top of page', () => {
      const { result } = renderHook(() => useScrollProgress());

      act(() => {
        Object.defineProperty(window, 'scrollY', { value: 0, configurable: true });
        window.dispatchEvent(new Event('scroll'));
      });

      expect(result.current).toBe(0);
    });

    it('should return 100 at bottom of page', () => {
      const { result } = renderHook(() => useScrollProgress());

      act(() => {
        // scrollY should be documentHeight - windowHeight to be at 100%
        Object.defineProperty(window, 'scrollY', { value: 2000, configurable: true });
        window.dispatchEvent(new Event('scroll'));
      });

      expect(result.current).toBe(100);
    });

    it('should return 50 when scrolled halfway', () => {
      const { result } = renderHook(() => useScrollProgress());

      act(() => {
        Object.defineProperty(window, 'scrollY', { value: 1000, configurable: true });
        window.dispatchEvent(new Event('scroll'));
      });

      expect(result.current).toBe(50);
    });

    it('should return 25 when scrolled quarter way', () => {
      const { result } = renderHook(() => useScrollProgress());

      act(() => {
        Object.defineProperty(window, 'scrollY', { value: 500, configurable: true });
        window.dispatchEvent(new Event('scroll'));
      });

      expect(result.current).toBe(25);
    });

    it('should return 75 when scrolled three-quarters', () => {
      const { result } = renderHook(() => useScrollProgress());

      act(() => {
        Object.defineProperty(window, 'scrollY', { value: 1500, configurable: true });
        window.dispatchEvent(new Event('scroll'));
      });

      expect(result.current).toBe(75);
    });

    it('should clamp to max 100 even if scroll exceeds bounds', () => {
      const { result } = renderHook(() => useScrollProgress());

      act(() => {
        // Scroll beyond maximum
        Object.defineProperty(window, 'scrollY', { value: 5000, configurable: true });
        window.dispatchEvent(new Event('scroll'));
      });

      expect(result.current).toBeLessThanOrEqual(100);
    });

    it('should clamp to min 0 for negative scroll values', () => {
      const { result } = renderHook(() => useScrollProgress());

      act(() => {
        Object.defineProperty(window, 'scrollY', { value: -100, configurable: true });
        window.dispatchEvent(new Event('scroll'));
      });

      expect(result.current).toBeGreaterThanOrEqual(0);
    });
  });

  // =========================================================================
  // Edge Cases
  // =========================================================================

  describe('Edge cases', () => {
    it('should return 100 when document is shorter than viewport', () => {
      Object.defineProperty(document.documentElement, 'scrollHeight', {
        value: 500, // Shorter than viewport
        configurable: true,
      });
      Object.defineProperty(window, 'innerHeight', {
        value: 1000,
        configurable: true,
      });

      const { result } = renderHook(() => useScrollProgress());
      expect(result.current).toBe(100);
    });

    it('should return 100 when document equals viewport height', () => {
      Object.defineProperty(document.documentElement, 'scrollHeight', {
        value: 1000,
        configurable: true,
      });
      Object.defineProperty(window, 'innerHeight', {
        value: 1000,
        configurable: true,
      });

      const { result } = renderHook(() => useScrollProgress());
      expect(result.current).toBe(100);
    });

    it('should round percentage to nearest integer', () => {
      Object.defineProperty(document.documentElement, 'scrollHeight', {
        value: 3000,
        configurable: true,
      });
      Object.defineProperty(window, 'innerHeight', {
        value: 1000,
        configurable: true,
      });

      const { result } = renderHook(() => useScrollProgress());

      act(() => {
        // 333 / 2000 = 16.65% -> rounds to 17
        Object.defineProperty(window, 'scrollY', { value: 333, configurable: true });
        window.dispatchEvent(new Event('scroll'));
      });

      expect(Number.isInteger(result.current)).toBe(true);
    });
  });

  // =========================================================================
  // Event Listener Tests
  // =========================================================================

  describe('Event listeners', () => {
    it('should add scroll and resize event listeners on mount', () => {
      renderHook(() => useScrollProgress());

      expect(window.addEventListener).toHaveBeenCalledWith(
        'scroll',
        expect.any(Function),
        { passive: true }
      );
      expect(window.addEventListener).toHaveBeenCalledWith(
        'resize',
        expect.any(Function),
        { passive: true }
      );
    });

    it('should remove event listeners on unmount', () => {
      const { unmount } = renderHook(() => useScrollProgress());
      unmount();

      expect(window.removeEventListener).toHaveBeenCalledWith(
        'scroll',
        expect.any(Function)
      );
      expect(window.removeEventListener).toHaveBeenCalledWith(
        'resize',
        expect.any(Function)
      );
    });

    it('should update progress on resize', () => {
      const { result } = renderHook(() => useScrollProgress());

      act(() => {
        Object.defineProperty(window, 'scrollY', { value: 500, configurable: true });
        window.dispatchEvent(new Event('resize'));
      });

      expect(result.current).toBe(25);
    });
  });

  // =========================================================================
  // Performance Tests
  // =========================================================================

  describe('Performance', () => {
    it('should use passive event listeners for better scroll performance', () => {
      renderHook(() => useScrollProgress());

      expect(window.addEventListener).toHaveBeenCalledWith(
        'scroll',
        expect.any(Function),
        expect.objectContaining({ passive: true })
      );
    });
  });
});
