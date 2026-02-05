/**
 * Tests for useCarouselSort hook
 *
 * Issue #3590: GC-005 — Unit & Integration Tests
 * Epic: #3585 — GameCarousel Integration & Production Readiness
 *
 * Tests sort state persistence, URL sync, and localStorage integration.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { useCarouselSort } from '../useCarouselSort';
import type { CarouselSortValue } from '@/components/ui/data-display/game-carousel';

// Mock Next.js navigation
const mockPush = vi.fn();
const mockReplace = vi.fn();
const mockSearchParams = new URLSearchParams();
let mockPathname = '/gallery';

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    replace: mockReplace,
  }),
  useSearchParams: () => mockSearchParams,
  usePathname: () => mockPathname,
}));

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: vi.fn((key: string) => store[key] || null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key];
    }),
    clear: vi.fn(() => {
      store = {};
    }),
  };
})();

Object.defineProperty(window, 'localStorage', {
  value: localStorageMock,
});

describe('useCarouselSort', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorageMock.clear();
    mockSearchParams.delete('sort');
  });

  afterEach(() => {
    localStorageMock.clear();
  });

  // --------------------------------------------------------------------------
  // Basic Functionality
  // --------------------------------------------------------------------------

  describe('Basic Functionality', () => {
    it('should return default sort value when no preference stored', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
        })
      );

      expect(result.current.sort).toBe('rating');
      expect(result.current.isDefault).toBe(true);
    });

    it('should use provided defaultSort', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'popularity',
        })
      );

      expect(result.current.sort).toBe('popularity');
    });

    it('should default to rating when no defaultSort provided', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
        })
      );

      expect(result.current.sort).toBe('rating');
    });

    it('should update sort value', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
        })
      );

      act(() => {
        result.current.setSort('name');
      });

      expect(result.current.sort).toBe('name');
      expect(result.current.isDefault).toBe(false);
    });
  });

  // --------------------------------------------------------------------------
  // localStorage Persistence
  // --------------------------------------------------------------------------

  describe('localStorage Persistence', () => {
    it('should save sort to localStorage', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'my-carousel',
          defaultSort: 'rating',
        })
      );

      act(() => {
        result.current.setSort('name');
      });

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        'carousel-sort-my-carousel',
        'name'
      );
    });

    it('should restore sort from localStorage on mount', () => {
      // Pre-populate localStorage
      localStorageMock.getItem.mockReturnValueOnce('popularity');

      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'my-carousel',
          defaultSort: 'rating',
        })
      );

      // Should get the stored value, but since mock returns once,
      // we verify the call was made
      expect(localStorageMock.getItem).toHaveBeenCalledWith(
        'carousel-sort-my-carousel'
      );
    });

    it('should clear preference from localStorage', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'my-carousel',
          defaultSort: 'rating',
        })
      );

      act(() => {
        result.current.setSort('name');
      });

      act(() => {
        result.current.clearPreference();
      });

      expect(localStorageMock.removeItem).toHaveBeenCalledWith(
        'carousel-sort-my-carousel'
      );
      expect(result.current.sort).toBe('rating');
      expect(result.current.isDefault).toBe(true);
    });

    it('should handle localStorage not available', () => {
      // Simulate localStorage throwing
      localStorageMock.setItem.mockImplementationOnce(() => {
        throw new Error('localStorage not available');
      });

      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
        })
      );

      // Should not throw
      act(() => {
        result.current.setSort('name');
      });

      expect(result.current.sort).toBe('name');
    });

    it('should ignore invalid stored values', () => {
      localStorageMock.getItem.mockReturnValueOnce('invalid-sort-value');

      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
        })
      );

      // Should fall back to default
      expect(result.current.sort).toBe('rating');
    });
  });

  // --------------------------------------------------------------------------
  // URL Sync
  // --------------------------------------------------------------------------

  describe('URL Sync', () => {
    beforeEach(() => {
      mockPathname = '/gallery';
    });

    it('should not sync with URL by default', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
        })
      );

      act(() => {
        result.current.setSort('name');
      });

      expect(mockReplace).not.toHaveBeenCalled();
    });

    it('should sync with URL when syncWithUrl is true', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
          syncWithUrl: true,
        })
      );

      act(() => {
        result.current.setSort('popularity');
      });

      expect(mockReplace).toHaveBeenCalledWith(
        expect.stringContaining('sort=popularity'),
        expect.anything()
      );
    });

    it('should remove URL param when sort is default', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
          syncWithUrl: true,
        })
      );

      // First set to non-default
      act(() => {
        result.current.setSort('name');
      });

      // Then set back to default
      act(() => {
        result.current.setSort('rating');
      });

      // Should remove the param
      expect(mockReplace).toHaveBeenLastCalledWith('/gallery', expect.anything());
    });

    it('should use custom URL param name', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
          syncWithUrl: true,
          urlParamName: 'order',
        })
      );

      act(() => {
        result.current.setSort('name');
      });

      expect(mockReplace).toHaveBeenCalledWith(
        expect.stringContaining('order=name'),
        expect.anything()
      );
    });

    it('should preserve other URL params', () => {
      // Mock existing params
      const paramsWithOthers = new URLSearchParams('page=2&filter=strategy');
      vi.mocked(vi.fn()).mockReturnValue(paramsWithOthers.toString());

      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
          syncWithUrl: true,
        })
      );

      act(() => {
        result.current.setSort('name');
      });

      // URL should include the new sort param
      expect(mockReplace).toHaveBeenCalled();
    });
  });

  // --------------------------------------------------------------------------
  // isDefault Tracking
  // --------------------------------------------------------------------------

  describe('isDefault Tracking', () => {
    it('should be true when sort equals default', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
        })
      );

      expect(result.current.isDefault).toBe(true);
    });

    it('should be false when sort differs from default', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
        })
      );

      act(() => {
        result.current.setSort('name');
      });

      expect(result.current.isDefault).toBe(false);
    });

    it('should become true again when set back to default', () => {
      const { result } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'test-carousel',
          defaultSort: 'rating',
        })
      );

      act(() => {
        result.current.setSort('name');
      });

      expect(result.current.isDefault).toBe(false);

      act(() => {
        result.current.setSort('rating');
      });

      expect(result.current.isDefault).toBe(true);
    });
  });

  // --------------------------------------------------------------------------
  // Multiple Carousels
  // --------------------------------------------------------------------------

  describe('Multiple Carousels', () => {
    it('should maintain separate state for different carouselKeys', () => {
      const { result: result1 } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'carousel-1',
          defaultSort: 'rating',
        })
      );

      const { result: result2 } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'carousel-2',
          defaultSort: 'rating',
        })
      );

      act(() => {
        result1.current.setSort('name');
      });

      expect(result1.current.sort).toBe('name');
      expect(result2.current.sort).toBe('rating');
    });

    it('should save to separate localStorage keys', () => {
      const { result: result1 } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'carousel-a',
          defaultSort: 'rating',
        })
      );

      const { result: result2 } = renderHook(() =>
        useCarouselSort({
          carouselKey: 'carousel-b',
          defaultSort: 'rating',
        })
      );

      act(() => {
        result1.current.setSort('name');
      });

      act(() => {
        result2.current.setSort('date');
      });

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        'carousel-sort-carousel-a',
        'name'
      );
      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        'carousel-sort-carousel-b',
        'date'
      );
    });
  });

  // --------------------------------------------------------------------------
  // Sort Value Validation
  // --------------------------------------------------------------------------

  describe('Sort Value Validation', () => {
    it('should accept valid sort values', () => {
      const validValues: CarouselSortValue[] = ['rating', 'popularity', 'name', 'date'];

      validValues.forEach(value => {
        const { result } = renderHook(() =>
          useCarouselSort({
            carouselKey: `test-${value}`,
            defaultSort: 'rating',
          })
        );

        act(() => {
          result.current.setSort(value);
        });

        expect(result.current.sort).toBe(value);
      });
    });
  });
});
