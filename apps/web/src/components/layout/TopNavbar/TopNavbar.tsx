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

import { Suspense, useEffect, useRef, useState } from 'react';

import { ChevronDown, LogOut, Settings, User } from 'lucide-react';
import Link from 'next/link';

import { logoutAction } from '@/actions/auth';
import { NotificationBell } from '@/components/notifications';
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';
import { useAuthUser } from '@/hooks/useAuthUser';
import { cn } from '@/lib/utils';

import { MobileNavDrawer } from '../MobileNavDrawer';
import { Logo } from '../Navbar/Logo';

// ─── User Menu ────────────────────────────────────────────────────────────────

interface UserMenuProps {
  userName?: string;
  userRole?: string;
}

function UserMenu({ userName, userRole }: UserMenuProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [open]);

  const initials = userName
    ? userName
        .split(' ')
        .map(w => w[0])
        .join('')
        .toUpperCase()
        .slice(0, 2)
    : 'U';

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen(!open)}
        aria-label="User menu"
        aria-haspopup="menu"
        aria-expanded={open}
        className={cn(
          'flex items-center gap-2 rounded-lg px-2 py-1.5',
          'transition-colors duration-200',
          'hover:bg-muted'
        )}
      >
        <div className="h-7 w-7 rounded-full bg-primary flex items-center justify-center">
          <span className="text-xs font-bold text-primary-foreground font-quicksand">
            {initials}
          </span>
        </div>
        <ChevronDown className="h-3 w-3 text-muted-foreground hidden sm:block" />
      </button>

      {open && (
        <div
          role="menu"
          className={cn(
            'absolute top-full right-0 mt-2 z-50',
            'w-52 rounded-xl border border-border bg-card',
            'shadow-lg shadow-black/10 p-1.5',
            'animate-in fade-in-0 zoom-in-95 duration-150'
          )}
        >
          {/* User info */}
          <div className="px-3 py-2 mb-1 border-b border-border">
            <p className="text-sm font-semibold font-quicksand text-foreground truncate">
              {userName ?? 'Utente'}
            </p>
            <p className="text-xs text-muted-foreground capitalize">{userRole ?? 'user'}</p>
          </div>

          <Link
            href="/profile"
            role="menuitem"
            onClick={() => setOpen(false)}
            className="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-foreground hover:bg-muted transition-colors"
          >
            <User className="h-4 w-4" />
            Il mio profilo
          </Link>
          <Link
            href="/profile?tab=settings"
            role="menuitem"
            onClick={() => setOpen(false)}
            className="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-foreground hover:bg-muted transition-colors"
          >
            <Settings className="h-4 w-4" />
            Impostazioni
          </Link>

          <div className="my-1 border-t border-border" />

          <div className="px-3 py-1">
            <ThemeToggle showLabel size="sm" className="w-full justify-start" />
          </div>

          <div className="my-1 border-t border-border" />

          <button
            role="menuitem"
            onClick={() => {
              setOpen(false);
              void logoutAction();
            }}
            className="flex w-full items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-destructive hover:bg-destructive/10 transition-colors"
          >
            <LogOut className="h-4 w-4" />
            Esci
          </button>
        </div>
      )}
    </div>
  );
}

// ─── TopNavbar ────────────────────────────────────────────────────────────────

export interface TopNavbarProps {
  className?: string;
}

export function TopNavbar({ className }: TopNavbarProps) {
  const { user } = useAuthUser();
  const [scrolled, setScrolled] = useState(false);

  // Shadow on scroll
  useEffect(() => {
    function onScroll() {
      setScrolled(window.scrollY > 4);
    }
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

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
