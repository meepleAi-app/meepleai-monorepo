'use client';

/**
 * ActionBarOverflow — Overflow dropdown menu for NavActionBar
 * Issue #5038 — ActionBar Component
 *
 * Renders excess actions (beyond the visible cap) in a "•••" dropdown menu.
 * Used on mobile when actions > 4, or on desktop when actions > 6.
 */

import { MoreHorizontal } from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { cn } from '@/lib/utils';
import type { NavAction } from '@/types/navigation';

// ─── Props ────────────────────────────────────────────────────────────────────

export interface ActionBarOverflowProps {
  actions: NavAction[];
}

// ─── Component ────────────────────────────────────────────────────────────────

/**
 * ActionBarOverflow
 *
 * "•••" button + dropdown listing overflow actions.
 * Excluded actions with `hidden: true` are filtered out upstream.
 */
export function ActionBarOverflow({ actions }: ActionBarOverflowProps) {
  if (actions.length === 0) return null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label="Altre azioni"
          aria-haspopup="menu"
          data-testid="action-bar-overflow"
          className={cn(
            'inline-flex items-center gap-1.5 rounded-lg px-2.5 py-2',
            'text-sm font-medium text-foreground/80',
            'hover:bg-accent hover:text-foreground',
            'transition-colors duration-150',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          <MoreHorizontal className="h-4 w-4" aria-hidden="true" />
        </button>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" side="top" sideOffset={8} className="w-48">
        {actions.map(action => {
          const Icon = action.icon;
          return (
            <DropdownMenuItem
              key={action.id}
              onClick={action.disabled ? undefined : action.onClick}
              disabled={action.disabled}
              className="flex cursor-pointer items-center gap-2"
              data-testid={`action-bar-overflow-item-${action.id}`}
            >
              <Icon className="h-4 w-4 shrink-0" aria-hidden="true" />
              <span>{action.label}</span>
              {action.badge !== undefined && action.badge !== null && (
                <span className="ml-auto text-xs text-muted-foreground tabular-nums">
                  {action.badge}
                </span>
              )}
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
