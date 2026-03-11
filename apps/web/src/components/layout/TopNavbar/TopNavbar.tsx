/**
 * TopNavbar — Slim horizontal bar (Level 1)
 * Issue #5036 — Navbar Component
 *
 * Slim bar with logo + notifications + user menu.
 * Navigation has moved to the left Sidebar (desktop) and MobileNavDrawer (mobile).
 *
 *   [☰ (mobile)] [🎲 MeepleAI]              [🔔] [Avatar ▾]
 */

'use client';

import { Suspense } from 'react';

import { ChevronDown, LogOut, Settings, User } from 'lucide-react';
import Link from 'next/link';

import { logoutAction } from '@/actions/auth';
import { NotificationBell } from '@/components/notifications';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';
import { useAuthUser } from '@/hooks/useAuthUser';
import { useScrollState } from '@/hooks/useScrollState';
import { cn } from '@/lib/utils';

import { MobileNavDrawer } from '../MobileNavDrawer';
import { Logo } from '../Navbar/Logo';

// ─── User Menu ────────────────────────────────────────────────────────────────

interface UserMenuProps {
  userName?: string;
  userRole?: string;
}

function UserMenu({ userName, userRole }: UserMenuProps) {
  const initials = userName
    ? userName
        .split(' ')
        .map(w => w[0])
        .join('')
        .toUpperCase()
        .slice(0, 2)
    : 'U';

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          aria-label="User menu"
          className={cn(
            'flex items-center gap-2 rounded-lg px-2 py-1.5',
            'transition-colors duration-200 hover:bg-muted'
          )}
        >
          <div className="h-7 w-7 rounded-full bg-primary flex items-center justify-center">
            <span className="text-xs font-bold text-primary-foreground font-quicksand">
              {initials}
            </span>
          </div>
          <ChevronDown className="h-3 w-3 text-muted-foreground hidden sm:block" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-52">
        <DropdownMenuLabel className="font-quicksand">
          <p className="text-sm font-semibold truncate">{userName ?? 'Utente'}</p>
          <p className="text-xs text-muted-foreground capitalize font-normal">
            {userRole ?? 'user'}
          </p>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link href="/profile" className="gap-2.5">
            <User className="h-4 w-4" />
            Il mio profilo
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <Link href="/profile?tab=settings" className="gap-2.5">
            <Settings className="h-4 w-4" />
            Impostazioni
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <div className="px-2 py-1">
          <ThemeToggle showLabel size="sm" className="w-full justify-start" />
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          onClick={() => void logoutAction()}
          className="text-destructive focus:text-destructive gap-2.5"
        >
          <LogOut className="h-4 w-4" />
          Esci
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

// ─── TopNavbar ────────────────────────────────────────────────────────────────

export interface TopNavbarProps {
  className?: string;
}

export function TopNavbar({ className }: TopNavbarProps) {
  const { user } = useAuthUser();
  const { isScrolled: scrolled } = useScrollState({ scrolledThreshold: 4 });

  return (
    <>
      {/* Skip to main content — keyboard/screen-reader navigation (WCAG 2.4.1) */}
      <a
        href="#main-content"
        className={cn(
          'sr-only focus:not-sr-only',
          'focus:fixed focus:top-2 focus:left-2 focus:z-[100]',
          'focus:px-4 focus:py-2 focus:rounded-lg',
          'focus:bg-background focus:text-foreground',
          'focus:border focus:border-border',
          'focus:text-sm focus:font-semibold focus:font-nunito',
          'focus:shadow-md'
        )}
      >
        Vai al contenuto principale
      </a>

      <header
        data-testid="top-navbar"
        className={cn(
          'sticky top-0 z-40 w-full',
          'h-14',
          'bg-background/95 backdrop-blur-md backdrop-saturate-150',
          'border-b border-border/60',
          'transition-shadow duration-200',
          scrolled && 'shadow-sm',
          className
        )}
      >
        <div className="flex h-full items-center justify-between px-4 md:px-6 gap-2">
          {/* ── LEFT: MobileNavDrawer (hamburger on mobile) + Logo ── */}
          <div className="flex items-center gap-2 shrink-0">
            <Suspense>
              <MobileNavDrawer />
            </Suspense>
            <Logo variant="auto" size="sm" />
          </div>

          {/* ── RIGHT: Notifications + Avatar ── */}
          <div className="flex items-center gap-1 shrink-0">
            <NotificationBell />
            <UserMenu userName={user?.displayName ?? user?.email} userRole={user?.role} />
          </div>
        </div>
      </header>
    </>
  );
}
