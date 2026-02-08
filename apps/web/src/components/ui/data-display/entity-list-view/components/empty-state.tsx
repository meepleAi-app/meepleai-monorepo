/**
 * EmptyState - Empty list message display
 *
 * Simple component for displaying a message when no items are available.
 *
 * @module components/ui/data-display/entity-list-view/components/empty-state
 */

'use client';

import React from 'react';
import { Inbox } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface EmptyStateProps {
  /** Message to display (default: "No items to display") */
  message?: string;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}

/**
 * Empty state component for EntityListView
 */
export const EmptyState = React.memo(function EmptyState({
  message = 'No items to display',
  className,
  'data-testid': testId,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center',
        'py-16 px-4',
        'text-center',
        className
      )}
      data-testid={testId || 'empty-state'}
      role="status"
      aria-live="polite"
    >
      <Inbox className="w-12 h-12 text-muted-foreground/40 mb-4" aria-hidden="true" />
      <p className="text-muted-foreground text-sm md:text-base">{message}</p>
    </div>
  );
});
