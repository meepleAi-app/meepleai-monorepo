'use client';

/**
 * MiniNavTab — Individual tab item for the MiniNav bar
 * Issue #5037 — MiniNav Component
 *
 * Link-based tab with active pill highlight, optional icon, and badge support.
 */

import Link from 'next/link';

import { NAV_TEST_IDS } from '@/lib/test-ids';
import { cn } from '@/lib/utils';
import type { NavTab } from '@/types/navigation';

// ─── Props ────────────────────────────────────────────────────────────────────

export interface MiniNavTabProps {
  tab: NavTab;
  isActive: boolean;
  onClick?: () => void;
}

// ─── Component ────────────────────────────────────────────────────────────────

/**
 * MiniNavTab
 *
 * Single tab in the MiniNav bar.
 * Renders as a Next.js Link with ARIA tab role.
 * Active state shows a pill highlight.
 */
export function MiniNavTab({ tab, isActive, onClick }: MiniNavTabProps) {
  const Icon = tab.icon;

  return (
    <Link
      href={tab.href}
      role="tab"
      aria-selected={isActive}
      data-testid={NAV_TEST_IDS.miniNavTab(tab.id)}
      onClick={onClick}
      className={cn(
        // Base layout — pill shape
        'relative flex items-center gap-1.5 whitespace-nowrap',
        'px-3.5 py-1.5 mx-0.5 rounded-lg',
        'text-sm font-medium transition-all duration-200',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
        // Active / inactive states
        isActive
          ? 'bg-primary/10 text-primary font-semibold'
          : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'
      )}
    >
      {Icon && <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />}

      <span>{tab.label}</span>

      {/* Badge */}
      {tab.badge !== undefined && tab.badge !== null && (
        <span
          className={cn(
            'ml-1 inline-flex h-5 min-w-[1.25rem] items-center justify-center',
            'rounded-full px-1.5 text-[10px] font-semibold tabular-nums',
            isActive ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'
          )}
          aria-label={`${tab.badge} items`}
        >
          {tab.badge}
        </span>
      )}
    </Link>
  );
}
