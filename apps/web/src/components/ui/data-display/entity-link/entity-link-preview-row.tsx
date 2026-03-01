'use client';

/**
 * EntityLinkPreviewRow — C2
 *
 * Footer row showing a preview of the first EntityLink on a MeepleCard.
 * Displayed after the CardNavigationFooter, separated by a dashed divider.
 *
 * Issue #5158 — Epic C2
 */

import React from 'react';

import { Link as LinkIcon, ChevronRight } from 'lucide-react';

import { cn } from '@/lib/utils';

import { EntityLinkChip } from './entity-link-chip';

import type { EntityLinkType } from './entity-link-types';

export interface EntityLinkPreviewRowProps {
  /** Link type for the first link */
  linkType: EntityLinkType;
  /** Name of the target entity */
  targetName: string;
  /** Total number of links (used to show "+N" overflow) */
  totalCount: number;
  /** Called when the row is clicked */
  onClick?: () => void;
  className?: string;
  'data-testid'?: string;
}

export function EntityLinkPreviewRow({
  linkType,
  targetName,
  totalCount,
  onClick,
  className,
  'data-testid': testId,
}: EntityLinkPreviewRowProps) {
  if (totalCount <= 0) return null;

  const overflow = totalCount - 1;

  return (
    <>
      {/* Dashed divider */}
      <div className="mx-3 border-t border-dashed border-border/40" aria-hidden="true" />

      {/* Preview row */}
      <button
        type="button"
        onClick={e => {
          e.stopPropagation();
          onClick?.();
        }}
        className={cn(
          'group/links flex w-full items-center gap-2',
          'px-3 py-1.5',
          'cursor-pointer rounded-b-2xl',
          'text-xs font-nunito text-left',
          'transition-all duration-[350ms] cubic-bezier[0.4,0,0.2,1]',
          'hover:bg-white/10',
          'focus:outline-none',
          className
        )}
        aria-label={`${totalCount} link${totalCount !== 1 ? 's' : ''} — view all`}
        data-testid={testId ?? 'entity-link-preview-row'}
      >
        {/* Chain icon */}
        <LinkIcon className="h-3 w-3 shrink-0 text-muted-foreground/60" aria-hidden="true" />

        {/* Link type chip */}
        <EntityLinkChip linkType={linkType} size="sm" />

        {/* Target name */}
        <span className="min-w-0 flex-1 truncate text-muted-foreground font-medium">
          {targetName}
        </span>

        {/* Overflow count */}
        {overflow > 0 && (
          <span className="shrink-0 text-muted-foreground/70 text-[10px]">+{overflow}</span>
        )}

        {/* Arrow */}
        <ChevronRight
          className="h-3 w-3 shrink-0 text-muted-foreground/40 transition-transform duration-150 group-hover/links:translate-x-0.5"
          aria-hidden="true"
        />
      </button>
    </>
  );
}

export default EntityLinkPreviewRow;
