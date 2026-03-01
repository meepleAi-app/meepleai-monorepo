'use client';

/**
 * EntityLinkBadge — C1
 *
 * Glassmorphism badge displayed in the top-right corner of a MeepleCard image.
 * Shows the total EntityLink count; clicking opens the drawer to the Links tab.
 *
 * Issue #5157 — Epic C1
 */

import React from 'react';

import { Link as LinkIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface EntityLinkBadgeProps {
  /** Total number of entity links */
  count: number;
  /** Called when the badge is clicked */
  onClick?: (e: React.MouseEvent) => void;
  className?: string;
  'data-testid'?: string;
}

export function EntityLinkBadge({
  count,
  onClick,
  className,
  'data-testid': testId,
}: EntityLinkBadgeProps) {
  if (count <= 0) return null;

  return (
    <button
      type="button"
      onClick={e => {
        e.stopPropagation();
        onClick?.(e);
      }}
      className={cn(
        // Position: absolute top-right corner over card image
        'absolute top-2 right-2 z-10',
        // Glass morphism style matching design spec
        'inline-flex items-center gap-1',
        'rounded-full px-2 py-0.5',
        'bg-white/80 backdrop-blur-[8px]',
        'border border-white/50',
        'shadow-sm',
        // Typography
        'text-xs font-semibold font-nunito text-slate-700',
        // Interaction
        'cursor-pointer transition-all duration-200',
        'hover:scale-105 hover:shadow-md hover:bg-white/90',
        'focus:outline-none focus:ring-2 focus:ring-slate-400/50 focus:ring-offset-1',
        className
      )}
      aria-label={`${count} connection${count !== 1 ? 's' : ''} — view links`}
      data-testid={testId ?? 'entity-link-badge'}
    >
      <LinkIcon className="h-3 w-3 text-slate-500" aria-hidden="true" />
      <span>{count}</span>
    </button>
  );
}

export default EntityLinkBadge;
