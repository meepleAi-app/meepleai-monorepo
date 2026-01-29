/**
 * Tests for useCatalogSearchParams Hook (Issue #2876)
 *
 * Tests URL search params management for catalog pagination and filtering.
 * Coverage target: 90%+
 */

import { renderHook, act } from '@testing-library/react';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';

import { useCatalogSearchParams, type CatalogSearchParams } from '../useCatalogSearchParams';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
  usePathname: vi.fn(),
  useSearchParams: vi.fn(),
}));

describe('useCatalogSearchParams', () => {
  const mockPush = vi.fn();
  const mockPathname = '/games/catalog';

  beforeEach(() => {
    vi.clearAllMocks();
    (useRouter as Mock).mockReturnValue({ push: mockPush });
    (usePathname as Mock).mockReturnValue(mockPathname);
  });

  // ==========================================================================
  // Default Values
  // ==========================================================================

  describe('Default Values', () => {
    it('returns default params when URL has no search params', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams());

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params).toEqual({
        page: 1,
        pageSize: 20,
        searchTerm: '',
        sortBy: 'title',
        sortDescending: false,
        categoryIds: [],
        mechanicIds: [],
        minPlayers: undefined,
        maxPlayers: undefined,
        maxPlayingTime: undefined,
      });
    });

    it('returns default page when page param is invalid', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('page=invalid'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.page).toBe(1);
    });

    it('returns default sortBy when sortBy param is invalid', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('sortBy=invalid'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.sortBy).toBe('title');
    });
  });

  // ==========================================================================
  // URL Parsing
  // ==========================================================================

  describe('URL Parsing', () => {
    it('parses page from URL', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('page=5'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.page).toBe(5);
    });

    it('parses pageSize from URL', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('pageSize=50'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.pageSize).toBe(50);
    });

    it('parses searchTerm from URL (q param)', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('q=chess'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.searchTerm).toBe('chess');
    });

    it('parses sortBy from URL', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('sortBy=complexity'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.sortBy).toBe('complexity');
    });

    it('parses sortDescending from URL', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('desc=true'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.sortDescending).toBe(true);
    });

    it('parses categoryIds from comma-separated URL param', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('categories=cat1,cat2,cat3'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.categoryIds).toEqual(['cat1', 'cat2', 'cat3']);
    });

    it('parses mechanicIds from comma-separated URL param', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('mechanics=mech1,mech2'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.mechanicIds).toEqual(['mech1', 'mech2']);
    });

    it('parses player filter params', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('minPlayers=2&maxPlayers=4'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.minPlayers).toBe(2);
      expect(result.current.params.maxPlayers).toBe(4);
    });

    it('parses maxPlayingTime from URL', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('maxPlayingTime=60'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.maxPlayingTime).toBe(60);
    });

    it('handles all valid sortBy values', () => {
      const validSortByValues = ['title', 'complexity', 'playingTime', 'AverageRating', 'CreatedAt'];

      for (const sortBy of validSortByValues) {
        (useSearchParams as Mock).mockReturnValue(new URLSearchParams(`sortBy=${sortBy}`));
        const { result } = renderHook(() => useCatalogSearchParams());
        expect(result.current.params.sortBy).toBe(sortBy);
      }
    });
  });

  // ==========================================================================
  // URL Building
  // ==========================================================================

  describe('setParams', () => {
    it('updates URL with new page', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams());

      const { result } = renderHook(() => useCatalogSearchParams());

      act(() => {
        result.current.setParams({ page: 3 });
      });

      expect(mockPush).toHaveBeenCalledWith('/games/catalog?page=3', { scroll: false });
    });

    it('updates URL with search term', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams());

      const { result } = renderHook(() => useCatalogSearchParams());

      act(() => {
        result.current.setParams({ searchTerm: 'monopoly' });
      });

      expect(mockPush).toHaveBeenCalledWith('/games/catalog?q=monopoly', { scroll: false });
    });

    it('updates URL with multiple params', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams());

      const { result } = renderHook(() => useCatalogSearchParams());

      act(() => {
        result.current.setParams({ page: 2, sortBy: 'complexity', sortDescending: true });
      });

      // Check that push was called with correct base path
      expect(mockPush).toHaveBeenCalled();
      const pushArg = mockPush.mock.calls[0][0];
      expect(pushArg).toContain('/games/catalog?');
      expect(pushArg).toContain('page=2');
      expect(pushArg).toContain('sortBy=complexity');
      expect(pushArg).toContain('desc=true');
    });

    it('preserves existing params when adding new ones', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('page=2'));

      const { result } = renderHook(() => useCatalogSearchParams());

      act(() => {
        result.current.setParams({ searchTerm: 'chess' });
      });

      const pushArg = mockPush.mock.calls[0][0];
      expect(pushArg).toContain('page=2');
      expect(pushArg).toContain('q=chess');
    });

    it('omits default values from URL', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('page=5'));

      const { result } = renderHook(() => useCatalogSearchParams());

      act(() => {
        result.current.setParams({ page: 1 }); // Reset to default
      });

      // Should not include page=1 in URL since it's the default
      expect(mockPush).toHaveBeenCalledWith('/games/catalog', { scroll: false });
    });

    it('handles categoryIds as comma-separated string', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams());

      const { result } = renderHook(() => useCatalogSearchParams());

      act(() => {
        result.current.setParams({ categoryIds: ['cat1', 'cat2'] });
      });

      expect(mockPush).toHaveBeenCalledWith('/games/catalog?categories=cat1%2Ccat2', { scroll: false });
    });
  });

  // ==========================================================================
  // setPage Convenience Method
  // ==========================================================================

  describe('setPage', () => {
    it('updates only the page param', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('q=chess'));

      const { result } = renderHook(() => useCatalogSearchParams());

      act(() => {
        result.current.setPage(3);
      });

      const pushArg = mockPush.mock.calls[0][0];
      expect(pushArg).toContain('page=3');
      expect(pushArg).toContain('q=chess');
    });
  });

  // ==========================================================================
  // resetParams
  // ==========================================================================

  describe('resetParams', () => {
    it('clears all params from URL', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('page=5&q=chess&sortBy=complexity'));

      const { result } = renderHook(() => useCatalogSearchParams());

      act(() => {
        result.current.resetParams();
      });

      expect(mockPush).toHaveBeenCalledWith('/games/catalog', { scroll: false });
    });
  });

  // ==========================================================================
  // Edge Cases
  // ==========================================================================

  describe('Edge Cases', () => {
    it('handles empty categoryIds string', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('categories='));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.categoryIds).toEqual([]);
    });

    it('filters out empty values in comma-separated params', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('categories=cat1,,cat2,'));

      const { result } = renderHook(() => useCatalogSearchParams());

      expect(result.current.params.categoryIds).toEqual(['cat1', 'cat2']);
    });

    it('handles negative numbers gracefully', () => {
      (useSearchParams as Mock).mockReturnValue(new URLSearchParams('page=-1'));

      const { result } = renderHook(() => useCatalogSearchParams());

      // Should parse negative number (validation is responsibility of consumer)
      expect(result.current.params.page).toBe(-1);
    });
  });
});
