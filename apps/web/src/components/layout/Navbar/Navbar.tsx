/**
 * Navbar - Responsive navigation header
 * Issue #3288 - Phase 2: Navbar Components
 *
 * Features:
 * - Responsive layout (mobile/tablet/desktop)
 * - Hamburger menu on mobile
 * - Full navigation on desktop
 * - Profile section with user menu
 * - Sticky header with glass morphism
 * - Search trigger integration (Phase 3)
 */

'use client';

import { useState, useEffect } from 'react';
import {
  Home,
  Gamepad2,
  BookOpen,
  LayoutDashboard,
  MessageSquare,
  Shield,
  User,
  Dice6,
  Settings,
} from 'lucide-react';
import Link from 'next/link';

import { useLayout } from '@/components/layout/LayoutProvider';
import { NotificationBell } from '@/components/notifications';
import { Button } from '@/components/ui/primitives/button';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { cn } from '@/lib/utils';

import { HamburgerButton } from './HamburgerButton';
import { HamburgerMenu } from './HamburgerMenu';
import { Logo } from './Logo';
import { NavItems, type NavItem } from './NavItems';
import { ProfileBar } from './ProfileBar';

/**
 * Desktop navigation items
 */
const DESKTOP_NAV_ITEMS: NavItem[] = [
  {
    href: '/dashboard',
    icon: LayoutDashboard,
    label: 'Dashboard',
    ariaLabel: 'Navigate to dashboard',
  },
  {
    href: '/library',
    icon: BookOpen,
    label: 'I Miei Giochi',
    ariaLabel: 'Navigate to your game library',
  },
  {
    href: '/games',
    icon: Gamepad2,
    label: 'Catalogo',
    ariaLabel: 'Navigate to games catalog',
  },
  {
    href: '/chat',
    icon: MessageSquare,
    label: 'Chat',
    ariaLabel: 'Navigate to chat interface',
  },
  {
    href: '/toolkit',
    icon: Dice6,
    label: 'Toolkit',
    ariaLabel: 'Navigate to game session toolkit',
  },
  {
    href: '/profile',
    icon: User,
    label: 'Profilo',
    ariaLabel: 'Navigate to your profile',
  },
];

const ADMIN_NAV_ITEM: NavItem = {
  href: '/admin',
  icon: Shield,
  label: 'Admin',
  ariaLabel: 'Navigate to admin dashboard',
  adminOnly: true,
};

export interface NavbarProps {
  /** Additional className */
  className?: string;
  /** Whether to show search (Phase 3) */
  showSearch?: boolean;
  /** Whether to show notifications */
  showNotifications?: boolean;
}

/**
 * Navbar Component
 *
 * Main navigation header with responsive behavior.
 * Mobile: Hamburger + Logo + Actions
 * Desktop: Logo + Nav + Search + Actions
 */
export function Navbar({
  className,
  showSearch = true,
  showNotifications = true,
}: NavbarProps) {
  const { responsive, isMenuOpen, toggleMenu } = useLayout();
  const { isMobile } = responsive;
  const { data: user } = useCurrentUser();
  const [isScrolled, setIsScrolled] = useState(false);

  const isAdmin = user?.role?.toLowerCase() === 'admin';

  // Build navigation items
  const navItems = isAdmin
    ? [...DESKTOP_NAV_ITEMS, ADMIN_NAV_ITEM]
    : DESKTOP_NAV_ITEMS;

  // Track scroll for shadow effect
  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 10);
    };

    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return (
    <>
      <header
        className={cn(
          'sticky top-0 z-50 w-full border-b',
          // Light mode: Glass morphism
          'bg-background/95 backdrop-blur-[16px] backdrop-saturate-[180%]',
          // Dark mode: Solid professional
          'dark:bg-card dark:backdrop-blur-none',
          'border-border/50 dark:border-border/30',
          // Shadow on scroll
          'transition-shadow duration-200',
          isScrolled && 'shadow-sm dark:shadow-md',
          className
        )}
        data-testid="navbar"
      >
        <div className="container mx-auto flex h-14 md:h-16 items-center justify-between px-4 sm:px-6 lg:px-8">
          {/* Left section */}
          <div className="flex items-center gap-2">
            {/* Hamburger (mobile only) */}
            {isMobile && (
              <HamburgerButton
                isOpen={isMenuOpen}
                onToggle={() => toggleMenu()}
              />
            )}

            {/* Logo */}
            <Logo variant="auto" size="sm" />
          </div>

          {/* Center section (desktop navigation) */}
          {!isMobile && (
            <NavItems
              items={navItems}
              direction="horizontal"
              className="hidden md:flex"
            />
          )}

          {/* Right section */}
          <div className="flex items-center gap-1 sm:gap-2">
            {/* Mobile settings button */}
            {isMobile && (
              <Link href="/settings">
                <Button variant="ghost" size="icon" aria-label="Impostazioni">
                  <Settings className="h-5 w-5" />
                </Button>
              </Link>
            )}

            {/* Search trigger (Phase 3 placeholder) */}
            {showSearch && (
              <div className="hidden sm:block">
                {/* GlobalSearch will be added in Phase 3 */}
              </div>
            )}

            {/* Notifications */}
            {showNotifications && <NotificationBell />}

            {/* Profile */}
            <ProfileBar compact={isMobile} />
          </div>
        </div>
      </header>

      {/* Mobile menu (Sheet) */}
      <HamburgerMenu />
    </>
  );
}
