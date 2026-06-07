/**
 * DirtyStateBar (Issue #1836)
 *
 * Sticky bottom bar that appears when there are unsaved local changes.
 * Used by admin tabs that adopt batch-save semantics (revert/preview/apply)
 * instead of immediate save on every interaction.
 *
 * Visible only when `dirtyCount > 0`.
 *
 * @example
 * ```tsx
 * <DirtyStateBar
 *   dirtyCount={pendingChanges.size}
 *   onRevert={revertPendingChanges}
 *   onApply={applyPendingChanges}
 *   applying={isApplying}
 * />
 * ```
 */

'use client';

import { Eye, Loader2, Save, Undo2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface DirtyStateBarProps {
  /**
   * Number of pending (unsaved) changes. The bar is hidden when 0.
   */
  dirtyCount: number;

  /**
   * Discard pending changes — reverts local state to last server snapshot.
   */
  onRevert: () => void;

  /**
   * Apply pending changes — persists them to the backend.
   */
  onApply: () => void;

  /**
   * Optional preview action (e.g. open a diff modal). Hidden if not provided.
   */
  onPreview?: () => void;

  /**
   * True while the apply is in flight — disables buttons and swaps the apply
   * label for a spinner.
   */
  applying?: boolean;

  /**
   * Custom item label (singular/plural) for the count text.
   * @default { singular: 'change', plural: 'changes' }
   */
  itemLabel?: { singular: string; plural: string };

  /**
   * Custom class name for the floating container.
   */
  className?: string;

  /**
   * Test ID hook for the container.
   * @default 'dirty-state-bar'
   */
  testId?: string;
}

export function DirtyStateBar({
  dirtyCount,
  onRevert,
  onApply,
  onPreview,
  applying = false,
  itemLabel = { singular: 'change', plural: 'changes' },
  className,
  testId = 'dirty-state-bar',
}: DirtyStateBarProps) {
  if (dirtyCount <= 0) return null;

  const countLabel = dirtyCount === 1 ? itemLabel.singular : itemLabel.plural;

  return (
    <div
      className={cn(
        'fixed inset-x-0 bottom-4 z-50 px-4 pointer-events-none',
        'flex justify-center',
        className
      )}
      role="region"
      aria-label="Unsaved changes"
      data-testid={testId}
    >
      <div
        className={cn(
          'pointer-events-auto',
          'w-full max-w-3xl',
          'flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between',
          'rounded-2xl px-4 py-3',
          'border border-[hsl(var(--c-warning)/0.4)] bg-card/95 backdrop-blur-md',
          'shadow-lg shadow-black/10',
          'animate-in slide-in-from-bottom-2 fade-in-0 duration-200'
        )}
      >
        {/* Left: pending count */}
        <div className="flex items-center gap-3 min-w-0">
          <span
            className={cn(
              'inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full',
              'bg-[hsl(var(--c-warning)/0.15)] text-[hsl(var(--c-warning))]',
              'text-sm font-semibold tabular-nums'
            )}
            aria-hidden="true"
            data-testid={`${testId}-count`}
          >
            {dirtyCount}
          </span>
          <div className="flex flex-col min-w-0">
            <p className="text-sm font-medium text-foreground">
              {dirtyCount} unsaved {countLabel}
            </p>
            <p className="text-xs text-muted-foreground">
              Review and apply, or discard to roll back.
            </p>
          </div>
        </div>

        {/* Right: actions */}
        <div className="flex items-center gap-2 sm:shrink-0">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={onRevert}
            disabled={applying}
            data-testid={`${testId}-revert`}
            aria-label="Discard unsaved changes"
          >
            <Undo2 className="h-4 w-4 mr-1.5" aria-hidden="true" />
            Discard
          </Button>

          {onPreview && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onPreview}
              disabled={applying}
              data-testid={`${testId}-preview`}
              aria-label="Preview unsaved changes"
            >
              <Eye className="h-4 w-4 mr-1.5" aria-hidden="true" />
              Preview
            </Button>
          )}

          <Button
            type="button"
            variant="default"
            size="sm"
            onClick={onApply}
            disabled={applying}
            data-testid={`${testId}-apply`}
            aria-label="Apply unsaved changes"
            aria-busy={applying || undefined}
          >
            {applying ? (
              <>
                <Loader2 className="h-4 w-4 mr-1.5 animate-spin" aria-hidden="true" />
                Applying…
              </>
            ) : (
              <>
                <Save className="h-4 w-4 mr-1.5" aria-hidden="true" />
                Apply
              </>
            )}
          </Button>
        </div>
      </div>
    </div>
  );
}

DirtyStateBar.displayName = 'DirtyStateBar';

export default DirtyStateBar;
