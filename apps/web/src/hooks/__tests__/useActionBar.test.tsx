/**
 * useActionBar Hook Tests
 * Issue #3290 - Phase 4: ActionBar System
 */

import { describe, it, expect, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { LayoutProvider } from '@/components/layout/LayoutProvider';
import { useActionBar } from '../useActionBar';

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

// Wrapper with library context
function createWrapper(context: 'default' | 'library' | 'game_detail' = 'library') {
  return function Wrapper({ children }: { children: ReactNode }) {
    const queryClient = createTestQueryClient();
    return (
      <QueryClientProvider client={queryClient}>
        <LayoutProvider initialContext={context}>{children}</LayoutProvider>
      </QueryClientProvider>
    );
  };
}

describe('useActionBar', () => {
  it('should return actions for library context', () => {
    const { result } = renderHook(() => useActionBar(), {
      wrapper: createWrapper('library'),
    });

    expect(result.current.isEmpty).toBe(false);
    expect(result.current.visibleActions.length).toBeGreaterThan(0);
    expect(result.current.context).toBe('library');
  });

  it('should return empty for default context', () => {
    const { result } = renderHook(() => useActionBar(), {
      wrapper: createWrapper('default'),
    });

    expect(result.current.isEmpty).toBe(true);
    expect(result.current.visibleActions.length).toBe(0);
    expect(result.current.overflowActions.length).toBe(0);
  });

  it('should limit visible actions based on device type (desktop = 6)', () => {
    const { result } = renderHook(() => useActionBar(), {
      wrapper: createWrapper('game_detail'),
    });

    // game_detail has 6 actions, desktop shows 6
    expect(result.current.visibleActions.length).toBeLessThanOrEqual(6);
  });

  it('should calculate overflow actions correctly', () => {
    const { result } = renderHook(() => useActionBar(), {
      wrapper: createWrapper('game_detail'),
    });

    // game_detail has 6 actions, desktop shows all 6
    expect(result.current.hasOverflow).toBe(false);
    // Total should equal visible + overflow
    const total = result.current.visibleActions.length + result.current.overflowActions.length;
    expect(total).toBe(6); // game_detail has 6 actions
  });

  it('should return isVisible as true when there are actions', () => {
    const { result } = renderHook(() => useActionBar(), {
      wrapper: createWrapper('library'),
    });

    expect(result.current.isVisible).toBe(true);
  });

  it('should return isVisible as false when there are no actions', () => {
    const { result } = renderHook(() => useActionBar(), {
      wrapper: createWrapper('default'),
    });

    expect(result.current.isVisible).toBe(false);
  });

  it('should sort actions by priority', () => {
    const { result } = renderHook(() => useActionBar(), {
      wrapper: createWrapper('library'),
    });

    const priorities = result.current.visibleActions.map(a => a.priority);
    const sortedPriorities = [...priorities].sort((a, b) => a - b);
    expect(priorities).toEqual(sortedPriorities);
  });
});

describe('Action Configuration', () => {
  it('should have library context actions', () => {
    const { result } = renderHook(() => useActionBar(), {
      wrapper: createWrapper('library'),
    });

    const actionIds = result.current.visibleActions.map(a => a.id);
    expect(actionIds).toContain('add');
    expect(actionIds).toContain('filter');
  });

  it('should have game_detail context actions', () => {
    const { result } = renderHook(() => useActionBar(), {
      wrapper: createWrapper('game_detail'),
    });

    const actionIds = result.current.visibleActions.map(a => a.id);
    expect(actionIds).toContain('play');
    expect(actionIds).toContain('ask-ai');
  });
});
