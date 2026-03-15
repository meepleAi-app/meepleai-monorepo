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

import { type ReactNode, Suspense } from 'react';

import { ChevronDown, LogOut, Search, Settings, User } from 'lucide-react';
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
import { useCommandPalette } from '@/hooks/useCommandPalette';
import { useScrollState } from '@/hooks/useScrollState';
import { cn } from '@/lib/utils';

import { DesktopBreadcrumb } from '../Breadcrumb/DesktopBreadcrumb';
import { CommandPalette } from '../CommandPalette';
import { MobileNavDrawer } from '../MobileNavDrawer';
import { Logo } from './Logo';

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
  /** Optional mini-cards slot rendered between center and right sections */
  miniCards?: ReactNode;
}

export function TopNavbar({ className, miniCards }: TopNavbarProps) {
  const { user } = useAuthUser();
  const { isScrolled: scrolled } = useScrollState({ scrolledThreshold: 4 });
  const commandPalette = useCommandPalette();

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
          {/* ── LEFT: MobileNavDrawer (hamburger on mobile) + Logo + Breadcrumb ── */}
          <div className="flex items-center gap-2 min-w-0">
            <div className="flex items-center gap-2 shrink-0">
              <Suspense>
                <MobileNavDrawer />
              </Suspense>
              <Logo variant="auto" size="sm" />
            </div>
            <DesktopBreadcrumb className="hidden md:flex ml-2" />
          </div>

          {/* ── CENTER: Mini cards (mobile, collapsed hand) ── */}
          {miniCards && <div className="flex items-center gap-1 overflow-x-auto">{miniCards}</div>}

          {/* ── RIGHT: Search + Notifications + Avatar ── */}
          <div className="flex items-center gap-1 shrink-0">
            <button
              onClick={commandPalette.toggle}
              aria-label="Apri ricerca"
              className={cn(
                'hidden md:flex items-center gap-2',
                'rounded-lg border border-border/60 bg-muted/40',
                'px-3 py-1.5 text-sm text-muted-foreground',
                'hover:bg-muted hover:border-border transition-colors duration-200',
                'min-w-[200px] max-w-[280px]'
              )}
            >
              <Search className="h-3.5 w-3.5 shrink-0" />
              <span className="truncate">Cerca...</span>
              <kbd
                className={cn(
                  'ml-auto hidden lg:inline-flex',
                  'h-5 items-center gap-0.5 rounded border border-border/80',
                  'bg-background px-1.5 text-[10px] font-medium text-muted-foreground'
                )}
              >
                <span className="text-xs">&#x2318;</span>K
              </kbd>
            </button>
            <NotificationBell />
            <UserMenu userName={user?.displayName ?? user?.email} userRole={user?.role} />
          </div>
        </div>
      </header>

      <CommandPalette
        isOpen={commandPalette.isOpen}
        onClose={commandPalette.close}
        dataSources={{}}
      />
    </>
  );
}
