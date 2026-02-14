/**
 * useActivityTimelineParams Tests (Issue #3925)
 *
 * Test coverage for URL state management hook.
 * Tests: parsing, URL updates, toggle, clear all, hasActiveFilters.
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useActivityTimelineParams } from '../useActivityTimelineParams';

// ============================================================================
// Mocks
// ============================================================================

const mockPush = vi.fn();
let mockSearchParams = new URLSearchParams();

vi.mock('next/navigation', () => ({
  useSearchParams: () => mockSearchParams,
  useRouter: () => ({ push: mockPush }),
  usePathname: () => '/dashboard',
}));

// ============================================================================
// Tests
// ============================================================================

describe('useActivityTimelineParams', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSearchParams = new URLSearchParams();
  });

  describe('Parsing', () => {
    it('returns default params when URL is empty', () => {
      const { result } = renderHook(() => useActivityTimelineParams());

      expect(result.current.params).toEqual({
        types: [],
        search: '',
        skip: 0,
        take: 20,
        order: 'desc',
      });
    });

    it('parses filter types from URL', () => {
      mockSearchParams = new URLSearchParams('filter=game_added,session_completed');

      const { result } = renderHook(() => useActivityTimelineParams());

      expect(result.current.params.types).toEqual(['game_added', 'session_completed']);
    });

    it('parses search from URL', () => {
      mockSearchParams = new URLSearchParams('search=catan');

      const { result } = renderHook(() => useActivityTimelineParams());

      expect(result.current.params.search).toBe('catan');
    });

    it('parses both filter and search from URL', () => {
      mockSearchParams = new URLSearchParams('filter=game_added&search=catan');

      const { result } = renderHook(() => useActivityTimelineParams());

      expect(result.current.params.types).toEqual(['game_added']);
      expect(result.current.params.search).toBe('catan');
    });

    it('ignores invalid filter types', () => {
      mockSearchParams = new URLSearchParams('filter=game_added,invalid_type,session_completed');

      const { result } = renderHook(() => useActivityTimelineParams());

      expect(result.current.params.types).toEqual(['game_added', 'session_completed']);
    });
  });

  describe('setTypes', () => {
    it('updates URL with filter types', () => {
      const { result } = renderHook(() => useActivityTimelineParams());

      act(() => {
        result.current.setTypes(['game_added', 'chat_saved']);
      });

      expect(mockPush).toHaveBeenCalledWith(
        '/dashboard?filter=game_added%2Cchat_saved',
        { scroll: false }
      );
    });

    it('removes filter param when types is empty', () => {
      mockSearchParams = new URLSearchParams('filter=game_added');

      const { result } = renderHook(() => useActivityTimelineParams());

      act(() => {
        result.current.setTypes([]);
      });

      expect(mockPush).toHaveBeenCalledWith('/dashboard', { scroll: false });
    });
  });

  describe('setSearch', () => {
    it('updates URL with search param', () => {
      const { result } = renderHook(() => useActivityTimelineParams());

      act(() => {
        result.current.setSearch('wingspan');
      });

      expect(mockPush).toHaveBeenCalledWith(
        '/dashboard?search=wingspan',
        { scroll: false }
      );
    });

    it('removes search param when empty', () => {
      mockSearchParams = new URLSearchParams('search=test');

      const { result } = renderHook(() => useActivityTimelineParams());

      act(() => {
        result.current.setSearch('');
      });

      expect(mockPush).toHaveBeenCalledWith('/dashboard', { scroll: false });
    });

    it('preserves filter when updating search', () => {
      mockSearchParams = new URLSearchParams('filter=game_added');

      const { result } = renderHook(() => useActivityTimelineParams());

      act(() => {
        result.current.setSearch('catan');
      });

      expect(mockPush).toHaveBeenCalledWith(
        expect.stringContaining('filter=game_added'),
        { scroll: false }
      );
      expect(mockPush).toHaveBeenCalledWith(
        expect.stringContaining('search=catan'),
        { scroll: false }
      );
    });
  });

  describe('toggleType', () => {
    it('adds type when not selected', () => {
      const { result } = renderHook(() => useActivityTimelineParams());

      act(() => {
        result.current.toggleType('game_added');
      });

      expect(mockPush).toHaveBeenCalledWith(
        '/dashboard?filter=game_added',
        { scroll: false }
      );
    });

    it('removes type when already selected', () => {
      mockSearchParams = new URLSearchParams('filter=game_added,session_completed');

      const { result } = renderHook(() => useActivityTimelineParams());

      act(() => {
        result.current.toggleType('game_added');
      });

      expect(mockPush).toHaveBeenCalledWith(
        '/dashboard?filter=session_completed',
        { scroll: false }
      );
    });
  });

  describe('clearAll', () => {
    it('clears all params from URL', () => {
      mockSearchParams = new URLSearchParams('filter=game_added&search=test');

      const { result } = renderHook(() => useActivityTimelineParams());

      act(() => {
        result.current.clearAll();
      });

      expect(mockPush).toHaveBeenCalledWith('/dashboard', { scroll: false });
    });
  });

  describe('hasActiveFilters', () => {
    it('returns false when no filters active', () => {
      const { result } = renderHook(() => useActivityTimelineParams());

      expect(result.current.hasActiveFilters).toBe(false);
    });

    it('returns true when types are selected', () => {
      mockSearchParams = new URLSearchParams('filter=game_added');

      const { result } = renderHook(() => useActivityTimelineParams());

      expect(result.current.hasActiveFilters).toBe(true);
    });

    it('returns true when search is active', () => {
      mockSearchParams = new URLSearchParams('search=catan');

      const { result } = renderHook(() => useActivityTimelineParams());

      expect(result.current.hasActiveFilters).toBe(true);
    });
  });
});
