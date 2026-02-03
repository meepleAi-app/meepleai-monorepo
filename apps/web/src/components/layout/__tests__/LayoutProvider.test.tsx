/**
 * LayoutProvider Tests
 * Issue #3287 - Phase 1: Core Layout Structure
 */

import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { type ReactNode } from 'react';

import {
  LayoutProvider,
  useLayout,
  useLayoutResponsive,
  useLayoutFAB,
  useLayoutActionBar,
  useLayoutMultiSelect,
} from '../LayoutProvider';
import type { LayoutContext, Action } from '@/types/layout';

// Mock useResponsive hook
vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => ({
    deviceType: 'desktop',
    isMobile: false,
    isTablet: false,
    isDesktop: true,
    viewportWidth: 1024,
  }),
}));

// Helper to wrap hooks with provider
function createWrapper(initialContext?: LayoutContext) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <LayoutProvider initialContext={initialContext}>
        {children}
      </LayoutProvider>
    );
  };
}

describe('LayoutProvider', () => {
  describe('initial state', () => {
    it('should provide default context value', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      expect(result.current.context).toBe('default');
      expect(result.current.fab.visible).toBe(true);
      expect(result.current.actionBar.visible).toBe(true);
      expect(result.current.multiSelect.isActive).toBe(false);
      expect(result.current.isMenuOpen).toBe(false);
    });

    it('should accept initial context', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper('library'),
      });

      expect(result.current.context).toBe('library');
    });
  });

  describe('context management', () => {
    it('should allow setting context', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      act(() => {
        result.current.setContext('game_detail');
      });

      expect(result.current.context).toBe('game_detail');
    });

    it('should reset multi-select when context changes', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      // Enable multi-select and select items
      act(() => {
        result.current.toggleMultiSelect(true);
        result.current.addToSelection('item-1');
        result.current.addToSelection('item-2');
      });

      expect(result.current.multiSelect.isActive).toBe(true);
      expect(result.current.multiSelect.selectedIds).toHaveLength(2);

      // Change context
      act(() => {
        result.current.setContext('library');
      });

      expect(result.current.multiSelect.isActive).toBe(false);
      expect(result.current.multiSelect.selectedIds).toHaveLength(0);
    });
  });

  describe('FAB configuration', () => {
    it('should update FAB config', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      act(() => {
        result.current.setFABConfig({ visible: false, bottomOffset: 100 });
      });

      expect(result.current.fab.visible).toBe(false);
      expect(result.current.fab.bottomOffset).toBe(100);
    });

    it('should preserve existing FAB config when updating partially', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      const initialRightOffset = result.current.fab.rightOffset;

      act(() => {
        result.current.setFABConfig({ visible: false });
      });

      expect(result.current.fab.visible).toBe(false);
      expect(result.current.fab.rightOffset).toBe(initialRightOffset);
    });
  });

  describe('ActionBar configuration', () => {
    it('should register actions', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      const mockActions: Action[] = [
        {
          id: 'action-1',
          label: 'Action 1',
          icon: vi.fn() as unknown as Action['icon'],
          onClick: vi.fn(),
          priority: 1,
        },
        {
          id: 'action-2',
          label: 'Action 2',
          icon: vi.fn() as unknown as Action['icon'],
          onClick: vi.fn(),
          priority: 2,
        },
      ];

      act(() => {
        result.current.registerActions(mockActions);
      });

      expect(result.current.actionBar.actions).toHaveLength(2);
      expect(result.current.actionBar.actions[0].id).toBe('action-1');
    });

    it('should clear actions', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      const mockActions: Action[] = [
        {
          id: 'action-1',
          label: 'Action 1',
          icon: vi.fn() as unknown as Action['icon'],
          onClick: vi.fn(),
          priority: 1,
        },
      ];

      act(() => {
        result.current.registerActions(mockActions);
      });

      expect(result.current.actionBar.actions).toHaveLength(1);

      act(() => {
        result.current.clearActions();
      });

      expect(result.current.actionBar.actions).toHaveLength(0);
    });
  });

  describe('multi-select management', () => {
    it('should toggle multi-select mode', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      expect(result.current.multiSelect.isActive).toBe(false);

      act(() => {
        result.current.toggleMultiSelect();
      });

      expect(result.current.multiSelect.isActive).toBe(true);

      act(() => {
        result.current.toggleMultiSelect();
      });

      expect(result.current.multiSelect.isActive).toBe(false);
    });

    it('should add and remove items from selection', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      act(() => {
        result.current.toggleMultiSelect(true);
        result.current.addToSelection('item-1');
        result.current.addToSelection('item-2');
      });

      expect(result.current.multiSelect.selectedIds).toEqual(['item-1', 'item-2']);

      act(() => {
        result.current.removeFromSelection('item-1');
      });

      expect(result.current.multiSelect.selectedIds).toEqual(['item-2']);
    });

    it('should not add duplicate items', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      act(() => {
        result.current.addToSelection('item-1');
        result.current.addToSelection('item-1');
      });

      expect(result.current.multiSelect.selectedIds).toHaveLength(1);
    });

    it('should clear selection', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      act(() => {
        result.current.toggleMultiSelect(true);
        result.current.addToSelection('item-1');
        result.current.addToSelection('item-2');
      });

      act(() => {
        result.current.clearSelection();
      });

      expect(result.current.multiSelect.selectedIds).toHaveLength(0);
      expect(result.current.multiSelect.isActive).toBe(false);
    });

    it('should select all items', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      const allIds = ['item-1', 'item-2', 'item-3'];

      act(() => {
        result.current.selectAll(allIds);
      });

      expect(result.current.multiSelect.selectedIds).toEqual(allIds);
      expect(result.current.multiSelect.totalCount).toBe(3);
    });
  });

  describe('menu state', () => {
    it('should toggle menu', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      expect(result.current.isMenuOpen).toBe(false);

      act(() => {
        result.current.toggleMenu();
      });

      expect(result.current.isMenuOpen).toBe(true);

      act(() => {
        result.current.toggleMenu(false);
      });

      expect(result.current.isMenuOpen).toBe(false);
    });
  });

  describe('breadcrumbs', () => {
    it('should set breadcrumbs', () => {
      const { result } = renderHook(() => useLayout(), {
        wrapper: createWrapper(),
      });

      const breadcrumbs = [
        { label: 'Home', href: '/' },
        { label: 'Library', href: '/library' },
        { label: 'Game', isCurrent: true },
      ];

      act(() => {
        result.current.setBreadcrumbs(breadcrumbs);
      });

      expect(result.current.breadcrumbs).toEqual(breadcrumbs);
    });
  });

  describe('specialized hooks', () => {
    it('useLayoutResponsive should return only responsive state', () => {
      const { result } = renderHook(() => useLayoutResponsive(), {
        wrapper: createWrapper(),
      });

      expect(result.current).toHaveProperty('isMobile');
      expect(result.current).toHaveProperty('isTablet');
      expect(result.current).toHaveProperty('isDesktop');
      expect(result.current).toHaveProperty('deviceType');
    });

    it('useLayoutFAB should return FAB state and setter', () => {
      const { result } = renderHook(() => useLayoutFAB(), {
        wrapper: createWrapper(),
      });

      expect(result.current.fab).toBeDefined();
      expect(result.current.setFABConfig).toBeInstanceOf(Function);
    });

    it('useLayoutActionBar should return ActionBar state and functions', () => {
      const { result } = renderHook(() => useLayoutActionBar(), {
        wrapper: createWrapper(),
      });

      expect(result.current.actionBar).toBeDefined();
      expect(result.current.setActionBarConfig).toBeInstanceOf(Function);
      expect(result.current.registerActions).toBeInstanceOf(Function);
      expect(result.current.clearActions).toBeInstanceOf(Function);
    });

    it('useLayoutMultiSelect should return multi-select state and actions', () => {
      const { result } = renderHook(() => useLayoutMultiSelect(), {
        wrapper: createWrapper(),
      });

      expect(result.current.multiSelect).toBeDefined();
      expect(result.current.toggleMultiSelect).toBeInstanceOf(Function);
      expect(result.current.addToSelection).toBeInstanceOf(Function);
      expect(result.current.removeFromSelection).toBeInstanceOf(Function);
      expect(result.current.clearSelection).toBeInstanceOf(Function);
      expect(result.current.selectAll).toBeInstanceOf(Function);
    });
  });

  describe('error handling', () => {
    it('should throw error when useLayout is used outside provider', () => {
      // Suppress console.error for this test
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      expect(() => {
        renderHook(() => useLayout());
      }).toThrow('useLayout must be used within a LayoutProvider');

      consoleSpy.mockRestore();
    });
  });
});
