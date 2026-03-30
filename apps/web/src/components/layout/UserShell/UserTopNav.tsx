'use client';

import { useEffect, useRef } from 'react';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { SessionNavBar } from '@/components/dashboard/session-nav/SessionNavBar';
import { useDashboardMode } from '@/components/dashboard/useDashboardMode';
import { NotificationBell } from '@/components/notifications';
import { getSectionEmoji } from '@/config/navigation-emoji';
import { useNavigation } from '@/hooks/useNavigation';
import { useNavbarHeightStore } from '@/lib/stores/navbar-height-store';
import { useSessionStore } from '@/lib/stores/sessionStore';
import { cn } from '@/lib/utils';

import { ContextualCTA } from './ContextualCTA';
import { UserMenuDropdown } from '../UserMenuDropdown';

interface UserTopNavProps {
  isAdmin?: boolean;
  onMenuToggle?: () => void;
  isMenuOpen?: boolean;
}

export function UserTopNav({ isAdmin, onMenuToggle, isMenuOpen }: UserTopNavProps) {
  const { sectionTitle, breadcrumbs, showBreadcrumb } = useNavigation();
  const { isGameMode, activeSheet, openSheet, send } = useDashboardMode();
  const activeSession = useSessionStore(s => s.activeSession);
  const pathname = usePathname();
  const sectionEmoji = getSectionEmoji(pathname);
  const setNavbarHeight = useNavbarHeightStore(s => s.setHeight);

  // NavContextTabs removed — height is always 52px
  useEffect(() => {
    setNavbarHeight(52);
  }, [setNavbarHeight]);

  // Stable session start time — reset when entering game mode, never on re-render
  const sessionStartRef = useRef(new Date());
  useEffect(() => {
    if (isGameMode) sessionStartRef.current = new Date();
  }, [isGameMode]);

  const gameName = activeSession?.gameName ?? 'Game Session';

  return (
    <header
      className={cn(
        'sticky top-0 z-40',
        'h-[52px]',
        'flex items-center justify-between px-4',
        'bg-background/90 backdrop-blur-xl',
        'border-b border-border/40'
      )}
      data-testid="user-top-nav"
    >
      {/* Admin hamburger (mobile only) */}
      {isAdmin && onMenuToggle && (
        <button
          type="button"
          className="md:hidden flex items-center justify-center w-9 h-9 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
          onClick={onMenuToggle}
          aria-label="Open admin menu"
          aria-expanded={isMenuOpen}
          aria-controls="admin-mobile-drawer"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            className="w-5 h-5"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <line x1="4" x2="20" y1="12" y2="12" />
            <line x1="4" x2="20" y1="6" y2="6" />
            <line x1="4" x2="20" y1="18" y2="18" />
          </svg>
        </button>
      )}

      {/* Left: section emoji + title / breadcrumb */}
      <div className="flex items-center gap-1.5 text-sm truncate min-w-0 shrink-0">
        <span className="text-base" role="img" aria-hidden="true">
          {sectionEmoji}
        </span>
        {showBreadcrumb ? (
          <nav aria-label="Breadcrumb" className="flex items-center gap-1 truncate">
            {breadcrumbs.map((crumb, i) => (
              <span key={crumb.href} className="flex items-center gap-1">
                {i > 0 && <span className="text-muted-foreground/50">&rsaquo;</span>}
                {i === breadcrumbs.length - 1 ? (
                  <span className="font-medium text-foreground truncate">{crumb.label}</span>
                ) : (
                  <Link
                    href={crumb.href}
                    className="text-muted-foreground hover:text-foreground transition-colors truncate"
                  >
                    {crumb.label}
                  </Link>
                )}
              </span>
            ))}
          </nav>
        ) : (
          <span className="font-medium text-muted-foreground font-quicksand truncate">
            {sectionTitle}
          </span>
        )}
      </div>

      {/* Center: contextual CTA (desktop) or game session bar */}
      <div className="flex-1 flex items-center justify-center mx-4 min-w-0">
        {isGameMode ? (
          <SessionNavBar
            gameName={gameName}
            sessionStartedAt={sessionStartRef.current}
            isPaused={false}
            activeSheet={activeSheet}
            onOpenSheet={openSheet}
            onExit={() => send({ type: 'SESSION_DISMISSED' })}
          />
        ) : (
          <ContextualCTA />
        )}
      </div>

      {/* Right: utility actions */}
      <div className="flex items-center gap-2 shrink-0">
        <NotificationBell />
        <UserMenuDropdown />
      </div>
    </header>
  );
}
