/**
 * Navbar Components
 * Issue #5036 — Navbar Component (3-section, role-aware)
 *
 * New exports for the 3-tier navigation system.
 * Legacy sub-components preserved for backwards-compatibility during migration.
 */

// ─── New 3-section Navbar (Issue #5036) ──────────────────────────────────────
export { Navbar, type NavbarProps } from './Navbar';
export { NavbarDropdown, type NavbarDropdownProps, type NavbarDropdownItem } from './NavbarDropdown';
export { NavbarMobileDrawer, type NavbarMobileDrawerProps } from './NavbarMobileDrawer';
export { NavbarUserMenu, type NavbarUserMenuProps } from './NavbarUserMenu';

// ─── Legacy sub-components (kept for migration period) ────────────────────────
export { HamburgerButton, type HamburgerButtonProps } from './HamburgerButton';
export { HamburgerMenu, type HamburgerMenuProps } from './HamburgerMenu';
export { Logo, type LogoProps } from './Logo';
export { NavItems, NavItemButton, type NavItem, type NavItemsProps, type NavItemButtonProps } from './NavItems';
export { ProfileBar, type ProfileBarProps } from './ProfileBar';
export { UniversalNavbar, type UniversalNavbarProps } from './UniversalNavbar';
export { NotificationCenter, type NotificationCenterProps } from './NotificationCenter';
