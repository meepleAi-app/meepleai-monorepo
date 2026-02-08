/**
 * Tests for useSearch hook
 */

import { renderHook, waitFor, act } from '@testing-library/react';
import { useSearch } from '../hooks/use-search';
import { vi } from 'vitest';

interface MockItem {
  id: string;
  title: string;
  description: string;
  rating: number;
}

const mockItems: MockItem[] = [
  { id: '1', title: 'Gloomhaven', description: 'Epic campaign game', rating: 8.8 },
  { id: '2', title: 'Wingspan', description: 'Bird collecting game', rating: 8.1 },
  { id: '3', title: 'Azul', description: 'Tile placement game', rating: 7.8 },
];

describe('useSearch', () => {
  it('should return all items when query is empty', () => {
    const { result } = renderHook(() => useSearch(mockItems, ['title', 'description']));

    expect(result.current.filteredItems).toEqual(mockItems);
    expect(result.current.query).toBe('');
  });

  it('should filter items by search query', async () => {
    const { result } = renderHook(() => useSearch(mockItems, ['title', 'description']));

    act(() => {
      result.current.setQuery('gloom');
    });

    // Wait for debounce (300ms)
    await waitFor(
      () => {
        expect(result.current.filteredItems).toHaveLength(1);
      },
      { timeout: 500 }
    );

    expect(result.current.filteredItems[0].title).toBe('Gloomhaven');
  });

  it('should be case-insensitive', async () => {
    const { result } = renderHook(() => useSearch(mockItems, ['title']));

    act(() => {
      result.current.setQuery('WINGSPAN');
    });

    await waitFor(
      () => {
        expect(result.current.filteredItems).toHaveLength(1);
      },
      { timeout: 500 }
    );

    expect(result.current.filteredItems[0].title).toBe('Wingspan');
  });

  it('should search across multiple fields', async () => {
    const { result } = renderHook(() => useSearch(mockItems, ['title', 'description']));

    act(() => {
      result.current.setQuery('campaign');
    });

    await waitFor(
      () => {
        expect(result.current.filteredItems).toHaveLength(1);
      },
      { timeout: 500 }
    );

    expect(result.current.filteredItems[0].title).toBe('Gloomhaven');
  });

  it('should use custom search function when provided', async () => {
    const customSearch = vi.fn((query: string, items: MockItem[]) =>
      items.filter((item) => item.rating > 8)
    );

    const { result } = renderHook(() => useSearch(mockItems, ['title'], customSearch));

    act(() => {
      result.current.setQuery('test');
    });

    await waitFor(
      () => {
        expect(customSearch).toHaveBeenCalledWith('test', mockItems);
      },
      { timeout: 500 }
    );

    expect(result.current.filteredItems).toHaveLength(2); // rating > 8
  });

  it('should debounce search queries (300ms)', async () => {
    const { result } = renderHook(() => useSearch(mockItems, ['title']));

    act(() => {
      result.current.setQuery('g');
    });

    // Before debounce - should still show all items
    expect(result.current.filteredItems).toEqual(mockItems);

    // After debounce - should be filtered
    await waitFor(
      () => {
        expect(result.current.filteredItems.length).toBeLessThan(mockItems.length);
      },
      { timeout: 500 }
    );
  });
});
