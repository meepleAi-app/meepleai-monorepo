/**
 * useLazyWidget Tests (Issue #3323)
 *
 * Test coverage for lazy loading hook.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useLazyWidget } from '../useLazyWidget';

// ============================================================================
// Tests
// ============================================================================

describe('useLazyWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('Initial State', () => {
    it('returns initial state with ref', () => {
      const { result } = renderHook(() => useLazyWidget());

      expect(result.current.ref).toBeDefined();
      expect(result.current.isVisible).toBe(false);
      expect(result.current.hasLoaded).toBe(false);
    });

    it('starts visible when disabled', () => {
      const { result } = renderHook(() => useLazyWidget({ disabled: true }));

      expect(result.current.isVisible).toBe(true);
      expect(result.current.hasLoaded).toBe(true);
    });

    it('returns triggerLoad function', () => {
      const { result } = renderHook(() => useLazyWidget());

      expect(typeof result.current.triggerLoad).toBe('function');
    });
  });

  describe('Manual Trigger', () => {
    it('triggerLoad sets visible and loaded states', () => {
      const { result } = renderHook(() => useLazyWidget());

      act(() => {
        result.current.triggerLoad();
      });

      expect(result.current.isVisible).toBe(true);
      expect(result.current.hasLoaded).toBe(true);
    });

    it('triggerLoad works when called multiple times', () => {
      const { result } = renderHook(() => useLazyWidget());

      act(() => {
        result.current.triggerLoad();
        result.current.triggerLoad();
      });

      expect(result.current.isVisible).toBe(true);
      expect(result.current.hasLoaded).toBe(true);
    });
  });

  describe('SSR Safety', () => {
    it('handles missing IntersectionObserver gracefully', () => {
      // Remove IntersectionObserver
      const originalIO = window.IntersectionObserver;
      // @ts-expect-error - intentionally setting to undefined for test
      window.IntersectionObserver = undefined;

      const { result } = renderHook(() => useLazyWidget());

      // Should default to visible for SSR compatibility
      expect(result.current.isVisible).toBe(true);
      expect(result.current.hasLoaded).toBe(true);

      // Restore
      window.IntersectionObserver = originalIO;
    });
  });

  describe('Options Handling', () => {
    it('accepts custom threshold option', () => {
      const { result } = renderHook(() =>
        useLazyWidget({ threshold: 0.5 })
      );

      expect(result.current.ref).toBeDefined();
      expect(result.current.isVisible).toBe(false);
    });

    it('accepts custom rootMargin option', () => {
      const { result } = renderHook(() =>
        useLazyWidget({ rootMargin: '200px' })
      );

      expect(result.current.ref).toBeDefined();
      expect(result.current.isVisible).toBe(false);
    });

    it('accepts once option', () => {
      const { result } = renderHook(() =>
        useLazyWidget({ once: true })
      );

      expect(result.current.ref).toBeDefined();
    });

    it('accepts multiple options', () => {
      const { result } = renderHook(() =>
        useLazyWidget({
          threshold: 0.25,
          rootMargin: '100px',
          once: false,
          disabled: false,
        })
      );

      expect(result.current.ref).toBeDefined();
      expect(result.current.isVisible).toBe(false);
      expect(result.current.hasLoaded).toBe(false);
    });
  });

  describe('Disabled State', () => {
    it('immediately visible when disabled', () => {
      const { result } = renderHook(() =>
        useLazyWidget({ disabled: true })
      );

      expect(result.current.isVisible).toBe(true);
      expect(result.current.hasLoaded).toBe(true);
    });

    it('disabled option takes precedence over other options', () => {
      const { result } = renderHook(() =>
        useLazyWidget({
          disabled: true,
          once: false,
          threshold: 0.5,
        })
      );

      expect(result.current.isVisible).toBe(true);
      expect(result.current.hasLoaded).toBe(true);
    });
  });

  describe('Ref Type', () => {
    it('returns ref with correct type', () => {
      const { result } = renderHook(() => useLazyWidget());

      expect(result.current.ref.current).toBeNull();
    });

    it('accepts custom element type', () => {
      const { result } = renderHook(() =>
        useLazyWidget<HTMLSpanElement>()
      );

      expect(result.current.ref.current).toBeNull();
    });
  });
});
