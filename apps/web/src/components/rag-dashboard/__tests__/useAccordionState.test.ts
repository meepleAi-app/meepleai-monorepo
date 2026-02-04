/**
 * Tests for useAccordionState hook
 * Issue #3449: Accordion system with localStorage persistence
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useAccordionState } from '../hooks/useAccordionState';

describe('useAccordionState', () => {
  const STORAGE_KEY = 'rag-dashboard-accordion-state';

  // Mock localStorage
  const localStorageMock = (() => {
    let store: Record<string, string> = {};
    return {
      getItem: vi.fn((key: string) => store[key] || null),
      setItem: vi.fn((key: string, value: string) => {
        store[key] = value;
      }),
      removeItem: vi.fn((key: string) => {
        delete store[key];
      }),
      clear: vi.fn(() => {
        store = {};
      }),
    };
  })();

  beforeEach(() => {
    Object.defineProperty(window, 'localStorage', {
      value: localStorageMock,
      writable: true,
    });
    localStorageMock.clear();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Initialization Tests
  // =========================================================================

  describe('Initialization', () => {
    it('should initialize with default open sections', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['overview', 'query-sim'] })
      );

      expect(result.current.openSections).toEqual(['overview', 'query-sim']);
    });

    it('should initialize with empty array when no defaults provided', () => {
      const { result } = renderHook(() => useAccordionState());

      expect(result.current.openSections).toEqual([]);
    });

    it('should load state from localStorage if available', () => {
      localStorageMock.getItem.mockReturnValueOnce(JSON.stringify(['saved-section']));

      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['default-section'] })
      );

      // Wait for useEffect to run
      expect(result.current.openSections).toEqual(['saved-section']);
    });

    it('should use defaults if localStorage is empty', () => {
      localStorageMock.getItem.mockReturnValueOnce(null);

      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['default-section'] })
      );

      expect(result.current.openSections).toEqual(['default-section']);
    });

    it('should use defaults if localStorage contains invalid JSON', () => {
      localStorageMock.getItem.mockReturnValueOnce('invalid-json');

      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['default-section'] })
      );

      expect(result.current.openSections).toEqual(['default-section']);
    });
  });

  // =========================================================================
  // Toggle Tests
  // =========================================================================

  describe('toggleSection', () => {
    it('should open a closed section', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: [] })
      );

      act(() => {
        result.current.toggleSection('new-section');
      });

      expect(result.current.openSections).toContain('new-section');
    });

    it('should close an open section', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['open-section'] })
      );

      act(() => {
        result.current.toggleSection('open-section');
      });

      expect(result.current.openSections).not.toContain('open-section');
    });

    it('should allow multiple sections to be open', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['section-1'] })
      );

      act(() => {
        result.current.toggleSection('section-2');
      });

      expect(result.current.openSections).toContain('section-1');
      expect(result.current.openSections).toContain('section-2');
    });
  });

  // =========================================================================
  // isOpen Tests
  // =========================================================================

  describe('isOpen', () => {
    it('should return true for open sections', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['open-section'] })
      );

      expect(result.current.isOpen('open-section')).toBe(true);
    });

    it('should return false for closed sections', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: [] })
      );

      expect(result.current.isOpen('closed-section')).toBe(false);
    });
  });

  // =========================================================================
  // setOpen Tests
  // =========================================================================

  describe('setOpen', () => {
    it('should set multiple sections to open', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: [] })
      );

      act(() => {
        result.current.setOpen(['section-1', 'section-2', 'section-3']);
      });

      expect(result.current.openSections).toEqual(['section-1', 'section-2', 'section-3']);
    });

    it('should replace existing open sections', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['old-section'] })
      );

      act(() => {
        result.current.setOpen(['new-section']);
      });

      expect(result.current.openSections).toEqual(['new-section']);
      expect(result.current.openSections).not.toContain('old-section');
    });
  });

  // =========================================================================
  // openSection/closeSection Tests
  // =========================================================================

  describe('openSection', () => {
    it('should open a closed section', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: [] })
      );

      act(() => {
        result.current.openSection('new-section');
      });

      expect(result.current.openSections).toContain('new-section');
    });

    it('should not duplicate already open sections', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['open-section'] })
      );

      act(() => {
        result.current.openSection('open-section');
      });

      const count = result.current.openSections.filter(s => s === 'open-section').length;
      expect(count).toBe(1);
    });
  });

  describe('closeSection', () => {
    it('should close an open section', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['open-section'] })
      );

      act(() => {
        result.current.closeSection('open-section');
      });

      expect(result.current.openSections).not.toContain('open-section');
    });

    it('should not affect already closed sections', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['other-section'] })
      );

      const before = [...result.current.openSections];

      act(() => {
        result.current.closeSection('not-open');
      });

      expect(result.current.openSections).toEqual(before);
    });
  });

  // =========================================================================
  // resetToDefaults Tests
  // =========================================================================

  describe('resetToDefaults', () => {
    it('should reset to default open sections', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['default-1', 'default-2'] })
      );

      // Change state
      act(() => {
        result.current.setOpen(['changed-1', 'changed-2']);
      });

      expect(result.current.openSections).toEqual(['changed-1', 'changed-2']);

      // Reset
      act(() => {
        result.current.resetToDefaults();
      });

      expect(result.current.openSections).toEqual(['default-1', 'default-2']);
    });

    it('should clear localStorage on reset', () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: ['default'] })
      );

      act(() => {
        result.current.resetToDefaults();
      });

      expect(localStorageMock.removeItem).toHaveBeenCalledWith(STORAGE_KEY);
    });
  });

  // =========================================================================
  // Persistence Tests
  // =========================================================================

  describe('localStorage persistence', () => {
    it('should save state to localStorage after toggle', async () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: [] })
      );

      // Wait for initialization
      await act(async () => {
        await Promise.resolve();
      });

      act(() => {
        result.current.toggleSection('new-section');
      });

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        STORAGE_KEY,
        JSON.stringify(['new-section'])
      );
    });

    it('should use custom storage key when provided', async () => {
      const customKey = 'custom-accordion-key';
      const { result } = renderHook(() =>
        useAccordionState({
          defaultOpen: [],
          storageKey: customKey,
        })
      );

      // Wait for initialization
      await act(async () => {
        await Promise.resolve();
      });

      act(() => {
        result.current.toggleSection('section');
      });

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        customKey,
        expect.any(String)
      );
    });
  });

  // =========================================================================
  // isInitialized Tests
  // =========================================================================

  describe('isInitialized', () => {
    it('should be true after localStorage load', async () => {
      const { result } = renderHook(() =>
        useAccordionState({ defaultOpen: [] })
      );

      // Wait for effect to run
      await act(async () => {
        await Promise.resolve();
      });

      expect(result.current.isInitialized).toBe(true);
    });
  });
});
