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
        'sticky top-0 z-50',
        'bg-white/95 dark:bg-gray-900/95',
        'backdrop-blur-[10px]',
        'border-b border-[#e8e4d8] dark:border-gray-700',
        'shadow-[0_1px_3px_rgba(139,90,60,0.08)]',
        className
      )}
      data-testid="admin-header"
    >
      <div className="flex h-14 items-center gap-4 px-4 sm:px-6 lg:px-8 max-w-[1400px] mx-auto">
        {/* Mobile menu trigger */}
        {mobileMenuTrigger && <div className="lg:hidden">{mobileMenuTrigger}</div>}

        {/* Logo with dice icon */}
        <div className="flex items-center gap-3">
          <span className="text-[1.875rem] text-meeple-orange" aria-hidden="true">
            🎲
          </span>
          <h1
            className="text-[1.375rem] font-heading font-bold text-[#2d2d2d] dark:text-white"
            data-testid="admin-header-title"
          >
            {title}
          </h1>
        </div>

        {/* Spacer */}
        <div className="flex-1" />

        {/* Actions */}
        {actions && <div className="flex items-center gap-2">{actions}</div>}

        {/* System Button */}
        <Button
          size="sm"
          asChild
          className="hidden sm:flex bg-[#16a34a] hover:bg-[#15803d] text-white font-heading font-bold rounded-xl px-7 py-3 transition-all"
          data-testid="admin-header-system-btn"
        >
          <Link href="/" className="flex items-center gap-2">
            <HomeIcon className="h-4 w-4" />
            <span>System</span>
          </Link>
        </Button>

        {/* Admin Button */}
        <Button
          size="sm"
          asChild
          className="hidden sm:flex bg-meeple-orange hover:bg-[#b85a19] text-white font-heading font-bold rounded-xl px-7 py-3 transition-all hover:translate-y-[-1px] hover:shadow-[0_4px_12px_rgba(210,105,30,0.3)]"
          data-testid="admin-header-admin-btn"
        >
          <Link href="/admin" className="flex items-center gap-2">
            <SettingsIcon className="h-4 w-4" />
            <span>Admin</span>
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