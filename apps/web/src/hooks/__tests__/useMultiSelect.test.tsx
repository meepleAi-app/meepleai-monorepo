/**
 * useMultiSelect Hook Tests
 * Issue #3292 - Phase 6: Breadcrumb & Polish
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { LayoutProvider } from '@/components/layout/LayoutProvider';
import { useMultiSelect, type Selectable } from '../useMultiSelect';

// Mock dependencies
vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => ({
    deviceType: 'desktop',
    isMobile: false,
    isTablet: false,
    isDesktop: true,
    viewportWidth: 1024,
  }),
}));

// Create query client for tests
const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

// Wrapper
function TestWrapper({ children }: { children: ReactNode }) {
  const queryClient = createTestQueryClient();
  return (
    <QueryClientProvider client={queryClient}>
      <LayoutProvider>{children}</LayoutProvider>
    </QueryClientProvider>
  );
}

// Test items
interface TestItem extends Selectable {
  name: string;
}

const testItems: TestItem[] = [
  { id: '1', name: 'Item 1' },
  { id: '2', name: 'Item 2' },
  { id: '3', name: 'Item 3' },
];

describe('useMultiSelect', () => {
  beforeEach(() => {
    // Clean up any announcer elements
    const announcer = document.getElementById('multi-select-announcer');
    if (announcer) {
      announcer.remove();
    }
  });

  it('should start with inactive mode', () => {
    const { result } = renderHook(() => useMultiSelect<TestItem>(), {
      wrapper: TestWrapper,
    });

    expect(result.current.isActive).toBe(false);
    expect(result.current.selectedCount).toBe(0);
  });

  it('should enter multi-select mode', () => {
    const { result } = renderHook(() => useMultiSelect<TestItem>(), {
      wrapper: TestWrapper,
    });

    act(() => {
      result.current.enterMultiSelect();
    });

    expect(result.current.isActive).toBe(true);
  });

  it('should exit multi-select mode', () => {
    const { result } = renderHook(() => useMultiSelect<TestItem>(), {
      wrapper: TestWrapper,
    });

    act(() => {
      result.current.enterMultiSelect();
    });
    expect(result.current.isActive).toBe(true);

    act(() => {
      result.current.exitMultiSelect();
    });
    expect(result.current.isActive).toBe(false);
  });

  it('should toggle item selection', () => {
    const { result } = renderHook(() => useMultiSelect<TestItem>(), {
      wrapper: TestWrapper,
    });

    // Toggle on
    act(() => {
      result.current.toggle('1');
    });

    expect(result.current.isSelected('1')).toBe(true);
    expect(result.current.selectedCount).toBe(1);

    // Toggle off
    act(() => {
      result.current.toggle('1');
    });

    expect(result.current.isSelected('1')).toBe(false);
    expect(result.current.selectedCount).toBe(0);
  });

  it('should auto-enter multi-select on first selection', () => {
    const { result } = renderHook(() => useMultiSelect<TestItem>(), {
      wrapper: TestWrapper,
    });

    expect(result.current.isActive).toBe(false);

    act(() => {
      result.current.toggle('1');
    });

    expect(result.current.isActive).toBe(true);
  });

  it('should select all items', () => {
    const { result } = renderHook(() => useMultiSelect<TestItem>(), {
      wrapper: TestWrapper,
    });

    act(() => {
      result.current.selectAll(testItems);
    });

    expect(result.current.selectedCount).toBe(3);
    expect(result.current.isSelected('1')).toBe(true);
    expect(result.current.isSelected('2')).toBe(true);
    expect(result.current.isSelected('3')).toBe(true);
  });

  it('should clear selection', () => {
    const { result } = renderHook(() => useMultiSelect<TestItem>(), {
      wrapper: TestWrapper,
    });

    act(() => {
      result.current.selectAll(testItems);
    });
    expect(result.current.selectedCount).toBe(3);

    act(() => {
      result.current.clearSelection();
    });
    expect(result.current.selectedCount).toBe(0);
  });

  it('should return Set of selected IDs', () => {
    const { result } = renderHook(() => useMultiSelect<TestItem>(), {
      wrapper: TestWrapper,
    });

    act(() => {
      result.current.toggle('1');
      result.current.toggle('2');
    });

    expect(result.current.selectedIds instanceof Set).toBe(true);
    expect(result.current.selectedIds.has('1')).toBe(true);
    expect(result.current.selectedIds.has('2')).toBe(true);
  });

  it('should check if item is selected', () => {
    const { result } = renderHook(() => useMultiSelect<TestItem>(), {
      wrapper: TestWrapper,
    });

    act(() => {
      result.current.toggle('1');
    });

    expect(result.current.isSelected('1')).toBe(true);
    expect(result.current.isSelected('2')).toBe(false);
    expect(result.current.isSelected('3')).toBe(false);
  });
});
