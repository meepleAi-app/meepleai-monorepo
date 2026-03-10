'use client';

/**
 * AdaptiveBottomBar - Unified mobile bottom navigation
 *
 * Consolidates MobileTabBar + FloatingActionBar + SmartFAB into a single
 * adaptive component with two rows:
 *   1. Contextual Actions (auto-hides when empty)
 *   2. Persistent Tab Bar (always visible)
 *
 * Mobile-only: hidden on md+ breakpoints.
 */

import { BookOpen, Compass, MessageSquare, Trophy, User } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useNavigation } from '@/context/NavigationContext';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { cn } from '@/lib/utils';
import type { NavAction } from '@/types/navigation';

// ── Persistent Tab Configuration ────────────────────────────────────────────

const TAB_ITEMS = [
  { id: 'library', label: 'Libreria', href: '/library', icon: BookOpen },
  { id: 'discover', label: 'Catalogo', href: '/discover', icon: Compass },
  { id: 'chat', label: 'Chat', href: '/chat', icon: MessageSquare, authOnly: true },
  { id: 'sessions', label: 'Sessioni', href: '/sessions', icon: Trophy, authOnly: true },
  { id: 'profile', label: 'Profilo', href: '/profile', icon: User, authOnly: true },
] as const;

// ── Action Button ───────────────────────────────────────────────────────────

function ActionButton({ action }: { action: NavAction }) {
  const isPrimary = action.variant === 'primary';
  const Icon = action.icon;

  return (
    <button
      onClick={action.onClick}
      disabled={action.disabled}
      className={cn(
        'flex items-center gap-1.5 rounded-xl px-3 py-2 text-xs font-semibold font-nunito',
        'transition-colors duration-150',
        isPrimary
          ? 'bg-primary text-primary-foreground flex-1 justify-center'
          : 'bg-muted/60 text-muted-foreground',
        action.disabled && 'opacity-50 cursor-not-allowed'
      )}
      aria-disabled={action.disabled || undefined}
      title={action.disabled ? action.disabledTooltip : undefined}
    >
      {Icon && <Icon className="h-3.5 w-3.5" />}
      {isPrimary && <span>{action.label}</span>}
    </button>
  );
}

// ── Main Component ──────────────────────────────────────────────────────────

export interface AdaptiveBottomBarProps {
  sidebarCollapsed?: boolean;
  className?: string;
}

export function AdaptiveBottomBar({ className }: AdaptiveBottomBarProps) {
  const pathname = usePathname();
  const { actionBarActions } = useNavigation();
  const { data: user } = useCurrentUser();
  const isAuthenticated = !!user;

  const visibleActions = actionBarActions.filter(a => !a.hidden);
  const hasActions = visibleActions.length > 0;

  const visibleTabs = TAB_ITEMS.filter(tab => !('authOnly' in tab && tab.authOnly) || isAuthenticated);

  return (
    <div
      data-testid="adaptive-bottom-bar"
      className={cn(
        'fixed bottom-0 left-0 right-0 z-40 md:hidden',
        'bg-background/97 backdrop-blur-xl',
        'border-t border-border/60',
        'pb-[env(safe-area-inset-bottom)]',
        className
      )}
    >
      {/* Contextual Actions Row (auto-hides when empty) */}
      {hasActions && (
        <div
          data-testid="adaptive-actions"
          className="flex gap-1.5 px-3 py-2 border-b border-border/30"
        >
          {visibleActions.slice(0, 4).map(action => (
            <ActionButton key={action.id} action={action} />
          ))}
        </div>
      )}

      {/* Persistent Tab Row */}
      <nav
        role="navigation"
        aria-label="Main navigation"
        className="flex items-center justify-around px-2 py-1.5"
      >
        {visibleTabs.map(tab => {
          const isActive = pathname === tab.href || pathname.startsWith(tab.href + '/');
          const Icon = tab.icon;

          return (
            <Link
              key={tab.id}
              href={tab.href}
              className={cn(
                'flex flex-col items-center gap-0.5 min-w-[48px] py-1',
                'text-[10px] font-semibold font-nunito',
                'transition-colors duration-150',
                isActive ? 'text-primary' : 'text-muted-foreground'
              )}
              aria-current={isActive ? 'page' : undefined}
            >
              <Icon className="h-5 w-5" strokeWidth={isActive ? 2.5 : 2} />
              <span>{tab.label}</span>
            </Link>
          );
        })}
      </nav>
    </div>
  );
}
