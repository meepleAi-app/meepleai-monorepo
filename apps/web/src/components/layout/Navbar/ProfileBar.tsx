/**
 * ProfileBar - User profile section in navbar
 * Issue #3288 - Phase 2: Navbar Components
 *
 * Features:
 * - Guest state: Login/Register buttons
 * - Logged-in state: User dropdown menu
 * - Admin badge for admin users
 * - Logout functionality
 */

'use client';

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

export interface ProfileBarProps {
  /** Show compact version (icon only) */
  compact?: boolean;
  /** Additional className */
  className?: string;
}

/**
 * ProfileBar Component
 *
 * Displays user authentication state and actions.
 * Shows login/register for guests, dropdown menu for logged-in users.
 */
export function ProfileBar({ compact = false, className }: ProfileBarProps) {
  const { data: user, isLoading } = useCurrentUser();
  const router = useRouter();
  const [isLoggingOut, startTransition] = useTransition();

  const isAdmin = user?.role?.toLowerCase() === 'admin';

  // Get user display info
  const userInitial =
    user?.displayName?.charAt(0).toUpperCase() ||
    user?.email?.charAt(0).toUpperCase() ||
    'U';

  // Handle logout
  const handleLogout = () => {
    startTransition(async () => {
      const result = await logoutAction();
      if (result.success) {
        router.push('/login');
      }
    });
  };

  // Loading state
  if (isLoading) {
    return (
      <div className={cn('flex items-center gap-2', className)}>
        <div className="h-8 w-8 rounded-full bg-muted animate-pulse" />
      </div>
    );
  }

  // Guest state
  if (!user) {
    return (
      <div className={cn('flex items-center gap-2', className)}>
        {compact ? (
          <Link href="/login">
            <Button variant="ghost" size="icon" aria-label="Accedi">
              <LogIn className="h-5 w-5" />
            </Button>
          </Link>
        ) : (
          <>
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
            {/* Mobile: icon only */}
            <Link href="/login" className="sm:hidden">
              <Button variant="default" size="sm">
                <LogIn className="h-4 w-4 mr-1" />
                Accedi
              </Button>
            </Link>
          </>
        )}
      </div>
    );
  }

  // Logged-in state
  return (
    <div className={cn('flex items-center gap-2', className)}>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            className="flex items-center gap-2 px-2"
            aria-label="Menu utente"
            data-testid="user-menu-trigger"
          >
            {/* Avatar */}
            <div
              className={cn(
                'flex items-center justify-center rounded-full',
                'w-8 h-8 bg-primary/20',
                isAdmin && 'ring-2 ring-[hsl(262_83%_62%)]'
              )}
            >
              <span className="text-sm font-semibold text-primary">
                {userInitial}
              </span>
            </div>

            {/* Name (desktop only) */}
            {!compact && (
              <span className="text-sm text-muted-foreground hidden lg:inline max-w-[120px] truncate">
                {user.displayName || user.email || 'Utente'}
              </span>
            )}
          </Button>
        </DropdownMenuTrigger>

        <DropdownMenuContent align="end" className="w-56">
          {/* User info */}
          <DropdownMenuLabel>
            <div className="flex flex-col space-y-1">
              <p className="text-sm font-medium leading-none">
                {user.displayName || 'Utente'}
              </p>
              <p className="text-xs leading-none text-muted-foreground">
                {user.email}
              </p>
              {isAdmin && (
                <span className="inline-flex items-center gap-1 mt-1 text-xs text-[hsl(262_83%_62%)]">
                  <Shield className="h-3 w-3" />
                  Admin
                </span>
              )}
            </div>
          </DropdownMenuLabel>

          <DropdownMenuSeparator />

          {/* Profile link */}
          <DropdownMenuItem asChild>
            <Link
              href="/profile"
              className="flex items-center gap-2 cursor-pointer"
            >
              <UserIcon className="h-4 w-4" />
              <span>Profilo</span>
            </Link>
          </DropdownMenuItem>

          {/* Admin link */}
          {isAdmin && (
            <DropdownMenuItem asChild data-testid="admin-menu-item">
              <Link
                href="/admin"
                className="flex items-center gap-2 cursor-pointer"
              >
                <Shield className="h-4 w-4" />
                <span>Admin Panel</span>
              </Link>
            </DropdownMenuItem>
          )}

          {/* Settings */}
          <DropdownMenuItem asChild data-testid="settings-menu-item">
            <Link
              href="/settings"
              className="flex items-center gap-2 cursor-pointer"
            >
              <Settings className="h-4 w-4" />
              <span>Impostazioni</span>
            </Link>
          </DropdownMenuItem>

          <DropdownMenuSeparator />

          {/* Theme toggle */}
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
    </div>
  );
}
