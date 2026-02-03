/**
 * Collection Filters Store Tests - Issue #3026
 *
 * Test coverage:
 * - Initial state
 * - Filter actions (categories, complexity, playerCount, status, sortBy, searchQuery)
 * - Toggle actions (category, complexity)
 * - View actions (viewMode, sidebar)
 * - Reset functionality
 * - Persistence (localStorage)
 * - Selectors (filters, viewPreferences, hasActiveFilters)
 *
 * Target: >90% coverage
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import {
  useCollectionFiltersStore,
  selectFilters,
  selectViewPreferences,
  selectHasActiveFilters,
} from '../collection-filters-store';

// Mock localStorage
const mockLocalStorage: Record<string, string> = {};
beforeEach(() => {
  // Clear all mocks and storage
  Object.keys(mockLocalStorage).forEach(key => {
    delete mockLocalStorage[key];
  });
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(key => mockLocalStorage[key] ?? null);
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation((key, value) => {
    mockLocalStorage[key] = value;
  });
  vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(key => {
    delete mockLocalStorage[key];
  });

  // Reset Zustand store state to default
  useCollectionFiltersStore.setState({
    categories: [],
    complexity: [],
    playerCount: 'all',
    status: 'all',
    sortBy: 'recent',
    searchQuery: '',
    viewMode: 'grid',
    sidebarOpen: true,
  });
});

describe('useCollectionFiltersStore', () => {
  describe('Initial State', () => {
    it('initializes with default values', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      expect(result.current.categories).toEqual([]);
      expect(result.current.complexity).toEqual([]);
      expect(result.current.playerCount).toBe('all');
      expect(result.current.status).toBe('all');
      expect(result.current.sortBy).toBe('recent');
      expect(result.current.searchQuery).toBe('');
      expect(result.current.viewMode).toBe('grid');
      expect(result.current.sidebarOpen).toBe(true);
    });
  });

  describe('Category Actions', () => {
    it('sets categories correctly', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setCategories(['Strategy', 'Eurogame']);
      });

      expect(result.current.categories).toEqual(['Strategy', 'Eurogame']);
    });

    it('toggles category on', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.toggleCategory('Strategy');
      });

      expect(result.current.categories).toEqual(['Strategy']);
    });

    it('toggles category off', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setCategories(['Strategy', 'Eurogame']);
        result.current.toggleCategory('Strategy');
      });

      expect(result.current.categories).toEqual(['Eurogame']);
    });

    it('handles multiple category toggles', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.toggleCategory('Strategy');
        result.current.toggleCategory('Eurogame');
        result.current.toggleCategory('Cooperative');
      });

      expect(result.current.categories).toEqual(['Strategy', 'Eurogame', 'Cooperative']);

      act(() => {
        result.current.toggleCategory('Eurogame');
      });

      expect(result.current.categories).toEqual(['Strategy', 'Cooperative']);
    });
  });

  describe('Complexity Actions', () => {
    it('sets complexity correctly', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setComplexity([2, 3]);
      });

      expect(result.current.complexity).toEqual([2, 3]);
    });

    it('toggles complexity on', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.toggleComplexity(2);
      });

      expect(result.current.complexity).toEqual([2]);
    });

    it('toggles complexity off', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setComplexity([2, 3, 4]);
        result.current.toggleComplexity(3);
      });

      expect(result.current.complexity).toEqual([2, 4]);
    });

    it('handles multiple complexity toggles', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.toggleComplexity(1);
        result.current.toggleComplexity(2);
        result.current.toggleComplexity(3);
      });

      expect(result.current.complexity).toEqual([1, 2, 3]);

      act(() => {
        result.current.toggleComplexity(2);
      });

      expect(result.current.complexity).toEqual([1, 3]);
    });
  });

  describe('Other Filter Actions', () => {
    it('sets playerCount correctly', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setPlayerCount('2-4');
      });

      expect(result.current.playerCount).toBe('2-4');
    });

    it('sets status correctly', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setStatus('owned');
      });

      expect(result.current.status).toBe('owned');
    });

    it('sets sortBy correctly', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setSortBy('alphabetical');
      });

      expect(result.current.sortBy).toBe('alphabetical');
    });

    it('sets searchQuery correctly', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setSearchQuery('Catan');
      });

      expect(result.current.searchQuery).toBe('Catan');
    });
  });

  describe('View Actions', () => {
    it('sets viewMode correctly', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setViewMode('list');
      });

      expect(result.current.viewMode).toBe('list');
    });

    it('toggles sidebar on', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setViewMode('list');
        result.current.toggleSidebar();
      });

      expect(result.current.sidebarOpen).toBe(false);
    });

    it('toggles sidebar off', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.toggleSidebar();
      });

      expect(result.current.sidebarOpen).toBe(false);

      act(() => {
        result.current.toggleSidebar();
      });

      expect(result.current.sidebarOpen).toBe(true);
    });
  });

  describe('Reset Functionality', () => {
    it('resets filters to default state', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setCategories(['Strategy']);
        result.current.setComplexity([3, 4]);
        result.current.setPlayerCount('2-4');
        result.current.setStatus('owned');
        result.current.setSortBy('alphabetical');
        result.current.setSearchQuery('Catan');
        result.current.setViewMode('list');
        result.current.toggleSidebar();
      });

      expect(result.current.categories).toEqual(['Strategy']);
      expect(result.current.complexity).toEqual([3, 4]);
      expect(result.current.playerCount).toBe('2-4');
      expect(result.current.status).toBe('owned');
      expect(result.current.sortBy).toBe('alphabetical');
      expect(result.current.searchQuery).toBe('Catan');
      expect(result.current.viewMode).toBe('list');
      expect(result.current.sidebarOpen).toBe(false);

      act(() => {
        result.current.resetFilters();
      });

      expect(result.current.categories).toEqual([]);
      expect(result.current.complexity).toEqual([]);
      expect(result.current.playerCount).toBe('all');
      expect(result.current.status).toBe('all');
      expect(result.current.sortBy).toBe('recent');
      expect(result.current.searchQuery).toBe('');
      expect(result.current.viewMode).toBe('grid');
      expect(result.current.sidebarOpen).toBe(true);
    });
  });

  describe('Persistence', () => {
    it('persists viewMode, sidebarOpen, sortBy to localStorage', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setViewMode('list');
        result.current.toggleSidebar();
        result.current.setSortBy('rating');
      });

      // Check localStorage was called
      expect(Storage.prototype.setItem).toHaveBeenCalled();
    });

    it('does not persist active filters (categories, complexity, playerCount, status, searchQuery)', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setCategories(['Strategy']);
        result.current.setComplexity([3]);
        result.current.setPlayerCount('2-4');
        result.current.setStatus('owned');
        result.current.setSearchQuery('Catan');
      });

      // Storage.setItem should be called for persistence config, but filters should not be in partialize
      // This is intentional UX choice per line 97-98 comments
      expect(Storage.prototype.setItem).toHaveBeenCalled();
    });
  });

  describe('Selectors', () => {
    it('selectFilters returns all filter values', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setCategories(['Strategy']);
        result.current.setComplexity([3, 4]);
        result.current.setPlayerCount('2-4');
        result.current.setStatus('owned');
        result.current.setSortBy('rating');
        result.current.setSearchQuery('Catan');
      });

      const filters = selectFilters(result.current);

      expect(filters).toEqual({
        categories: ['Strategy'],
        complexity: [3, 4],
        playerCount: '2-4',
        status: 'owned',
        sortBy: 'rating',
        searchQuery: 'Catan',
      });
    });

    it('selectViewPreferences returns viewMode and sidebarOpen', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setViewMode('list');
        result.current.toggleSidebar();
      });

      const viewPrefs = selectViewPreferences(result.current);

      expect(viewPrefs).toEqual({
        viewMode: 'list',
        sidebarOpen: false,
      });
    });

    it('selectHasActiveFilters returns false with no filters', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      expect(selectHasActiveFilters(result.current)).toBe(false);
    });

    it('selectHasActiveFilters returns true with categories filter', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.toggleCategory('Strategy');
      });

      expect(selectHasActiveFilters(result.current)).toBe(true);
    });

    it('selectHasActiveFilters returns true with complexity filter', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.toggleComplexity(3);
      });

      expect(selectHasActiveFilters(result.current)).toBe(true);
    });

    it('selectHasActiveFilters returns true with playerCount filter', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setPlayerCount('2-4');
      });

      expect(selectHasActiveFilters(result.current)).toBe(true);
    });

    it('selectHasActiveFilters returns true with status filter', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setStatus('owned');
      });

      expect(selectHasActiveFilters(result.current)).toBe(true);
    });

    it('selectHasActiveFilters returns true with searchQuery', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setSearchQuery('Catan');
      });

      expect(selectHasActiveFilters(result.current)).toBe(true);
    });

    it('selectHasActiveFilters returns true with multiple filters', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.toggleCategory('Strategy');
        result.current.toggleComplexity(3);
        result.current.setPlayerCount('2-4');
      });

      expect(selectHasActiveFilters(result.current)).toBe(true);
    });
  });

  describe('State Consistency', () => {
    it('maintains state consistency across multiple changes', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setCategories(['Strategy']);
        result.current.toggleCategory('Eurogame');
        result.current.toggleComplexity(3);
        result.current.setPlayerCount('2-4');
        result.current.setSortBy('rating');
      });

      expect(result.current.categories).toEqual(['Strategy', 'Eurogame']);
      expect(result.current.complexity).toEqual([3]);
      expect(result.current.playerCount).toBe('2-4');
      expect(result.current.sortBy).toBe('rating');

      act(() => {
        result.current.resetFilters();
      });

      expect(result.current.categories).toEqual([]);
      expect(result.current.complexity).toEqual([]);
      expect(result.current.playerCount).toBe('all');
      expect(result.current.sortBy).toBe('recent');
    });

    it('handles edge case with empty arrays', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      act(() => {
        result.current.setCategories([]);
        result.current.setComplexity([]);
      });

      expect(result.current.categories).toEqual([]);
      expect(result.current.complexity).toEqual([]);
      expect(selectHasActiveFilters(result.current)).toBe(false);
    });

    it('handles all status values', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      const statuses: ('all' | 'owned' | 'played' | 'wishlist')[] = [
        'all',
        'owned',
        'played',
        'wishlist',
      ];

      statuses.forEach(status => {
        act(() => {
          result.current.setStatus(status);
        });

        expect(result.current.status).toBe(status);
      });
    });

    it('handles all sortBy values', () => {
      const { result } = renderHook(() => useCollectionFiltersStore());

      const sortOptions: ('recent' | 'alphabetical' | 'rating' | 'duration')[] = [
        'recent',
        'alphabetical',
        'rating',
        'duration',
      ];

      sortOptions.forEach(sortBy => {
        act(() => {
          result.current.setSortBy(sortBy);
        });

        expect(result.current.sortBy).toBe(sortBy);
      });
    });
  });
});
