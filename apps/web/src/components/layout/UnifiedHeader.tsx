/**
 * UnifiedHeader - Unified navigation header component
 * Issue #3104 - Unify header navigation
 *
 * Single source of truth for header navigation across the app.
 *
 * Features:
 * - Desktop: Full navigation with all items + Admin link for admins
 * - Mobile: Top bar with Logo + Settings + Notifications + User menu
 * - Active state highlighting
 * - Responsive glass morphism design
 * - Keyboard navigation support
 * - WCAG 2.1 AA compliance
 */

'use client';

import { useState, useEffect, useTransition } from 'react';

import {
  Gamepad2,
  LayoutDashboard,
  Settings,
  Shield,
  LogOut,
  UserIcon,
  User,
  FileEdit,
  Users,
  History,
  Calendar,
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
import { cn } from '@/lib/utils';

interface NavItem {
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  ariaLabel: string;
  /** Only show for admin users */
  adminOnly?: boolean;
  /** Only show for editor users (includes admin) */
  editorOnly?: boolean;
  /** Only show for authenticated users */
  authOnly?: boolean;
  /** Only show for anonymous (non-authenticated) users */
  anonOnly?: boolean;
}

/**
 * Navigation items for the header
 * Issue #4064 - Navigation Overhaul
 *
 * Visibility rules:
 * - anonOnly: only visible when NOT logged in
 * - authOnly: only visible when logged in
 * - editorOnly: only visible for editor users (includes admin)
 * - adminOnly: only visible for admin users
 * - no flags: visible to everyone
 *
 * Anonymous users see: Welcome, Catalogo
 * Authenticated users see: Dashboard, Catalogo, Libreria (dropdown), Agenti, Chat History, Sessioni
 * Editor users see: + Editor (in user dropdown)
 * Admin users see: + Editor, Admin (in user dropdown)
 */
const NAV_ITEMS: NavItem[] = [
  // Anonymous-only items
  {
    href: '/',
    icon: LayoutDashboard,
    label: 'Welcome',
    ariaLabel: 'Navigate to welcome page',
    anonOnly: true,
  },
  // Authenticated-only items
  {
    href: '/dashboard',
    icon: LayoutDashboard,
    label: 'Dashboard',
    ariaLabel: 'Navigate to dashboard',
    authOnly: true,
  },
  // Visible to everyone
  {
    href: '/games',
    icon: Gamepad2,
    label: 'Catalogo',
    ariaLabel: 'Navigate to games catalog',
  },
  // Authenticated-only items
  {
    href: '/agents',
    icon: Users,
    label: 'Agenti',
    ariaLabel: 'Navigate to agents list',
    authOnly: true,
  },
  {
    href: '/chat/new',
    icon: History,
    label: 'Chat History',
    ariaLabel: 'Navigate to chat history',
    authOnly: true,
  },
  {
    href: '/sessions',
    icon: Calendar,
    label: 'Sessioni',
    ariaLabel: 'Navigate to play sessions',
    authOnly: true,
  },
];

export interface UnifiedHeaderProps {
  /** Additional className */
  className?: string;
}

export function UnifiedHeader({ className }: UnifiedHeaderProps) {
  const pathname = usePathname();
  const router = useRouter();
  const { data: user, isLoading: isAuthLoading } = useCurrentUser();
  const [isScrolled, setIsScrolled] = useState(false);
  const [isLoggingOut, startTransition] = useTransition();

  // Check user authentication and role
  const isAuthenticated = !!user;
  const isAdmin = user?.role?.toLowerCase() === 'admin';
  const isEditor = user?.role?.toLowerCase() === 'editor' || isAdmin;

  // Handle scroll for sticky header effect
  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 10);
    };

    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  // Check if route is active
  const isActive = (href: string) => {
    if (href === '/') {
      return pathname === '/';
    }
    if (href === '/dashboard') {
      return pathname === '/dashboard';
    }
    // Library routes include /library, /library/private, etc.
    if (href === '/library') {
      return pathname === '/library' || pathname?.startsWith('/library/');
    }
    // Chat routes: /chat/new, /chat/{threadId}, etc.
    if (href === '/chat/new') {
      return pathname?.startsWith('/chat') ?? false;
    }
    return pathname?.startsWith(href) ?? false;
  };

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

  // Check if any library route is active
  const isLibraryActive = pathname === '/library' || pathname?.startsWith('/library/') || false;

  // Library sub-items for mobile drawer
  const LIBRARY_ITEMS = [
    {
      href: '/library',
      label: 'Collezione',
      ariaLabel: 'Navigate to your game collection',
    },
    {
      href: '/library/private',
      label: 'Giochi Privati',
      ariaLabel: 'Navigate to your private games',
    },
  ];

  // Filter nav items based on authentication and role
  // While auth is loading, show only items visible to everyone (no authOnly, no anonOnly)
  const visibleNavItems = NAV_ITEMS.filter(item => {
    if (isAuthLoading) {
      // While loading, only show items with no auth restrictions
      return !item.authOnly && !item.anonOnly && !item.adminOnly && !item.editorOnly;
    }
    // Admin-only items: only for admins
    if (item.adminOnly && !isAdmin) return false;
    // Editor-only items: only for editors (includes admins)
    if (item.editorOnly && !isEditor) return false;
    // Auth-only items: only for authenticated users
    if (item.authOnly && !isAuthenticated) return false;
    // Anon-only items: only for non-authenticated users
    if (item.anonOnly && isAuthenticated) return false;
    return true;
  });

  // Desktop Navigation
  const DesktopNav = () => {
    // Find where to insert LibraryDropdown (after Catalogo in authenticated view)
    const catalogIndex = visibleNavItems.findIndex(item => item.href === '/games');
    const beforeLibrary = visibleNavItems.slice(0, catalogIndex + 1);
    const afterLibrary = visibleNavItems.slice(catalogIndex + 1);

    return (
      <nav className="hidden md:flex items-center gap-1" aria-label="Main navigation">
        {beforeLibrary.map(({ href, icon: Icon, label, ariaLabel }) => {
          const active = isActive(href);
          return (
            <Link
              key={href}
              href={href}
              aria-label={ariaLabel}
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
              <span>{label}</span>
            </Link>
          );
        })}

        {/* Library Dropdown (only for authenticated users) */}
        {isAuthenticated && !isAuthLoading && (
          <LibraryDropdown />
        )}

        {afterLibrary.map(({ href, icon: Icon, label, ariaLabel }) => {
          const active = isActive(href);
          return (
            <Link
              key={href}
              href={href}
              aria-label={ariaLabel}
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
              <span>{label}</span>
            </Link>
          );
        })}
      </nav>
    );
  };

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

          {/* Settings (useful shortcut in dropdown) */}
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
            <MobileNavDrawer
              navItems={visibleNavItems.map(item => ({
                href: item.href,
                icon: item.icon,
                label: item.label,
                ariaLabel: item.ariaLabel,
              }))}
              libraryItems={LIBRARY_ITEMS}
              isLibraryActive={isLibraryActive}
            />
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

        {/* Right: Settings + Notifications + User Menu (Settings/Notifications only for authenticated) */}
        <div className="flex items-center gap-2">
          {isAuthenticated && !isAuthLoading && (
            <>
              <Link
                href="/settings"
                aria-label="Navigate to settings"
              >
                <Button variant="ghost" size="icon">
                  <Settings className="h-5 w-5" />
                </Button>
              </Link>
              <NotificationBell />
            </>
          )}
          <UserMenu />
        </div>
      </div>
    </header>
  );
}
