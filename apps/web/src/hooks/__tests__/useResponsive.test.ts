/**
 * useResponsive Hook Tests
 * Issue #3287 - Phase 1: Core Layout Structure
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Define types for mocked module
type MediaQueryMock = (query: string) => boolean;

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
    it('should detect mobile viewport', () => {
      // Mobile: < 640px, so isSmall=true, isMedium=false, isLarge=false
      mockUseMediaQuery.mockImplementation((query: string) => {
        if (query.includes('max-width: 639px')) return true;
        if (query.includes('min-width: 640px') && query.includes('max-width: 1023px')) return false;
        if (query.includes('min-width: 1024px')) return false;
        return false;
      });

      const { result } = renderHook(() => useResponsive());

      expect(result.current.isMobile).toBe(true);
      expect(result.current.isTablet).toBe(false);
      expect(result.current.isDesktop).toBe(false);
      expect(result.current.deviceType).toBe('mobile');
    });

    it('should detect tablet viewport', () => {
      mockUseMediaQuery.mockImplementation((query: string) => {
        if (query.includes('max-width: 639px')) return false;
        if (query.includes('min-width: 640px') && query.includes('max-width: 1023px')) return true;
        if (query.includes('min-width: 1024px')) return false;
        return false;
      });

      const { result } = renderHook(() => useResponsive());

      expect(result.current.isMobile).toBe(false);
      expect(result.current.isTablet).toBe(true);
      expect(result.current.isDesktop).toBe(false);
      expect(result.current.deviceType).toBe('tablet');
    });

    it('should detect desktop viewport', () => {
      mockUseMediaQuery.mockImplementation((query: string) => {
        if (query.includes('max-width: 639px')) return false;
        if (query.includes('min-width: 640px') && query.includes('max-width: 1023px')) return false;
        if (query.includes('min-width: 1024px')) return true;
        return false;
      });

      const { result } = renderHook(() => useResponsive());

      expect(result.current.isMobile).toBe(false);
      expect(result.current.isTablet).toBe(false);
      expect(result.current.isDesktop).toBe(true);
      expect(result.current.deviceType).toBe('desktop');
    });
  });

  describe('viewport width tracking', () => {
    it('should track viewport width', () => {
      Object.defineProperty(window, 'innerWidth', { value: 768 });
      mockUseMediaQuery.mockReturnValue(false);

      const { result } = renderHook(() => useResponsive());

      expect(result.current.viewportWidth).toBe(768);
    });

    it('should update on resize', () => {
      mockUseMediaQuery.mockReturnValue(false);
      Object.defineProperty(window, 'innerWidth', { value: 1024 });

      const { result } = renderHook(() => useResponsive());

      // Initial value
      expect(result.current.viewportWidth).toBe(1024);

      // Simulate resize
      act(() => {
        Object.defineProperty(window, 'innerWidth', { value: 800 });
        window.dispatchEvent(new Event('resize'));
      });

      expect(result.current.viewportWidth).toBe(800);
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

  it('should detect touch capability', () => {
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

    expect(result.current).toBe(true);
  });

  it('should return false for non-touch devices', () => {
    Object.defineProperty(navigator, 'maxTouchPoints', {
      value: 0,
      configurable: true,
    });

    const { result } = renderHook(() => useIsTouchDevice());

    expect(result.current).toBe(false);
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
