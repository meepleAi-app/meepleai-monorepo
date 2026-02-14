/**
 * UnifiedHeader - Unified navigation header component
 * Issue #3104 - Unify header navigation
 *
 * Single source of truth for header navigation across the app.
 * Uses useNavigationItems hook for unified nav config.
 *
 * Features:
 * - Desktop: Full navigation with all items + Admin link for admins
 * - Mobile: Hamburger drawer + Logo + Notifications + User menu
 * - Active state highlighting
 * - Responsive glass morphism design
 * - Keyboard navigation support
 * - WCAG 2.1 AA compliance
 */

'use client';

import { useState, useEffect, useTransition } from 'react';

import {
  Settings,
  Shield,
  LogOut,
  UserIcon,
  User,
  FileEdit,
  MoreHorizontal,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
import { LibraryDropdown } from '@/components/layout/LibraryDropdown';
import { MobileNavDrawer } from '@/components/layout/MobileNavDrawer';
import { NotificationBell } from '@/components/notifications';
import { MeepleLogo } from '@/components/ui/meeple/meeple-logo';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';
import { Button } from '@/components/ui/primitives/button';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useNavigationItems } from '@/hooks/useNavigationItems';
import { cn } from '@/lib/utils';

import type { UnifiedNavItem } from '@/config/navigation.types';

/**
 * "Altro" overflow dropdown for medium viewports (md to xl-1).
 * Shows nav items that don't fit at smaller desktop widths.
 */
function OverflowDropdown({
  items,
  isItemActive,
  pathname,
}: {
  items: UnifiedNavItem[];
  isItemActive: (item: UnifiedNavItem, pathname: string) => boolean;
  pathname: string;
}) {
  const hasActiveOverflow = items.some(item => isItemActive(item, pathname));

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          className={cn(
            'flex xl:hidden items-center gap-1.5 px-3 py-2 rounded-lg text-sm font-medium',
            'transition-colors duration-200',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
            hasActiveOverflow
              ? 'bg-[hsl(262_83%_62%/0.1)] dark:bg-[hsl(262_83%_62%/0.2)] text-[hsl(262_83%_62%)] font-semibold'
              : 'text-muted-foreground hover:text-primary hover:bg-muted dark:hover:bg-muted/70'
          )}
          aria-label="More navigation items"
          data-testid="nav-overflow-trigger"
        >
          <MoreHorizontal className="h-4 w-4" aria-hidden="true" />
          <span>Altro</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-48">
        {items.map(item => {
          const Icon = item.icon;
          const active = isItemActive(item, pathname);
          return (
            <DropdownMenuItem key={item.id} asChild data-testid={`nav-overflow-item-${item.id}`}>
              <Link
                href={item.href}
                aria-label={item.ariaLabel}
                aria-current={active ? 'page' : undefined}
                className={cn(
                  'flex items-center gap-2 cursor-pointer w-full',
                  active && 'bg-[hsl(262_83%_62%/0.1)] text-[hsl(262_83%_62%)] font-semibold'
                )}
              >
                <Icon className="h-4 w-4" aria-hidden="true" />
                <span>{item.label}</span>
              </Link>
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export interface UnifiedHeaderProps {
  /** Additional className */
  className?: string;
}

export function UnifiedHeader({ className }: UnifiedHeaderProps) {
  const pathname = usePathname();
  const router = useRouter();
  const { items: navItems, isAuthLoading, isAuthenticated, isItemActive } = useNavigationItems();
  const { data: user } = useCurrentUser();
  const [isScrolled, setIsScrolled] = useState(false);
  const [isLoggingOut, startTransition] = useTransition();

  // Check user role
  const isAdmin = user?.role?.toLowerCase() === 'admin' || user?.role?.toLowerCase() === 'superadmin';
  const isEditor = user?.role?.toLowerCase() === 'editor' || isAdmin;

  // Handle scroll for sticky header effect
  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 10);
    };

    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  // Handle logout
  const handleLogout = () => {
    startTransition(async () => {
      const result = await logoutAction();
      if (result.success) {
        router.push('/login');
      }
    });
  };

  // Get user display info
  const userInitial =
    user?.displayName?.charAt(0).toUpperCase() || user?.email?.charAt(0).toUpperCase() || 'U';

  // Split nav items around library for desktop rendering:
  // Library gets its own dropdown, so we remove it from the linear nav
  // and insert LibraryDropdown after Catalogo.
  const itemsWithoutLibrary = navItems.filter(item => item.id !== 'library');
  const hasLibrary = navItems.some(item => item.id === 'library');

  const catalogIndex = itemsWithoutLibrary.findIndex(item => item.id === 'catalog');
  const beforeLibrary = catalogIndex >= 0
    ? itemsWithoutLibrary.slice(0, catalogIndex + 1)
    : itemsWithoutLibrary;
  const afterLibrary = catalogIndex >= 0
    ? itemsWithoutLibrary.slice(catalogIndex + 1)
    : [];

  // Split afterLibrary into always-visible and overflow for responsive nav.
  // Items with priority >= 7 go in "Altro" dropdown at md-xl, shown directly at xl+.
  const OVERFLOW_PRIORITY_THRESHOLD = 7;
  const primaryAfterLibrary = afterLibrary.filter(item => item.priority < OVERFLOW_PRIORITY_THRESHOLD);
  const overflowItems = afterLibrary.filter(item => item.priority >= OVERFLOW_PRIORITY_THRESHOLD);

  // Desktop Navigation
  const DesktopNav = () => (
    <nav className="hidden md:flex items-center gap-1" aria-label="Main navigation">
      {beforeLibrary.map(item => {
        const Icon = item.icon;
        const active = isItemActive(item, pathname ?? '');
        return (
          <Link
            key={item.id}
            href={item.href}
            aria-label={item.ariaLabel}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium',
              'transition-colors duration-200',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
              active
                ? 'bg-[hsl(262_83%_62%/0.1)] dark:bg-[hsl(262_83%_62%/0.2)] text-[hsl(262_83%_62%)] font-semibold'
                : 'text-muted-foreground hover:text-primary hover:bg-muted dark:hover:bg-muted/70'
            )}
          >
            <Icon className="h-4 w-4" aria-hidden="true" />
            <span>{item.label}</span>
          </Link>
        );
      })}

      {/* Library Dropdown (only for authenticated users) */}
      {isAuthenticated && !isAuthLoading && hasLibrary && (
        <LibraryDropdown />
      )}

      {/* Always-visible items after Library */}
      {primaryAfterLibrary.map(item => {
        const Icon = item.icon;
        const active = isItemActive(item, pathname ?? '');
        return (
          <Link
            key={item.id}
            href={item.href}
            aria-label={item.ariaLabel}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium',
              'transition-colors duration-200',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
              active
                ? 'bg-[hsl(262_83%_62%/0.1)] dark:bg-[hsl(262_83%_62%/0.2)] text-[hsl(262_83%_62%)] font-semibold'
                : 'text-muted-foreground hover:text-primary hover:bg-muted dark:hover:bg-muted/70'
            )}
          >
            <Icon className="h-4 w-4" aria-hidden="true" />
            <span>{item.label}</span>
          </Link>
        );
      })}

      {/* Overflow items — visible directly only at xl+ */}
      {overflowItems.map(item => {
        const Icon = item.icon;
        const active = isItemActive(item, pathname ?? '');
        return (
          <Link
            key={item.id}
            href={item.href}
            aria-label={item.ariaLabel}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'hidden xl:flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium',
              'transition-colors duration-200',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
              active
                ? 'bg-[hsl(262_83%_62%/0.1)] dark:bg-[hsl(262_83%_62%/0.2)] text-[hsl(262_83%_62%)] font-semibold'
                : 'text-muted-foreground hover:text-primary hover:bg-muted dark:hover:bg-muted/70'
            )}
          >
            <Icon className="h-4 w-4" aria-hidden="true" />
            <span>{item.label}</span>
          </Link>
        );
      })}

      {/* "Altro" overflow dropdown — visible at md to xl-1 */}
      {overflowItems.length > 0 && (
        <OverflowDropdown
          items={overflowItems}
          isItemActive={isItemActive}
          pathname={pathname ?? ''}
        />
      )}
    </nav>
  );

  // User Menu
  const UserMenu = () => {
    // Show skeleton while auth state is loading to prevent flash of "Accedi" button
    if (isAuthLoading) {
      return (
        <div className="flex items-center gap-2" data-testid="user-menu-loading">
          <div className="w-8 h-8 rounded-full bg-muted animate-pulse" />
          <div className="hidden lg:block w-20 h-4 rounded bg-muted animate-pulse" />
        </div>
      );
    }

    if (!user) {
      return (
        <Link href="/login">
          <Button variant="default" size="sm">
            Accedi
          </Button>
        </Link>
      );
    }

    return (
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            className="flex items-center gap-2"
            aria-label="User menu"
            data-testid="user-menu-trigger"
          >
            {user.displayName ? (
              <div className="w-8 h-8 rounded-full bg-primary/20 flex items-center justify-center">
                <span className="text-sm font-semibold text-primary">{userInitial}</span>
              </div>
            ) : (
              <UserIcon className="h-5 w-5" />
            )}
            <span className="text-sm text-muted-foreground hidden lg:inline">
              {user.displayName || user.email || 'Utente'}
            </span>
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-56">
          <DropdownMenuLabel>
            <div className="flex flex-col space-y-1">
              <p className="text-sm font-medium leading-none">
                {user.displayName || 'Utente'}
              </p>
              <p className="text-xs leading-none text-muted-foreground">{user.email}</p>
            </div>
          </DropdownMenuLabel>
          <DropdownMenuSeparator />

          {/* Profilo link */}
          <DropdownMenuItem asChild data-testid="profile-menu-item">
            <Link href="/profile" className="flex items-center gap-2 cursor-pointer">
              <User className="h-4 w-4" />
              <span>Profilo</span>
            </Link>
          </DropdownMenuItem>

          <DropdownMenuSeparator />

          {/* Editor link in dropdown (for editors and admins) */}
          {isEditor && (
            <>
              <DropdownMenuItem asChild data-testid="editor-panel-menu-item">
                <Link href="/editor" className="flex items-center gap-2 cursor-pointer">
                  <FileEdit className="h-4 w-4" />
                  <span>Editor Panel</span>
                </Link>
              </DropdownMenuItem>
              <DropdownMenuSeparator />
            </>
          )}

          {/* Admin link in dropdown (for both desktop and mobile) */}
          {isAdmin && (
            <>
              <DropdownMenuItem asChild data-testid="admin-panel-menu-item">
                <Link href="/admin" className="flex items-center gap-2 cursor-pointer">
                  <Shield className="h-4 w-4" />
                  <span>Admin Panel</span>
                </Link>
              </DropdownMenuItem>
              <DropdownMenuSeparator />
            </>
          )}

          {/* Settings */}
          <DropdownMenuItem asChild data-testid="settings-menu-item">
            <Link href="/settings" className="flex items-center gap-2 cursor-pointer">
              <Settings className="h-4 w-4" />
              <span>Impostazioni</span>
            </Link>
          </DropdownMenuItem>

          <DropdownMenuSeparator />

          {/* Theme Toggle */}
          <div className="px-2 py-1.5">
            <ThemeToggle showLabel size="sm" className="w-full justify-start" />
          </div>

          <DropdownMenuSeparator />

          {/* Logout */}
          <DropdownMenuItem
            onClick={handleLogout}
            disabled={isLoggingOut}
            className="flex items-center gap-2 cursor-pointer text-destructive focus:text-destructive"
            data-testid="logout-menu-item"
          >
            <LogOut className="h-4 w-4" />
            <span>{isLoggingOut ? 'Disconnessione...' : 'Esci'}</span>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    );
  };

  return (
    <header
      className={cn(
        'sticky top-0 z-50 w-full border-b',
        // Light mode: Glass morphism
        'bg-background/95 backdrop-blur-[16px] backdrop-saturate-[180%]',
        // Dark mode: Solid professional
        'dark:bg-card dark:backdrop-blur-none',
        'border-border/50 dark:border-border/30',
        'transition-shadow duration-200',
        isScrolled && 'shadow-sm dark:shadow-md',
        className
      )}
      data-testid="unified-header"
    >
      <div className="container mx-auto flex h-16 items-center justify-between px-4 sm:px-6 lg:px-8">
        {/* Left: Mobile Nav + Logo */}
        <div className="flex items-center gap-2">
          {/* Mobile Navigation Drawer */}
          {isAuthenticated && !isAuthLoading && (
            <MobileNavDrawer />
          )}

          <Link href="/" className="flex items-center gap-2" aria-label="MeepleAI Home">
            <MeepleLogo variant="icon" size="sm" />
            <span className="font-quicksand font-bold text-xl text-foreground hidden sm:inline">
              MeepleAI
            </span>
          </Link>
        </div>

        {/* Center: Desktop Navigation */}
        <DesktopNav />

        {/* Right: Notifications + User Menu (for authenticated users) */}
        <div className="flex items-center gap-2">
          {isAuthenticated && !isAuthLoading && (
            <NotificationBell />
          )}
          <UserMenu />
        </div>
      </div>
    </header>
  );
}
