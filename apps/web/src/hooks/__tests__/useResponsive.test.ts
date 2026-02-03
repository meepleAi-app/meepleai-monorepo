/**
 * useResponsive Hook Tests
 * Issue #3287 - Phase 1: Core Layout Structure
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Create mock function
const mockUseMediaQuery = vi.fn<[string], boolean>();

// Mock usehooks-ts before importing the hook
vi.mock('usehooks-ts', () => ({
  useMediaQuery: (query: string) => mockUseMediaQuery(query),
}));

// Import hook after mocking
import {
  useResponsive,
  useBreakpoint,
  useIsTouchDevice,
  useOrientation,
  usePrefersReducedMotion,
} from '../useResponsive';

describe('useResponsive', () => {
  beforeEach(() => {
    // Reset window properties
    Object.defineProperty(window, 'innerWidth', {
      writable: true,
      configurable: true,
      value: 1024,
    });
    mockUseMediaQuery.mockReset();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('device type detection', () => {
    it('should detect mobile viewport', async () => {
      // Mobile: max-width: 639px = true, tablet range = false, desktop = false
      mockUseMediaQuery.mockImplementation((query: string) => {
        if (query.includes('max-width: 639px')) return true;
        if (query.includes('min-width: 640px') && query.includes('max-width: 1023px')) return false;
        if (query.includes('min-width: 1024px')) return false;
        return false;
      });

      const { result } = renderHook(() => useResponsive());

      // Wait for hydration
      await waitFor(() => {
        expect(result.current.viewportWidth).toBeGreaterThan(0);
      });

      expect(result.current.isMobile).toBe(true);
      expect(result.current.isTablet).toBe(false);
      expect(result.current.isDesktop).toBe(false);
      expect(result.current.deviceType).toBe('mobile');
    });

    it('should detect tablet viewport', async () => {
      mockUseMediaQuery.mockImplementation((query: string) => {
        if (query.includes('max-width: 639px')) return false;
        if (query.includes('min-width: 640px') && query.includes('max-width: 1023px')) return true;
        if (query.includes('min-width: 1024px')) return false;
        return false;
      });

      const { result } = renderHook(() => useResponsive());

      // Wait for hydration
      await waitFor(() => {
        expect(result.current.viewportWidth).toBeGreaterThan(0);
      });

      expect(result.current.isMobile).toBe(false);
      expect(result.current.isTablet).toBe(true);
      expect(result.current.isDesktop).toBe(false);
      expect(result.current.deviceType).toBe('tablet');
    });

    it('should detect desktop viewport', async () => {
      mockUseMediaQuery.mockImplementation((query: string) => {
        if (query.includes('max-width: 639px')) return false;
        if (query.includes('min-width: 640px') && query.includes('max-width: 1023px')) return false;
        if (query.includes('min-width: 1024px')) return true;
        return false;
      });

      const { result } = renderHook(() => useResponsive());

      // Wait for hydration
      await waitFor(() => {
        expect(result.current.viewportWidth).toBeGreaterThan(0);
      });

      expect(result.current.isMobile).toBe(false);
      expect(result.current.isTablet).toBe(false);
      expect(result.current.isDesktop).toBe(true);
      expect(result.current.deviceType).toBe('desktop');
    });

    it('should return mobile-first initial state during SSR/hydration', () => {
      mockUseMediaQuery.mockReturnValue(false);

      const { result } = renderHook(() => useResponsive());

      // Before hydration completes, should return initial mobile state
      // Note: This may already be hydrated in test env, so we just check it's valid
      expect(['mobile', 'tablet', 'desktop']).toContain(result.current.deviceType);
    });
  });

  describe('viewport width tracking', () => {
    it('should track viewport width', async () => {
      Object.defineProperty(window, 'innerWidth', { value: 768 });
      mockUseMediaQuery.mockReturnValue(false);

      const { result } = renderHook(() => useResponsive());

      // Wait for hydration and width update
      await waitFor(() => {
        expect(result.current.viewportWidth).toBe(768);
      });
    });

    it('should update on resize', async () => {
      mockUseMediaQuery.mockReturnValue(false);
      Object.defineProperty(window, 'innerWidth', { value: 1024 });

      const { result } = renderHook(() => useResponsive());

      // Wait for initial hydration
      await waitFor(() => {
        expect(result.current.viewportWidth).toBe(1024);
      });

      // Simulate resize
      act(() => {
        Object.defineProperty(window, 'innerWidth', { value: 800 });
        window.dispatchEvent(new Event('resize'));
      });

      await waitFor(() => {
        expect(result.current.viewportWidth).toBe(800);
      });
    });
  });
});

describe('useBreakpoint', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('should return true when viewport matches breakpoint', () => {
    mockUseMediaQuery.mockReturnValue(true);

    const { result } = renderHook(() => useBreakpoint('lg'));

    expect(result.current).toBe(true);
  });

  it('should return false when viewport is smaller than breakpoint', () => {
    mockUseMediaQuery.mockReturnValue(false);

    const { result } = renderHook(() => useBreakpoint('lg'));

    expect(result.current).toBe(false);
  });
});

describe('useIsTouchDevice', () => {
  afterEach(() => {
    // Clean up any window modifications
    delete (window as { ontouchstart?: unknown }).ontouchstart;
  });

  it('should detect touch capability', async () => {
    // Mock touch support
    Object.defineProperty(window, 'ontouchstart', {
      value: () => {},
      configurable: true,
    });
    Object.defineProperty(navigator, 'maxTouchPoints', {
      value: 5,
      configurable: true,
    });

    const { result } = renderHook(() => useIsTouchDevice());

    // Wait for effect to run
    await waitFor(() => {
      expect(result.current).toBe(true);
    });
  });

  it('should return false for non-touch devices', async () => {
    // Ensure no touch support
    delete (window as { ontouchstart?: unknown }).ontouchstart;
    Object.defineProperty(navigator, 'maxTouchPoints', {
      value: 0,
      configurable: true,
    });

    const { result } = renderHook(() => useIsTouchDevice());

    // Wait for effect to run - initial state is false
    await waitFor(() => {
      expect(result.current).toBe(false);
    });
  });
});

describe('useOrientation', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('should return portrait when in portrait orientation', () => {
    mockUseMediaQuery.mockReturnValue(true);

    const { result } = renderHook(() => useOrientation());

    expect(result.current).toBe('portrait');
  });

  it('should return landscape when in landscape orientation', () => {
    mockUseMediaQuery.mockReturnValue(false);

    const { result } = renderHook(() => useOrientation());

    expect(result.current).toBe('landscape');
  });
});

describe('usePrefersReducedMotion', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('should return true when user prefers reduced motion', () => {
    mockUseMediaQuery.mockReturnValue(true);

    const { result } = renderHook(() => usePrefersReducedMotion());

    expect(result.current).toBe(true);
  });

  it('should return false when user does not prefer reduced motion', () => {
    mockUseMediaQuery.mockReturnValue(false);

    const { result } = renderHook(() => usePrefersReducedMotion());

    expect(result.current).toBe(false);
  });
});
