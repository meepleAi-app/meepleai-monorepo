/**
 * NavigationContext Tests
 * Issue #5034 - Navigation Config System
 *
 * Tests: NavigationProvider, useNavigation, useSetNavConfig
 */

import { act, renderHook } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import type { ReactNode } from 'react';
import { Home, Plus } from 'lucide-react';

import { NavigationProvider, useNavigation, useSetNavConfig } from '../NavigationContext';
import type { NavAction, NavTab, PageNavConfig } from '@/types/navigation';

// ─── Test Fixtures ────────────────────────────────────────────────────────────

const TAB_GAMES: NavTab = {
  id: 'games',
  label: 'Games',
  href: '/library',
  icon: Home,
};

const TAB_WISHLIST: NavTab = {
  id: 'wishlist',
  label: 'Wishlist',
  href: '/library?tab=wishlist',
  badge: 3,
};

const ACTION_ADD: NavAction = {
  id: 'add-game',
  label: 'Add Game',
  icon: Plus,
  onClick: vi.fn(),
  variant: 'primary',
};

const ACTION_FILTER: NavAction = {
  id: 'filter',
  label: 'Filter',
  icon: Home,
  onClick: vi.fn(),
  variant: 'secondary',
};

// ─── Helper Wrapper ───────────────────────────────────────────────────────────

function wrapper({ children }: { children: ReactNode }) {
  return <NavigationProvider>{children}</NavigationProvider>;
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('NavigationProvider + useNavigation', () => {
  it('provides empty default state', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    expect(result.current.miniNavTabs).toEqual([]);
    expect(result.current.actionBarActions).toEqual([]);
    expect(result.current.activeZone).toBeNull();
  });

  it('exposes setNavConfig and clearNavConfig functions', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    expect(typeof result.current.setNavConfig).toBe('function');
    expect(typeof result.current.clearNavConfig).toBe('function');
  });

  it('updates tabs and actions when setNavConfig is called', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    act(() => {
      result.current.setNavConfig({
        miniNav: [TAB_GAMES, TAB_WISHLIST],
        actionBar: [ACTION_ADD, ACTION_FILTER],
      });
    });

    expect(result.current.miniNavTabs).toHaveLength(2);
    expect(result.current.miniNavTabs[0]).toEqual(TAB_GAMES);
    expect(result.current.miniNavTabs[1]).toEqual(TAB_WISHLIST);
    expect(result.current.actionBarActions).toHaveLength(2);
    expect(result.current.actionBarActions[0]).toEqual(ACTION_ADD);
  });

  it('sets zone when provided', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    act(() => {
      result.current.setNavConfig({
        miniNav: [TAB_GAMES],
        zone: 'game-detail/agent',
      });
    });

    expect(result.current.activeZone).toBe('game-detail/agent');
  });

  it('defaults zone to null when not provided', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    act(() => {
      result.current.setNavConfig({ miniNav: [TAB_GAMES] });
    });

    expect(result.current.activeZone).toBeNull();
  });

  it('clears config when clearNavConfig is called', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    act(() => {
      result.current.setNavConfig({
        miniNav: [TAB_GAMES],
        actionBar: [ACTION_ADD],
        zone: 'some-zone',
      });
    });

    act(() => {
      result.current.clearNavConfig();
    });

    expect(result.current.miniNavTabs).toEqual([]);
    expect(result.current.actionBarActions).toEqual([]);
    expect(result.current.activeZone).toBeNull();
  });

  it('replaces previous config on each setNavConfig call', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    act(() => {
      result.current.setNavConfig({ miniNav: [TAB_GAMES, TAB_WISHLIST] });
    });

    act(() => {
      result.current.setNavConfig({ miniNav: [TAB_WISHLIST] });
    });

    expect(result.current.miniNavTabs).toHaveLength(1);
    expect(result.current.miniNavTabs[0].id).toBe('wishlist');
  });

  it('handles empty miniNav as empty array', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    act(() => {
      result.current.setNavConfig({ miniNav: [] });
    });

    expect(result.current.miniNavTabs).toEqual([]);
  });

  it('handles config with only actionBar', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    act(() => {
      result.current.setNavConfig({ actionBar: [ACTION_ADD] });
    });

    expect(result.current.miniNavTabs).toEqual([]);
    expect(result.current.actionBarActions).toHaveLength(1);
    expect(result.current.actionBarActions[0].id).toBe('add-game');
  });
});

describe('useSetNavConfig', () => {
  it('returns a function that updates navigation config', () => {
    const { result } = renderHook(() => ({
      nav: useNavigation(),
      setter: useSetNavConfig(),
    }), { wrapper });

    act(() => {
      result.current.setter({
        miniNav: [TAB_GAMES],
        actionBar: [ACTION_ADD],
      });
    });

    expect(result.current.nav.miniNavTabs).toHaveLength(1);
    expect(result.current.nav.actionBarActions).toHaveLength(1);
  });

  it('setter is a stable function reference', () => {
    const { result, rerender } = renderHook(() => useSetNavConfig(), { wrapper });

    const firstRef = result.current;
    rerender();
    const secondRef = result.current;

    expect(firstRef).toBe(secondRef);
  });
});

describe('NavTab type validation', () => {
  it('tabs preserve all properties', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    const tabWithAll: NavTab = {
      id: 'test',
      label: 'Test',
      href: '/test',
      icon: Home,
      badge: 42,
    };

    act(() => {
      result.current.setNavConfig({ miniNav: [tabWithAll] });
    });

    const tab = result.current.miniNavTabs[0];
    expect(tab.id).toBe('test');
    expect(tab.label).toBe('Test');
    expect(tab.href).toBe('/test');
    expect(tab.icon).toBe(Home);
    expect(tab.badge).toBe(42);
  });
});

describe('NavAction type validation', () => {
  it('actions preserve all properties including disabled state', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    const disabledAction: NavAction = {
      id: 'save',
      label: 'Save',
      icon: Home,
      onClick: vi.fn(),
      variant: 'primary',
      disabled: true,
      disabledTooltip: 'No changes to save',
      hidden: false,
    };

    act(() => {
      result.current.setNavConfig({ actionBar: [disabledAction] });
    });

    const action = result.current.actionBarActions[0];
    expect(action.disabled).toBe(true);
    expect(action.disabledTooltip).toBe('No changes to save');
    expect(action.variant).toBe('primary');
  });
});

describe('zone-aware navigation', () => {
  it('tracks zone changes independently from tabs', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    const config: PageNavConfig = {
      miniNav: [TAB_GAMES],
      actionBar: [ACTION_ADD],
      zone: 'library/game-detail',
    };

    act(() => {
      result.current.setNavConfig(config);
    });

    expect(result.current.activeZone).toBe('library/game-detail');
    expect(result.current.miniNavTabs[0].id).toBe('games');
  });

  it('can update zone without changing tabs', () => {
    const { result } = renderHook(() => useNavigation(), { wrapper });

    act(() => {
      result.current.setNavConfig({ miniNav: [TAB_GAMES] });
    });

    act(() => {
      result.current.setNavConfig({ miniNav: [TAB_GAMES], zone: 'overview' });
    });

    expect(result.current.activeZone).toBe('overview');
    expect(result.current.miniNavTabs[0].id).toBe('games');
  });
});
