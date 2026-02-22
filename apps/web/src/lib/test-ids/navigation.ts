/**
 * Centralized data-testid constants for Navigation components.
 * Import these in both the component and the test file to avoid magic strings.
 *
 * Issue #5095 — Centralize test magic strings
 */

export const NAV_TEST_IDS = {
  // ── MiniNav ────────────────────────────────────────────────────────────────

  /** MiniNav root element (tablist) */
  miniNav: 'mini-nav',

  /** MiniNav horizontal scroll-left button */
  miniNavScrollLeft: 'mini-nav-scroll-left',

  /** MiniNav tablist container */
  miniNavTablist: 'mini-nav-tablist',

  /** MiniNav horizontal scroll-right button */
  miniNavScrollRight: 'mini-nav-scroll-right',

  /**
   * Individual MiniNavTab by tab id.
   * @example NAV_TEST_IDS.miniNavTab('games') // => "mini-nav-tab-games"
   */
  miniNavTab: (id: string) => `mini-nav-tab-${id}` as const,

  // ── MobileNavDrawer ────────────────────────────────────────────────────────

  /** Hamburger trigger button */
  mobileNavTrigger: 'mobile-nav-trigger',

  /** Drawer sheet/panel when open */
  mobileNavDrawer: 'mobile-nav-drawer',

  /** Close button inside drawer */
  mobileNavClose: 'mobile-nav-close',

  /** Library toggle section in mobile drawer */
  mobileLibraryToggle: 'mobile-library-toggle',

  /**
   * Mobile nav item by href segments.
   * @example NAV_TEST_IDS.mobileNavItem('library') // => "mobile-nav-item-library"
   */
  mobileNavItem: (id: string) => `mobile-nav-item-${id}` as const,

  /**
   * Mobile library child item by id.
   * @example NAV_TEST_IDS.mobileLibraryItem('private') // => "mobile-library-item-private"
   */
  mobileLibraryItem: (id: string) => `mobile-library-item-${id}` as const,

  // ── FloatingActionBar ──────────────────────────────────────────────────────

  /** FloatingActionBar pill container */
  floatingActionBar: 'floating-action-bar',

  /**
   * Individual action button inside FloatingActionBar.
   * @example NAV_TEST_IDS.floatingAction('add') // => "floating-action-add"
   */
  floatingAction: (id: string) => `floating-action-${id}` as const,
} as const;
