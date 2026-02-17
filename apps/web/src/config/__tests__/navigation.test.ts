/**
 * Tests for unified navigation configuration
 * Validates filterNavItemsByRole, isUnifiedNavItemActive, and NAV_ITEMS consistency
 */

import { describe, it, expect } from 'vitest';

import {
  UNIFIED_NAV_ITEMS,
  NAV_ITEMS,
  filterNavItemsByRole,
  isUnifiedNavItemActive,
  isNavItemActive,
  getNavItemsForBreakpoint,
  getOverflowNavItems,
  getContextActionSlots,
} from '../navigation';

describe('UNIFIED_NAV_ITEMS', () => {
  it('has unique ids', () => {
    const ids = UNIFIED_NAV_ITEMS.map(item => item.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('has unique priorities', () => {
    const priorities = UNIFIED_NAV_ITEMS.map(item => item.priority);
    expect(new Set(priorities).size).toBe(priorities.length);
  });

  it('contains expected items', () => {
    const ids = UNIFIED_NAV_ITEMS.map(item => item.id);
    expect(ids).toContain('welcome');
    expect(ids).toContain('dashboard');
    expect(ids).toContain('library');
    expect(ids).toContain('chat');
    expect(ids).toContain('catalog');
    expect(ids).toContain('profile');
    expect(ids).toContain('agents');
    expect(ids).toContain('sessions');
  });

  it('library item has children', () => {
    const library = UNIFIED_NAV_ITEMS.find(item => item.id === 'library');
    expect(library?.children).toBeDefined();
    expect(library!.children!.length).toBeGreaterThan(0);
  });

  it('catalog has no visibility (visible to all)', () => {
    const catalog = UNIFIED_NAV_ITEMS.find(item => item.id === 'catalog');
    expect(catalog?.visibility).toBeUndefined();
  });

  it('welcome is anonOnly', () => {
    const welcome = UNIFIED_NAV_ITEMS.find(item => item.id === 'welcome');
    expect(welcome?.visibility?.anonOnly).toBe(true);
  });

  it('dashboard is authOnly', () => {
    const dashboard = UNIFIED_NAV_ITEMS.find(item => item.id === 'dashboard');
    expect(dashboard?.visibility?.authOnly).toBe(true);
  });

  it('profile has hideFromMainNav flag', () => {
    const profile = UNIFIED_NAV_ITEMS.find(item => item.id === 'profile');
    expect(profile?.hideFromMainNav).toBe(true);
  });

  it('agents and sessions have group strumenti', () => {
    const agents = UNIFIED_NAV_ITEMS.find(item => item.id === 'agents');
    const sessions = UNIFIED_NAV_ITEMS.find(item => item.id === 'sessions');
    expect(agents?.group).toBe('strumenti');
    expect(sessions?.group).toBe('strumenti');
  });
});

describe('filterNavItemsByRole', () => {
  it('returns only non-auth items when not authenticated', () => {
    const result = filterNavItemsByRole(UNIFIED_NAV_ITEMS, {
      isAuthenticated: false,
    });

    const ids = result.map(item => item.id);
    expect(ids).toContain('welcome');
    expect(ids).toContain('catalog');
    expect(ids).not.toContain('dashboard');
    expect(ids).not.toContain('library');
    expect(ids).not.toContain('profile');
  });

  it('returns auth items and hides anon items when authenticated', () => {
    const result = filterNavItemsByRole(UNIFIED_NAV_ITEMS, {
      isAuthenticated: true,
      userRole: 'User',
    });

    const ids = result.map(item => item.id);
    expect(ids).not.toContain('welcome');
    expect(ids).toContain('dashboard');
    expect(ids).toContain('catalog');
    expect(ids).toContain('library');
    expect(ids).toContain('profile');
    expect(ids).toContain('agents');
    expect(ids).toContain('sessions');
  });

  it('returns only non-restricted items while auth is loading', () => {
    const result = filterNavItemsByRole(UNIFIED_NAV_ITEMS, {
      isAuthenticated: false,
      isAuthLoading: true,
    });

    const ids = result.map(item => item.id);
    // Only catalog (no auth restrictions)
    expect(ids).toContain('catalog');
    expect(ids).not.toContain('welcome');
    expect(ids).not.toContain('dashboard');
  });

  it('filters by minRole = Admin', () => {
    const itemsWithAdmin = [
      ...UNIFIED_NAV_ITEMS,
      {
        id: 'admin-only',
        href: '/admin',
        icon: UNIFIED_NAV_ITEMS[0].icon,
        iconName: 'shield',
        label: 'Admin',
        ariaLabel: 'Admin',
        priority: 99,
        testId: 'nav-admin',
        visibility: { authOnly: true as const, minRole: 'Admin' as const },
      },
    ];

    // Regular user should not see admin item
    const userResult = filterNavItemsByRole(itemsWithAdmin, {
      isAuthenticated: true,
      userRole: 'User',
    });
    expect(userResult.map(i => i.id)).not.toContain('admin-only');

    // Admin should see admin item
    const adminResult = filterNavItemsByRole(itemsWithAdmin, {
      isAuthenticated: true,
      userRole: 'Admin',
    });
    expect(adminResult.map(i => i.id)).toContain('admin-only');

    // SuperAdmin should also see admin item
    const superAdminResult = filterNavItemsByRole(itemsWithAdmin, {
      isAuthenticated: true,
      userRole: 'SuperAdmin',
    });
    expect(superAdminResult.map(i => i.id)).toContain('admin-only');
  });

  it('filters by minRole = Editor', () => {
    const itemsWithEditor = [
      ...UNIFIED_NAV_ITEMS,
      {
        id: 'editor-only',
        href: '/editor',
        icon: UNIFIED_NAV_ITEMS[0].icon,
        iconName: 'edit',
        label: 'Editor',
        ariaLabel: 'Editor',
        priority: 98,
        testId: 'nav-editor',
        visibility: { authOnly: true as const, minRole: 'Editor' as const },
      },
    ];

    // Regular user should not see editor item
    const userResult = filterNavItemsByRole(itemsWithEditor, {
      isAuthenticated: true,
      userRole: 'User',
    });
    expect(userResult.map(i => i.id)).not.toContain('editor-only');

    // Editor should see editor item
    const editorResult = filterNavItemsByRole(itemsWithEditor, {
      isAuthenticated: true,
      userRole: 'Editor',
    });
    expect(editorResult.map(i => i.id)).toContain('editor-only');

    // Admin should also see editor item
    const adminResult = filterNavItemsByRole(itemsWithEditor, {
      isAuthenticated: true,
      userRole: 'Admin',
    });
    expect(adminResult.map(i => i.id)).toContain('editor-only');
  });
});

describe('isUnifiedNavItemActive', () => {
  it('matches exact path for welcome', () => {
    const welcome = UNIFIED_NAV_ITEMS.find(item => item.id === 'welcome')!;
    expect(isUnifiedNavItemActive(welcome, '/')).toBe(true);
    expect(isUnifiedNavItemActive(welcome, '/dashboard')).toBe(false);
  });

  it('matches exact path for dashboard', () => {
    const dashboard = UNIFIED_NAV_ITEMS.find(item => item.id === 'dashboard')!;
    expect(isUnifiedNavItemActive(dashboard, '/dashboard')).toBe(true);
    expect(isUnifiedNavItemActive(dashboard, '/dashboard/sub')).toBe(false);
  });

  it('matches prefix for library', () => {
    const library = UNIFIED_NAV_ITEMS.find(item => item.id === 'library')!;
    expect(isUnifiedNavItemActive(library, '/library')).toBe(true);
    expect(isUnifiedNavItemActive(library, '/library/private')).toBe(true);
    expect(isUnifiedNavItemActive(library, '/library/proposals')).toBe(true);
  });

  it('matches prefix for catalog', () => {
    const catalog = UNIFIED_NAV_ITEMS.find(item => item.id === 'catalog')!;
    expect(isUnifiedNavItemActive(catalog, '/games')).toBe(true);
    expect(isUnifiedNavItemActive(catalog, '/games/123')).toBe(true);
    expect(isUnifiedNavItemActive(catalog, '/library')).toBe(false);
  });
});

describe('Legacy NAV_ITEMS', () => {
  it('contains exactly 3 items for ActionBar', () => {
    expect(NAV_ITEMS).toHaveLength(3);
    const ids = NAV_ITEMS.map(item => item.id);
    expect(ids).toContain('home'); // dashboard mapped to "home"
    expect(ids).toContain('library');
    expect(ids).toContain('catalog');
  });

  it('does not contain chat or profile', () => {
    const ids = NAV_ITEMS.map(item => item.id);
    expect(ids).not.toContain('chat');
    expect(ids).not.toContain('profile');
  });

  it('uses string icons', () => {
    NAV_ITEMS.forEach(item => {
      expect(typeof item.icon).toBe('string');
    });
  });
});

describe('getNavItemsForBreakpoint', () => {
  it('returns max 3 items for mobile', () => {
    const items = getNavItemsForBreakpoint('mobile');
    expect(items.length).toBeLessThanOrEqual(3);
  });

  it('returns max 5 items for tablet', () => {
    const items = getNavItemsForBreakpoint('tablet');
    expect(items.length).toBeLessThanOrEqual(5);
  });

  it('returns items sorted by priority', () => {
    const items = getNavItemsForBreakpoint('desktop');
    for (let i = 1; i < items.length; i++) {
      expect(items[i].priority).toBeGreaterThanOrEqual(items[i - 1].priority);
    }
  });
});

describe('getOverflowNavItems', () => {
  it('returns items not shown on mobile', () => {
    const visible = getNavItemsForBreakpoint('mobile');
    const overflow = getOverflowNavItems('mobile');
    const allIds = [...visible, ...overflow].map(i => i.id);
    const navIds = NAV_ITEMS.map(i => i.id);
    expect(allIds.sort()).toEqual(navIds.sort());
  });
});

describe('isNavItemActive (legacy)', () => {
  it('matches exact path for home', () => {
    const home = NAV_ITEMS.find(item => item.id === 'home');
    if (home) {
      expect(isNavItemActive(home, '/dashboard')).toBe(true);
      expect(isNavItemActive(home, '/dashboard/sub')).toBe(false);
    }
  });
});

describe('getContextActionSlots', () => {
  it('returns positive number for mobile', () => {
    expect(getContextActionSlots('mobile')).toBeGreaterThan(0);
  });

  it('returns positive number for tablet', () => {
    expect(getContextActionSlots('tablet')).toBeGreaterThan(0);
  });
});
