/**
 * HamburgerMenu - Mobile slide-out navigation menu
 * Issue #3288 - Phase 2: Navbar Components
 *
 * Features:
 * - Slides in from left (mobile pattern)
 * - Full navigation links
 * - Theme toggle
 * - User actions (login/logout)
 * - Closes on backdrop click or escape
 */

'use client';

import { useTransition } from 'react';

import {
  Home,
  Gamepad2,
  BookOpen,
  LayoutDashboard,
  MessageSquare,
  Settings,
  Shield,
  User,
  Dice6,
  LogOut,
  LogIn,
  UserPlus,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
import { useLayout } from '@/components/layout/LayoutProvider';
import { Separator } from '@/components/ui/navigation/separator';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/navigation/sheet';
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';
import { Button } from '@/components/ui/primitives/button';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { cn } from '@/lib/utils';

import { Logo } from './Logo';
import { NavItems, type NavItem } from './NavItems';

/**
 * Default navigation items
 */
const NAV_ITEMS: NavItem[] = [
  {
    href: '/',
    icon: Home,
    label: 'Home',
    ariaLabel: 'Navigate to home page',
    testId: 'menu-home',
  },
  {
    href: '/dashboard',
    icon: LayoutDashboard,
    label: 'Dashboard',
    ariaLabel: 'Navigate to dashboard',
    testId: 'menu-dashboard',
  },
  {
    href: '/library',
    icon: BookOpen,
    label: 'I Miei Giochi',
    ariaLabel: 'Navigate to your game library',
    testId: 'menu-library',
  },
  {
    href: '/games',
    icon: Gamepad2,
    label: 'Catalogo',
    ariaLabel: 'Navigate to games catalog',
    testId: 'menu-catalog',
  },
  {
    href: '/chat/new',
    icon: MessageSquare,
    label: 'Chat',
    ariaLabel: 'Navigate to chat interface',
    testId: 'menu-chat',
  },
  {
    href: '/toolkit',
    icon: Dice6,
    label: 'Toolkit',
    ariaLabel: 'Navigate to game session toolkit',
    testId: 'menu-toolkit',
  },
  {
    href: '/profile',
    icon: User,
    label: 'Profilo',
    ariaLabel: 'Navigate to your profile',
    testId: 'menu-profile',
  },
];

const ADMIN_NAV_ITEM: NavItem = {
  href: '/admin/overview',
  icon: Shield,
  label: 'Admin',
  ariaLabel: 'Navigate to admin dashboard',
  testId: 'menu-admin',
  adminOnly: true,
};

export interface HamburgerMenuProps {
  /** Additional className */
  className?: string;
}

/**
 * HamburgerMenu Component
 *
 * Full-screen mobile menu with navigation links and user actions.
 * Uses Sheet component for slide-in animation.
 */
export function HamburgerMenu({ className }: HamburgerMenuProps) {
  const { isMenuOpen, toggleMenu } = useLayout();
  const { data: user } = useCurrentUser();
  const router = useRouter();
  const [isLoggingOut, startTransition] = useTransition();

  const isAdmin = user?.role?.toLowerCase() === 'admin';

  // Build navigation items list
  const navItems = isAdmin ? [...NAV_ITEMS, ADMIN_NAV_ITEM] : NAV_ITEMS;

  // Handle logout
  const handleLogout = () => {
    startTransition(async () => {
      const result = await logoutAction();
      if (result.success) {
        toggleMenu(false);
        router.push('/login');
      }
    });
  };

  // Close menu when navigating
  const handleNavigation = () => {
    toggleMenu(false);
  };

  return (
    <Sheet open={isMenuOpen} onOpenChange={toggleMenu}>
      <SheetContent
        side="left"
        id="mobile-menu"
        className={cn(
          'w-[300px] sm:w-[320px] p-0',
          className
        )}
      >
        {/* Header with logo */}
        <SheetHeader className="p-4 pb-2 border-b border-border/50">
          <SheetTitle asChild>
            <div className="flex items-center justify-between">
              <Logo variant="full" size="sm" asLink={false} />
            </div>
          </SheetTitle>
        </SheetHeader>

        {/* Navigation links */}
        <div className="flex-1 overflow-y-auto py-4">
          <NavItems
            items={navItems}
            direction="vertical"
            className="px-2"
            onItemClick={handleNavigation}
          />
        </div>

        {/* Footer with user actions */}
        <div className="mt-auto border-t border-border/50 p-4 space-y-3">
          {/* Settings link */}
          <Link
            href="/profile?tab=settings"
            onClick={handleNavigation}
            className={cn(
              'flex items-center gap-2 px-4 py-3 rounded-lg',
              'text-sm font-medium text-muted-foreground',
              'hover:bg-muted hover:text-foreground',
              'transition-colors duration-200',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)]'
            )}
          >
            <Settings className="h-5 w-5" aria-hidden="true" />
            <span>Impostazioni</span>
          </Link>

          <Separator />

          {/* Theme toggle */}
          <div className="px-2">
            <ThemeToggle showLabel size="sm" className="w-full justify-start" />
          </div>

          <Separator />

          {/* User actions */}
          {user ? (
            <>
              {/* User info */}
              <div className="px-4 py-2">
                <p className="text-sm font-medium">{user.displayName || 'Utente'}</p>
                <p className="text-xs text-muted-foreground">{user.email}</p>
              </div>

              {/* Logout button */}
              <Button
                variant="ghost"
                onClick={handleLogout}
                disabled={isLoggingOut}
                className="w-full justify-start text-destructive hover:text-destructive hover:bg-destructive/10"
              >
                <LogOut className="h-5 w-5 mr-2" aria-hidden="true" />
                {isLoggingOut ? 'Disconnessione...' : 'Esci'}
              </Button>
            </>
          ) : (
            <div className="flex flex-col gap-2">
              <Link
                href="/login"
                onClick={handleNavigation}
              >
                <Button variant="default" className="w-full">
                  <LogIn className="h-5 w-5 mr-2" aria-hidden="true" />
                  Accedi
                </Button>
              </Link>
              <Link
                href="/register"
                onClick={handleNavigation}
              >
                <Button variant="outline" className="w-full">
                  <UserPlus className="h-5 w-5 mr-2" aria-hidden="true" />
                  Registrati
                </Button>
              </Link>
            </div>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
