/**
 * AdminHeader Component - Issue #881
 *
 * Header for admin pages with user menu and navigation.
 * Features:
 * - Responsive design
 * - User dropdown menu with profile, settings, logout
 * - Mobile menu trigger slot
 * - Back to home link
 */

'use client';

import { ReactNode } from 'react';

import { HomeIcon, UserIcon, SettingsIcon, LogOutIcon } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { Avatar, AvatarFallback } from '@/components/ui/data-display/avatar';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

export interface AdminUser {
  id: string;
  email: string;
  displayName?: string;
  role?: string;
}

export interface AdminHeaderProps {
  /** Current user info */
  user?: AdminUser;
  /** Mobile menu trigger (rendered on left for mobile) */
  mobileMenuTrigger?: ReactNode;
  /** Additional actions (rendered before user menu) */
  actions?: ReactNode;
  /** Custom title */
  title?: string;
  /** Additional className */
  className?: string;
}

function getInitials(name?: string, email?: string): string {
  if (name) {
    return name
      .split(' ')
      .map(n => n[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  }
  if (email) {
    return email.slice(0, 2).toUpperCase();
  }
  return 'AD';
}

export function AdminHeader({
  user,
  mobileMenuTrigger,
  actions,
  title = 'MeepleAI Admin',
  className,
}: AdminHeaderProps) {
  const router = useRouter();

  const handleLogout = async () => {
    try {
      await api.auth.logout();
      router.push('/login');
    } catch {
      // Force redirect even on error
      router.push('/login');
    }
  };

  return (
    <header
      className={cn(
        'sticky top-0 z-40 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900',
        className
      )}
      data-testid="admin-header"
    >
      <div className="flex h-14 items-center gap-4 px-4 sm:px-6">
        {/* Mobile menu trigger */}
        {mobileMenuTrigger && <div className="lg:hidden">{mobileMenuTrigger}</div>}

        {/* Title */}
        <div className="flex items-center gap-3">
          <h1
            className="text-lg font-semibold text-gray-900 dark:text-white"
            data-testid="admin-header-title"
          >
            {title}
          </h1>
        </div>

        {/* Spacer */}
        <div className="flex-1" />

        {/* Actions */}
        {actions && <div className="flex items-center gap-2">{actions}</div>}

        {/* Back to Home */}
        <Button
          variant="ghost"
          size="sm"
          asChild
          className="hidden sm:flex"
          data-testid="admin-header-home-btn"
        >
          <Link href="/" className="flex items-center gap-2">
            <HomeIcon className="h-4 w-4" />
            <span>Home</span>
          </Link>
        </Button>

        {/* User Menu */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              className="relative h-8 w-8 rounded-full"
              data-testid="admin-header-user-menu"
            >
              <Avatar className="h-8 w-8">
                <AvatarFallback className="bg-indigo-100 text-indigo-700 dark:bg-indigo-900 dark:text-indigo-300 text-xs">
                  {getInitials(user?.displayName, user?.email)}
                </AvatarFallback>
              </Avatar>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuLabel>
              <div className="flex flex-col space-y-1">
                <p className="text-sm font-medium">{user?.displayName ?? 'Admin'}</p>
                <p className="text-xs text-muted-foreground truncate">{user?.email ?? ''}</p>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem asChild>
              <Link href="/profile" className="flex items-center cursor-pointer">
                <UserIcon className="mr-2 h-4 w-4" />
                Profile
              </Link>
            </DropdownMenuItem>
            <DropdownMenuItem asChild>
              <Link href="/settings" className="flex items-center cursor-pointer">
                <SettingsIcon className="mr-2 h-4 w-4" />
                Settings
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem asChild>
              <Link href="/" className="flex items-center cursor-pointer sm:hidden">
                <HomeIcon className="mr-2 h-4 w-4" />
                Home
              </Link>
            </DropdownMenuItem>
            <DropdownMenuItem
              onClick={handleLogout}
              className="text-red-600 dark:text-red-400 cursor-pointer"
              data-testid="admin-header-logout-btn"
            >
              <LogOutIcon className="mr-2 h-4 w-4" />
              Log out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}