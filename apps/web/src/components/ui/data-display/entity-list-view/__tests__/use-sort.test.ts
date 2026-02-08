/**
 * Tests for useSort hook
 */

import { renderHook, act } from '@testing-library/react';
import { useSort } from '../hooks/use-sort';
import { createComparator } from '../utils/sort-utils';
import type { SortOption } from '../entity-list-view.types';
import { vi } from 'vitest';

interface MockItem {
  id: string;
  title: string;
  rating: number;
}

const mockItems: MockItem[] = [
  { id: '1', title: 'Wingspan', rating: 8.1 },
  { id: '2', title: 'Gloomhaven', rating: 8.8 },
  { id: '3', title: 'Azul', rating: 7.8 },
];

const sortOptions: SortOption<MockItem>[] = [
  {
    value: 'rating',
    label: 'Rating',
    compareFn: createComparator('rating', 'rating'),
  },
  {
    value: 'name',
    label: 'Name (A-Z)',
    compareFn: createComparator('title', 'alphabetical'),
  },
];

describe('useSort', () => {
  it('should use default sort on init', () => {
    const { result } = renderHook(() => useSort(mockItems, sortOptions, 'rating'));

    expect(result.current.currentSort).toBe('rating');
    // Rating sort is descending (highest first)
    expect(result.current.sortedItems[0].title).toBe('Gloomhaven'); // 8.8
  });

  it('should sort items by selected option', () => {
    const { result } = renderHook(() => useSort(mockItems, sortOptions, 'name'));

    expect(result.current.currentSort).toBe('name');
    // Alphabetical sort
    expect(result.current.sortedItems[0].title).toBe('Azul');
    expect(result.current.sortedItems[1].title).toBe('Gloomhaven');
    expect(result.current.sortedItems[2].title).toBe('Wingspan');
  });

  it('should update sort when setCurrentSort is called', () => {
    const { result } = renderHook(() => useSort(mockItems, sortOptions, 'rating'));

    act(() => {
      result.current.setCurrentSort('name');
    });

    expect(result.current.currentSort).toBe('name');
    expect(result.current.sortedItems[0].title).toBe('Azul');
  });

  it('should not mutate original items array', () => {
    const originalItems = [...mockItems];
    const { result } = renderHook(() => useSort(mockItems, sortOptions, 'rating'));

    // Original array unchanged
    expect(mockItems).toEqual(originalItems);
    // Sorted array is different
    expect(result.current.sortedItems).not.toEqual(mockItems);
  });

  it('should handle invalid sort option gracefully', () => {
    const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation();

    const { result } = renderHook(() => useSort(mockItems, sortOptions, 'invalid'));

    expect(result.current.sortedItems).toEqual(mockItems); // Return unsorted
    expect(consoleWarnSpy).toHaveBeenCalledWith(
      expect.stringContaining('not found')
    );

    consoleWarnSpy.mockRestore();
  });

  it('should use controlled sort when provided', () => {
    const { result, rerender } = renderHook(
      ({ controlled }) => useSort(mockItems, sortOptions, 'rating', controlled),
      { initialProps: { controlled: 'name' } }
    );

    expect(result.current.currentSort).toBe('name');

    // Controlled value changes
    rerender({ controlled: 'rating' });

    expect(result.current.currentSort).toBe('rating');
  });
});
