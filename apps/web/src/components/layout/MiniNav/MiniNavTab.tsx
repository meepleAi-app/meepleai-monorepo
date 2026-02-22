'use client';

/**
 * MiniNavTab — Individual tab item for the MiniNav bar
 * Issue #5037 — MiniNav Component
 *
 * Link-based tab with active underline highlight, optional icon, and badge support.
 */

import Link from 'next/link';

import type { NavTab } from '@/types/navigation';
import { NAV_TEST_IDS } from '@/lib/test-ids';
import { cn } from '@/lib/utils';

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
 * Active state shows an animated underline.
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
        // Base layout
        'relative flex items-center gap-1.5 whitespace-nowrap px-3 py-2.5',
        'text-sm font-medium transition-colors duration-150',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
        // Inactive state
        'text-muted-foreground hover:text-foreground',
        // Active state
        isActive && 'text-foreground',
      )}
    >
      {Icon && (
        <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
      )}

      <span>{tab.label}</span>

      {/* Badge */}
      {tab.badge !== undefined && tab.badge !== null && (
        <span
          className={cn(
            'ml-1 inline-flex h-5 min-w-[1.25rem] items-center justify-center',
            'rounded-full px-1.5 text-[10px] font-semibold tabular-nums',
            isActive
              ? 'bg-primary text-primary-foreground'
              : 'bg-muted text-muted-foreground',
          )}
          aria-label={`${tab.badge} items`}
        >
          {tab.badge}
        </span>
      )}

      {/* Active underline — animated */}
      <span
        className={cn(
          'absolute inset-x-0 bottom-0 h-0.5 rounded-full',
          'transition-all duration-200',
          isActive ? 'bg-primary scale-x-100' : 'scale-x-0',
        )}
        aria-hidden="true"
      />
    </Link>
  );
}
