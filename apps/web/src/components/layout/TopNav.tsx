/**
 * TopNav - Desktop top navigation (Global)
 *
 * Primary desktop navigation bar, hidden on mobile (<768px).
 * Complements BottomNav for responsive navigation.
 *
 * Design: Playful Boardroom - wireframes-playful-boardroom.md
 * Issue: #2053 - User notifications with bell icon
 *
 * Features:
 * - Same nav items as BottomNav for consistency
 * - Fixed top sticky nav, hidden on mobile
 * - Active state detection (path matching)
 * - Keyboard navigation support
 * - User menu with logout
 * - NotificationBell with unread count badge
 */

'use client';

import { useTransition } from 'react';

import { Home, Gamepad2, MessageSquare, Settings, LogOut } from 'lucide-react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
import { NotificationBell } from '@/components/notifications';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { cn } from '@/lib/utils';

import { Button } from '../ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '../ui/dropdown-menu';
import { MeepleLogo } from '../ui/meeple-logo';

interface NavItem {
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  ariaLabel: string;
}

const navItems: NavItem[] = [
  {
    href: '/dashboard',
    icon: Home,
    label: 'Home',
    ariaLabel: 'Navigate to dashboard home',
  },
  {
    href: '/games',
    icon: Gamepad2,
    label: 'Giochi',
    ariaLabel: 'Navigate to games catalog',
  },
  {
    href: '/chat',
    icon: MessageSquare,
    label: 'Chat',
    ariaLabel: 'Navigate to chat interface',
  },
  {
    href: '/settings',
    icon: Settings,
    label: 'Impostazioni',
    ariaLabel: 'Navigate to settings',
  },
];

export function TopNav() {
  const pathname = usePathname();
  const router = useRouter();
  const { data: user } = useCurrentUser();
  const [isLoggingOut, startTransition] = useTransition();

  const isActive = (href: string) => {
    if (href === '/dashboard') {
      return pathname === '/dashboard' || pathname === '/';
    }
    return pathname?.startsWith(href) ?? false;
  };

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

  return (
    <nav
      className="hidden md:flex fixed top-0 left-0 right-0 h-16 bg-card border-b border-border shadow-sm z-50"
      aria-label="Primary desktop navigation"
    >
      <div className="container mx-auto flex items-center justify-between h-full px-4">
        {/* Logo */}
        <Link href="/dashboard" className="flex items-center gap-2">
          <MeepleLogo variant="icon" size="sm" />
          <span className="font-quicksand font-bold text-xl text-foreground">MeepleAI</span>
        </Link>

        {/* Navigation Items */}
        <div className="flex items-center gap-1">
          {navItems.map(({ href, icon: Icon, label, ariaLabel }) => {
            const active = isActive(href);
            return (
              <Link
                key={href}
                href={href}
                aria-label={ariaLabel}
                aria-current={active ? 'page' : undefined}
                className={cn(
                  'flex items-center gap-2 px-4 py-2 rounded-lg',
                  'transition-colors duration-200',
                  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2',
                  active
                    ? 'bg-primary/10 text-primary font-semibold'
                    : 'text-muted-foreground hover:text-foreground hover:bg-muted'
                )}
              >
                <Icon className="w-5 h-5" aria-hidden="true" />
                <span className="text-sm">{label}</span>
              </Link>
            );
          })}
        </div>

        {/* Notification Bell + User Menu */}
        <div className="flex items-center gap-2">
          <NotificationBell />

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" className="flex items-center gap-2">
                <div className="w-8 h-8 rounded-full bg-primary/20 flex items-center justify-center">
                  <span className="text-sm font-semibold text-primary">{userInitial}</span>
                </div>
                <span className="text-sm text-muted-foreground hidden lg:inline">
                  {user?.displayName || user?.email || 'Utente'}
                </span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-48">
              <DropdownMenuItem asChild data-testid="settings-menu-item">
                <Link href="/settings" className="flex items-center gap-2">
                  <Settings className="w-4 h-4" />
                  Impostazioni
                </Link>
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                onClick={handleLogout}
                disabled={isLoggingOut}
                className="flex items-center gap-2 text-destructive focus:text-destructive"
                data-testid="logout-menu-item"
              >
                <LogOut className="w-4 h-4" />
                {isLoggingOut ? 'Disconnessione...' : 'Esci'}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>
    </nav>
  );
}
