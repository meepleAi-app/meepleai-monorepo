'use client';

import React from 'react';

import { LockIcon, BookOpenIcon, PlusCircleIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface Source {
  reference: string;
  text: string | null;
  imageUrl: string | null;
  hasAccess: boolean;
}

interface SourceReferenceProps {
  source: Source;
  onAddToCollection?: () => void;
  className?: string;
}

interface ContentGatingBannerProps {
  onAddToCollection?: () => void;
}

// ============================================================================
// SourceReference
// ============================================================================

/**
 * SourceReference — renders a single source from an agent response.
 *
 * When the user owns the game (`hasAccess: true`) the full content is shown.
 * When the user does not own the game the content is blurred and a prompt
 * to add the game to their collection is displayed instead.
 */
export const SourceReference = React.memo<SourceReferenceProps>(
  ({ source, onAddToCollection, className }) => {
    if (source.hasAccess) {
      return (
        <div
          className={cn(
            'rounded-lg border border-border bg-muted/40 p-3',
            'dark:bg-muted/20',
            className
          )}
        >
          {/* Header */}
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <BookOpenIcon className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
            <span className="truncate">{source.reference}</span>
          </div>

          {/* Text content */}
          {source.text && <p className="mt-2 text-sm leading-relaxed">{source.text}</p>}

          {/* Image content */}
          {source.imageUrl && (
            <img
              src={source.imageUrl}
              alt={`Source from ${source.reference}`}
              className="mt-2 max-h-48 rounded object-contain"
            />
          )}
        </div>
      );
    }

    // ---- Locked state ----
    return (
      <div
        className={cn(
          'rounded-lg border border-border bg-muted/30 p-3',
          'dark:bg-muted/10',
          className
        )}
      >
        {/* Header with lock indicator */}
        <div className="flex items-center justify-between gap-1.5 text-xs text-muted-foreground">
          <span className="flex items-center gap-1.5 truncate">
            <BookOpenIcon className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
            <span className="truncate">{source.reference}</span>
          </span>
          <LockIcon className="h-3.5 w-3.5 shrink-0 text-amber-500" aria-label="Content locked" />
        </div>

        {/* Blurred placeholder */}
        <div className="mt-2 select-none text-sm text-muted-foreground/60 blur-[2px]">
          Add this game to your collection to see the full content
        </div>

        {/* Add to collection CTA */}
        {onAddToCollection && (
          <button
            type="button"
            onClick={onAddToCollection}
            className={cn(
              'mt-2 inline-flex items-center gap-1 text-xs font-medium',
              'text-amber-600 hover:text-amber-700',
              'dark:text-amber-500 dark:hover:text-amber-400',
              'transition-colors focus:outline-none focus:ring-2 focus:ring-amber-400 rounded'
            )}
          >
            <PlusCircleIcon className="h-3.5 w-3.5" aria-hidden="true" />
            Add to collection
          </button>
        )}
      </div>
    );
  }
);
SourceReference.displayName = 'SourceReference';

// ============================================================================
// ContentGatingBanner
// ============================================================================

/**
 * ContentGatingBanner — displayed at the top of a chat thread when the user
 * has ReferenceOnly access (does not own the game). Informs them that AI
 * responses will include rulebook references but not the full source text.
 */
export const ContentGatingBanner: React.FC<ContentGatingBannerProps> = ({ onAddToCollection }) => {
  return (
    <div
      className={cn(
        'flex items-center gap-3 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3',
        'dark:border-amber-500/30 dark:bg-amber-950/30'
      )}
      role="status"
    >
      <LockIcon className="h-4 w-4 shrink-0 text-amber-500" aria-hidden="true" />

      <p className="flex-1 text-sm text-amber-800 dark:text-amber-200">
        You don&apos;t own this game. Responses include rulebook references but not the full text.
      </p>

      {onAddToCollection && (
        <button
          type="button"
          onClick={onAddToCollection}
          className={cn(
            'shrink-0 text-sm font-medium',
            'text-amber-600 hover:text-amber-700',
            'dark:text-amber-400 dark:hover:text-amber-300',
            'transition-colors focus:outline-none focus:ring-2 focus:ring-amber-400 rounded'
          )}
        >
          Add to collection &rarr;
        </button>
      )}
    </div>
  );
};
ContentGatingBanner.displayName = 'ContentGatingBanner';
