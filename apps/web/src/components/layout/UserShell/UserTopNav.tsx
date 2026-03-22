'use client';

import { useEffect, useRef } from 'react';

import Link from 'next/link';

import { SessionNavBar } from '@/components/dashboard-v2/session-nav/SessionNavBar';
import { useDashboardMode } from '@/components/dashboard-v2/useDashboardMode';
import { NotificationBell } from '@/components/notifications';
import { useNavigation } from '@/hooks/useNavigation';
import { useSessionStore } from '@/lib/stores/sessionStore';
import { cn } from '@/lib/utils';

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

  // Stable session start time — reset when entering game mode, never on re-render
  const sessionStartRef = useRef(new Date());
  useEffect(() => {
    if (isGameMode) sessionStartRef.current = new Date();
  }, [isGameMode]);

  const gameName = activeSession?.gameName ?? 'Game Session';

  return (
    <header
      className={cn(
        'sticky top-0 z-40 h-12',
        'flex items-center justify-between px-4',
        'bg-background/90 backdrop-blur-xl',
        'border-b border-border/40'
      )}
      data-testid="user-top-nav"
    >
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

      <Link href="/dashboard" className="flex items-center gap-2 shrink-0">
        <span className="text-lg font-bold font-quicksand">MeepleAI</span>
      </Link>

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
        <div className="flex-1 flex items-center justify-center min-w-0 mx-4">
          {showBreadcrumb ? (
            <nav aria-label="Breadcrumb" className="flex items-center gap-1 text-sm truncate">
              {breadcrumbs.map((crumb, i) => (
                <span key={crumb.href} className="flex items-center gap-1">
                  {i > 0 && <span className="text-muted-foreground/50">/</span>}
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
            <span className="text-sm font-medium text-muted-foreground font-quicksand truncate">
              {sectionTitle}
            </span>
          )}
        </div>
      )}

      <div className={`flex items-center gap-2 shrink-0 ${isGameMode ? 'hidden sm:flex' : 'flex'}`}>
        <NotificationBell />
        <UserMenuDropdown />
      </div>
    </header>
  );
}
