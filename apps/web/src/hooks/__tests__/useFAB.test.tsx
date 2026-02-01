/**
 * useFAB Hook Tests
 * Issue #3291 - Phase 5: Smart FAB
 */

import { describe, it, expect, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { LayoutProvider } from '@/components/layout/LayoutProvider';
import { useFAB } from '../useFAB';

// Mock dependencies - mobile by default
vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => ({
    deviceType: 'mobile',
    isMobile: true,
    isTablet: false,
    isDesktop: false,
    viewportWidth: 375,
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

describe('useFAB', () => {
  it('should return FAB config for library context', () => {
    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('library'),
    });

    expect(result.current.config).not.toBeNull();
    expect(result.current.config?.icon).toBe('plus');
    expect(result.current.config?.label).toBe('Aggiungi gioco');
  });

  it('should return null config for default context', () => {
    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('default'),
    });

    expect(result.current.config).toBeNull();
  });

  it('should be visible on mobile with valid context', () => {
    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('library'),
    });

    expect(result.current.isVisible).toBe(true);
  });

  it('should not be visible when context has no FAB config', () => {
    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('default'),
    });

    expect(result.current.isVisible).toBe(false);
  });

  it('should start with quick menu closed', () => {
    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('library'),
    });

    expect(result.current.isQuickMenuOpen).toBe(false);
  });

  it('should open quick menu', () => {
    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('library'),
    });

    act(() => {
      result.current.openQuickMenu();
    });

    expect(result.current.isQuickMenuOpen).toBe(true);
  });

  it('should close quick menu', () => {
    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('library'),
    });

    act(() => {
      result.current.openQuickMenu();
    });
    expect(result.current.isQuickMenuOpen).toBe(true);

    act(() => {
      result.current.closeQuickMenu();
    });
    expect(result.current.isQuickMenuOpen).toBe(false);
  });

  it('should toggle quick menu', () => {
    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('library'),
    });

    expect(result.current.isQuickMenuOpen).toBe(false);

    act(() => {
      result.current.toggleQuickMenu();
    });
    expect(result.current.isQuickMenuOpen).toBe(true);

    act(() => {
      result.current.toggleQuickMenu();
    });
    expect(result.current.isQuickMenuOpen).toBe(false);
  });

  it('should have quick menu items for library context', () => {
    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('library'),
    });

    expect(result.current.config?.quickMenuItems).toBeDefined();
    expect(result.current.config?.quickMenuItems.length).toBeGreaterThan(0);
  });

  it('should return game_detail config', () => {
    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('game_detail'),
    });

    expect(result.current.config?.icon).toBe('play');
    expect(result.current.config?.label).toBe('Inizia sessione');
  });
});

describe('useFAB actions', () => {
  it('should dispatch action event on triggerAction', () => {
    const eventHandler = vi.fn();
    window.addEventListener('fab:action', eventHandler);

    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('library'),
    });

    act(() => {
      result.current.triggerAction();
    });

    expect(eventHandler).toHaveBeenCalled();

    window.removeEventListener('fab:action', eventHandler);
  });

  it('should dispatch quick action event and close menu', () => {
    const eventHandler = vi.fn();
    window.addEventListener('fab:quickaction', eventHandler);

    const { result } = renderHook(() => useFAB(), {
      wrapper: createWrapper('library'),
    });

    // Open menu first
    act(() => {
      result.current.openQuickMenu();
    });
    expect(result.current.isQuickMenuOpen).toBe(true);

    // Trigger quick action
    act(() => {
      result.current.triggerQuickAction('search');
    });

    expect(eventHandler).toHaveBeenCalled();
    expect(result.current.isQuickMenuOpen).toBe(false);

    window.removeEventListener('fab:quickaction', eventHandler);
  });
});
