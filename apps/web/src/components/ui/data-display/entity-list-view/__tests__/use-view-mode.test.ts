/**
 * Tests for useViewMode hook
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { useViewMode } from '../hooks/use-view-mode';
import type { ViewMode } from '../entity-list-view.types';
import { vi } from 'vitest';

describe('useViewMode', () => {
  const TEST_KEY = 'test-page';

  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  describe('Initialization', () => {
    it('should initialize with default mode', () => {
      const { result } = renderHook(() =>
        useViewMode(TEST_KEY, 'grid', ['grid', 'list', 'carousel'])
      );

      expect(result.current.mode).toBe('grid');
    });

    it('should initialize with first available mode if defaultMode is not available', () => {
      const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation();

      const { result } = renderHook(() =>
        useViewMode(TEST_KEY, 'carousel' as ViewMode, ['grid', 'list'])
      );

      // Should fallback to first available mode
      expect(result.current.mode).toBe('grid');

      consoleWarnSpy.mockRestore();
    });

    it('should restore mode from localStorage', () => {
      localStorage.setItem('view-mode:test-page', JSON.stringify('list'));

      const { result } = renderHook(() =>
        useViewMode(TEST_KEY, 'grid', ['grid', 'list', 'carousel'])
      );

      expect(result.current.mode).toBe('list');
    });

    it('should use defaultMode if localStorage has invalid mode', async () => {
      localStorage.setItem('view-mode:test-page', JSON.stringify('invalid-mode'));

      const { result } = renderHook(() =>
        useViewMode(TEST_KEY, 'grid', ['grid', 'list'])
      );

      // Should fallback to grid (first available)
      await waitFor(() => {
        expect(result.current.mode).toBe('grid');
      });
    });
  });

  describe('Mode Updates', () => {
    it('should update mode and persist to localStorage', () => {
      const { result } = renderHook(() =>
        useViewMode(TEST_KEY, 'grid', ['grid', 'list', 'carousel'])
      );

      act(() => {
        result.current.setMode('list');
      });

      expect(result.current.mode).toBe('list');
      expect(localStorage.getItem('view-mode:test-page')).toBe('"list"');
    });

    it('should not update mode if mode is not available', () => {
      const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation();

      const { result } = renderHook(() =>
        useViewMode(TEST_KEY, 'grid', ['grid', 'list'])
      );

      act(() => {
        result.current.setMode('carousel' as ViewMode);
      });

      // Mode should not change
      expect(result.current.mode).toBe('grid');
      expect(consoleWarnSpy).toHaveBeenCalledWith(
        expect.stringContaining('not available')
      );

      consoleWarnSpy.mockRestore();
    });

    it('should persist mode across component remount', () => {
      const { result, unmount } = renderHook(() =>
        useViewMode(TEST_KEY, 'grid', ['grid', 'list', 'carousel'])
      );

      act(() => {
        result.current.setMode('carousel');
      });

      unmount();

      // Remount
      const { result: result2 } = renderHook(() =>
        useViewMode(TEST_KEY, 'grid', ['grid', 'list', 'carousel'])
      );

      expect(result2.current.mode).toBe('carousel');
    });
  });

  describe('Controlled Mode', () => {
    it('should use controlled mode when provided', () => {
      const { result } = renderHook(() =>
        useViewMode(TEST_KEY, 'grid', ['grid', 'list', 'carousel'], 'list')
      );

      expect(result.current.mode).toBe('list');
    });

    it('should ignore localStorage when controlled mode is provided', () => {
      localStorage.setItem('view-mode:test-page', JSON.stringify('carousel'));

      const { result } = renderHook(() =>
        useViewMode(TEST_KEY, 'grid', ['grid', 'list', 'carousel'], 'grid')
      );

      // Controlled mode takes precedence
      expect(result.current.mode).toBe('grid');
    });

    it('should still persist to localStorage even in controlled mode', () => {
      const { result } = renderHook(() =>
        useViewMode(TEST_KEY, 'grid', ['grid', 'list', 'carousel'], 'list')
      );

      act(() => {
        result.current.setMode('carousel');
      });

      // Should persist for when component becomes uncontrolled
      expect(localStorage.getItem('view-mode:test-page')).toBe('"carousel"');
    });
  });

  describe('Available Modes', () => {
    it('should check if mode is available', () => {
      const { result } = renderHook(() =>
        useViewMode(TEST_KEY, 'grid', ['grid', 'list'])
      );

      expect(result.current.isAvailable('grid')).toBe(true);
      expect(result.current.isAvailable('list')).toBe(true);
      expect(result.current.isAvailable('carousel')).toBe(false);
    });

    it('should handle dynamic availableModes changes', async () => {
      const { result, rerender } = renderHook(
        ({ availableModes }) => useViewMode(TEST_KEY, 'grid', availableModes),
        { initialProps: { availableModes: ['grid', 'list', 'carousel'] as ViewMode[] } }
      );

      // Set to carousel
      act(() => {
        result.current.setMode('carousel');
      });

      expect(result.current.mode).toBe('carousel');

      // Remove carousel from available modes
      rerender({ availableModes: ['grid', 'list'] as ViewMode[] });

      // Should fallback to first available mode
      await waitFor(() => {
        expect(result.current.mode).toBe('grid');
      });
    });
  });

  describe('Persistence Key Namespacing', () => {
    it('should maintain independent state per persistenceKey', () => {
      const { result: result1 } = renderHook(() =>
        useViewMode('page-a', 'grid', ['grid', 'list', 'carousel'])
      );

      const { result: result2 } = renderHook(() =>
        useViewMode('page-b', 'grid', ['grid', 'list', 'carousel'])
      );

      // Update page-a to list
      act(() => {
        result1.current.setMode('list');
      });

      // page-b should still be grid
      expect(result2.current.mode).toBe('grid');

      // Verify separate localStorage keys
      expect(localStorage.getItem('view-mode:page-a')).toBe('"list"');
      expect(localStorage.getItem('view-mode:page-b')).toBe('"grid"');
    });
  });
});
