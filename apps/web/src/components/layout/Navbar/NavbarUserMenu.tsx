'use client';

/**
 * NavbarUserMenu — User avatar + dropdown menu for the Navbar
 * Issue #5036 — Navbar Component
 *
 * Shows:
 * - Loading skeleton while user data is fetching
 * - Login/Register buttons for unauthenticated users
 * - Avatar + dropdown (profile, settings, theme, logout) for authenticated users
 *
 * Does NOT depend on LayoutProvider.
 */

import { useTransition } from 'react';

import {
  Settings,
  Shield,
  LogOut,
  User as UserIcon,
  LogIn,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
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

export interface NavbarUserMenuProps {
  /** Additional className */
  className?: string;
}

/**
 * NavbarUserMenu
 *
 * User authentication widget for the Navbar right side.
 * Handles guest, loading and authenticated states.
 */
export function NavbarUserMenu({ className }: NavbarUserMenuProps) {
  const { data: user, isLoading } = useCurrentUser();
  const router = useRouter();
  const [isLoggingOut, startTransition] = useTransition();

  const isAdmin =
    user?.role?.toLowerCase() === 'admin' ||
    user?.role?.toLowerCase() === 'superadmin' ||
    user?.role?.toLowerCase() === 'editor';

  const userInitial =
    user?.displayName?.charAt(0).toUpperCase() ||
    user?.email?.charAt(0).toUpperCase() ||
    'U';

  function handleLogout() {
    startTransition(async () => {
      const result = await logoutAction();
      if (result.success) {
        router.push('/login');
      }
    });
  }

  // ── Loading ─────────────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <div className={cn('flex items-center', className)}>
        <div className="h-8 w-8 rounded-full bg-muted animate-pulse" aria-busy="true" />
      </div>
    );
  }

  // ── Guest ───────────────────────────────────────────────────────────────────
  if (!user) {
    return (
      <div className={cn('flex items-center gap-2', className)}>
        <Link href="/login" className="hidden sm:block">
          <Button variant="ghost" size="sm">
            Accedi
          </Button>
        </Link>
        <Link href="/register" className="hidden sm:block">
          <Button variant="default" size="sm">
            Registrati
          </Button>
        </Link>
        <Link href="/login" className="sm:hidden">
          <Button variant="ghost" size="icon" aria-label="Accedi">
            <LogIn className="h-5 w-5" aria-hidden="true" />
          </Button>
        </Link>
      </div>
    );
  }

  // ── Authenticated ───────────────────────────────────────────────────────────
  return (
    <div className={cn('flex items-center', className)}>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            className="flex items-center gap-2 px-2 focus-visible:ring-2 focus-visible:ring-ring"
            aria-label="Menu utente"
            data-testid="user-menu-trigger"
          >
            {/* Avatar circle */}
            <div
              className={cn(
                'flex h-8 w-8 items-center justify-center rounded-full bg-primary/20',
                isAdmin && 'ring-2 ring-[hsl(262_83%_62%)]'
              )}
              aria-hidden="true"
            >
              <span className="text-sm font-semibold text-primary">{userInitial}</span>
            </div>
            {/* Name (lg+) */}
            <span className="hidden lg:inline max-w-[120px] truncate text-sm text-muted-foreground">
              {user.displayName || user.email || 'Utente'}
            </span>
          </Button>
        </DropdownMenuTrigger>

        <DropdownMenuContent align="end" className="w-56">
          {/* User info header */}
          <DropdownMenuLabel>
            <div className="flex flex-col space-y-1">
              <p className="text-sm font-medium leading-none">
                {user.displayName || 'Utente'}
              </p>
              <p className="text-xs leading-none text-muted-foreground">{user.email}</p>
              {isAdmin && (
                <span className="mt-1 inline-flex items-center gap-1 text-xs text-[hsl(262_83%_62%)]">
                  <Shield className="h-3 w-3" aria-hidden="true" />
                  Admin
                </span>
              )}
            </div>
          </DropdownMenuLabel>

          <DropdownMenuSeparator />

          <DropdownMenuItem asChild>
            <Link href="/profile" className="flex cursor-pointer items-center gap-2">
              <UserIcon className="h-4 w-4" aria-hidden="true" />
              <span>Profilo</span>
            </Link>
          </DropdownMenuItem>

          {isAdmin && (
            <DropdownMenuItem asChild data-testid="admin-menu-item">
              <Link href="/admin/overview" className="flex cursor-pointer items-center gap-2">
                <Shield className="h-4 w-4" aria-hidden="true" />
                <span>Admin Panel</span>
              </Link>
            </DropdownMenuItem>
          )}

          <DropdownMenuItem asChild data-testid="settings-menu-item">
            <Link href="/profile?tab=settings" className="flex cursor-pointer items-center gap-2">
              <Settings className="h-4 w-4" aria-hidden="true" />
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
            className="flex cursor-pointer items-center gap-2 text-destructive focus:text-destructive"
            data-testid="logout-menu-item"
          >
            <LogOut className="h-4 w-4" aria-hidden="true" />
            <span>{isLoggingOut ? 'Disconnessione...' : 'Esci'}</span>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
