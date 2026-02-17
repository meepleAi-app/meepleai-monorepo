/**
 * UnifiedHeader - Mobile-only compact header
 * Issue #3104 - Unify header navigation
 *
 * Desktop navigation is now handled by the Sidebar component.
 * This header renders only on mobile (< md breakpoint).
 *
 * Features:
 * - Compact 48px height
 * - Left: Hamburger drawer + Logo (icon only)
 * - Right: Notifications + User avatar dropdown
 * - Glass morphism design
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
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
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

export interface UnifiedHeaderProps {
  /** Additional className */
  className?: string;
}

export function UnifiedHeader({ className }: UnifiedHeaderProps) {
  const router = useRouter();
  const { isAuthLoading, isAuthenticated } = useNavigationItems();
  const { data: user } = useCurrentUser();
  const [isScrolled, setIsScrolled] = useState(false);
  const [isLoggingOut, startTransition] = useTransition();

  const isAdmin = user?.role?.toLowerCase() === 'admin' || user?.role?.toLowerCase() === 'superadmin';
  const isEditor = user?.role?.toLowerCase() === 'editor' || isAdmin;

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 10);
    };
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  const handleLogout = () => {
    startTransition(async () => {
      const result = await logoutAction();
      if (result.success) {
        router.push('/login');
      }
    });
  };

  const userInitial =
    user?.displayName?.charAt(0).toUpperCase() || user?.email?.charAt(0).toUpperCase() || 'U';

  // User Menu (compact — avatar only, no name)
  const UserMenu = () => {
    if (isAuthLoading) {
      return (
        <div className="flex items-center" data-testid="user-menu-loading">
          <div className="w-8 h-8 rounded-full bg-muted animate-pulse" />
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
            size="icon"
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

          <DropdownMenuItem asChild data-testid="profile-menu-item">
            <Link href="/profile" className="flex items-center gap-2 cursor-pointer">
              <User className="h-4 w-4" />
              <span>Profilo</span>
            </Link>
          </DropdownMenuItem>

          <DropdownMenuSeparator />

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

          <DropdownMenuItem asChild data-testid="settings-menu-item">
            <Link href="/settings" className="flex items-center gap-2 cursor-pointer">
              <Settings className="h-4 w-4" />
              <span>Impostazioni</span>
            </Link>
          </DropdownMenuItem>

          <DropdownMenuSeparator />

          <div className="px-2 py-1.5">
            <ThemeToggle showLabel size="sm" className="w-full justify-start" />
          </div>

          <DropdownMenuSeparator />

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
        'md:hidden',
        // Glass morphism
        'bg-background/95 backdrop-blur-[16px] backdrop-saturate-[180%]',
        'dark:bg-card dark:backdrop-blur-none',
        'border-border/50 dark:border-border/30',
        'transition-shadow duration-200',
        isScrolled && 'shadow-sm dark:shadow-md',
        className
      )}
      data-testid="unified-header"
    >
      <div className="container mx-auto flex h-12 items-center justify-between px-4">
        {/* Left: Mobile Nav + Logo (icon only) */}
        <div className="flex items-center gap-2">
          {isAuthenticated && !isAuthLoading && (
            <MobileNavDrawer />
          )}
          <Link href="/" className="flex items-center" aria-label="MeepleAI Home">
            <MeepleLogo variant="icon" size="sm" />
          </Link>
        </div>

        {/* Right: Notifications + User Menu */}
        <div className="flex items-center gap-1">
          {isAuthenticated && !isAuthLoading && (
            <NotificationBell />
          )}
          <UserMenu />
        </div>
      </div>
    </header>
  );
}
