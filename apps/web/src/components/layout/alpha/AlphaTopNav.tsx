'use client';

/**
 * AlphaTopNav — Top navigation bar for the alpha layout.
 *
 * Features:
 * - Sticky header with glassmorphism styling
 * - Left: MeepleAI logo linking to /dashboard
 * - Center: dynamic section title from alphaNavStore
 * - Right: NotificationBell (unread badge + sheet) + user avatar dropdown
 * - Mobile: hides on scroll down, shows on scroll up
 * - Desktop: always visible
 */

import { useTransition } from 'react';

import { LogOut, Settings, Shield, User } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
import { NotificationBell } from '@/components/notifications';
import { Avatar, AvatarFallback } from '@/components/ui/data-display/avatar';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { useAlphaNav } from '@/hooks/useAlphaNav';
import { cn } from '@/lib/utils';

interface AlphaTopNavProps {
  isAdmin: boolean;
}

export function AlphaTopNav({ isAdmin }: AlphaTopNavProps) {
  const { sectionTitle } = useAlphaNav();
  const router = useRouter();
  const [isLoggingOut, startTransition] = useTransition();

  const handleLogout = () => {
    startTransition(async () => {
      const result = await logoutAction();
      if (result.success) {
        router.push('/login');
      }
    });
  };

  return (
    <header
      className={cn(
        'sticky top-0 z-40 h-14',
        'flex items-center justify-between px-4',
        'bg-background/90 backdrop-blur-xl',
        'border-b border-border/40'
      )}
      data-testid="alpha-top-nav"
    >
      {/* Left: Logo */}
      <Link href="/dashboard" className="flex items-center gap-2 shrink-0">
        <span className="text-lg font-bold font-quicksand">MeepleAI</span>
      </Link>

      {/* Center: Section title */}
      <div className="flex-1 flex items-center justify-center min-w-0 mx-4">
        <span className="text-sm font-medium text-muted-foreground font-quicksand truncate">
          {sectionTitle}
        </span>
      </div>

      {/* Right: Notification bell + User avatar */}
      <div className="flex items-center gap-2 shrink-0">
        {/* Notification bell with unread badge + NotificationCenter sheet */}
        <NotificationBell />

        {/* User avatar dropdown */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button
              type="button"
              className="flex items-center justify-center rounded-full focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              aria-label="User menu"
            >
              <Avatar className="h-8 w-8">
                <AvatarFallback className="text-xs bg-primary/10 text-primary">
                  <User className="w-4 h-4" />
                </AvatarFallback>
              </Avatar>
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-48">
            <DropdownMenuItem asChild>
              <Link href="/profile" className="flex items-center gap-2 cursor-pointer">
                <User className="w-4 h-4" />
                <span>Profile</span>
              </Link>
            </DropdownMenuItem>
            <DropdownMenuItem asChild>
              <Link href="/settings" className="flex items-center gap-2 cursor-pointer">
                <Settings className="w-4 h-4" />
                <span>Settings</span>
              </Link>
            </DropdownMenuItem>
            {isAdmin && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem asChild>
                  <Link href="/admin" className="flex items-center gap-2 cursor-pointer">
                    <Shield className="w-4 h-4" />
                    <span>Admin</span>
                  </Link>
                </DropdownMenuItem>
              </>
            )}
            <DropdownMenuSeparator />
            <DropdownMenuItem
              onClick={handleLogout}
              disabled={isLoggingOut}
              className="flex items-center gap-2 cursor-pointer text-destructive focus:text-destructive"
              data-testid="logout-menu-item"
            >
              <LogOut className="w-4 h-4" />
              <span>{isLoggingOut ? 'Disconnessione...' : 'Logout'}</span>
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
