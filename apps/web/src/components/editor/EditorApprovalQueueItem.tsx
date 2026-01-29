/**
 * Editor Approval Queue Item Component (Issue #2895, Extended #2896)
 *
 * Displays a single approval queue item with:
 * - Game cover and title
 * - Submission timestamp
 * - Priority indicator (high/medium/low)
 * - Changes description badge
 * - Action buttons: Review, Approve, Reject
 * - Optional checkbox for bulk selection (#2896)
 */

import React from 'react';
import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import Image from 'next/image';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Eye, Check, X } from 'lucide-react';
import { calculatePriority, getPriorityBadgeVariant, getPriorityLabel } from '@/lib/utils/priority';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';
import { cn } from '@/lib/utils';

export interface EditorApprovalQueueItemProps {
  /**
   * Shared game pending approval
   */
  game: SharedGame;

  /**
   * Callback when Review button is clicked
   */
  onReview: (gameId: string) => void;

  /**
   * Callback when Approve button is clicked
   */
  onApprove: (gameId: string) => void;

  /**
   * Callback when Reject button is clicked
   */
  onReject: (gameId: string) => void;

  /**
   * Optional: Whether item is selected for bulk operations (Issue #2896)
   */
  selected?: boolean;

  /**
   * Optional: Callback when checkbox is toggled (Issue #2896)
   */
  onToggleSelect?: (gameId: string) => void;

  /**
   * Optional CSS class name
   */
  className?: string;

  /**
   * Test ID for automated testing
   */
  'data-testid'?: string;
}

/**
 * EditorApprovalQueueItem Component
 *
 * Displays an approval queue item with priority indicators and actions.
 * Priority is calculated client-side based on submission age:
 * - High (>7 days): Red badge
 * - Medium (3-7 days): Yellow badge
 * - Low (<3 days): No badge displayed
 *
 * Extended in Issue #2896 to support bulk selection with optional checkbox.
 */
export function EditorApprovalQueueItem({
  game,
  onReview,
  onApprove,
  onReject,
  selected,
  onToggleSelect,
  className,
  'data-testid': testId = 'editor-approval-queue-item',
}: EditorApprovalQueueItemProps) {
  const priority = calculatePriority(game.createdAt);
  const showPriorityBadge = priority !== 'low'; // Only show badge for medium/high

  // Format submission time: "2 giorni fa", "3 ore fa", etc.
  const submittedTime = formatDistanceToNow(new Date(game.createdAt), {
    addSuffix: true,
    locale: it,
  });

  const showCheckbox = onToggleSelect !== undefined;

  return (
    <div
      className={cn(
        'group relative rounded-lg border border-border/50 bg-card p-4',
        'hover:border-orange-200 hover:shadow-md transition-all duration-200',
        selected && 'border-orange-300 bg-orange-50/30',
        className
      )}
      data-testid={testId}
    >
      {/* Checkbox (Top Left) - Issue #2896 */}
      {showCheckbox && (
        <div className="absolute top-2 left-2">
          <Checkbox
            checked={selected}
            onCheckedChange={() => onToggleSelect(game.id)}
            aria-label={`Seleziona ${game.title}`}
            data-testid={`${testId}-checkbox`}
          />
        </div>
      )}

      {/* Priority Badge (Top Right) - Only show for medium/high */}
      {showPriorityBadge && (
        <div className="absolute top-2 right-2">
          <Badge
            variant={getPriorityBadgeVariant(priority)}
            className="text-xs font-medium"
            data-testid={`${testId}-priority-badge`}
          >
            {getPriorityLabel(priority)}
          </Badge>
        </div>
      )}

      <div className={cn('flex gap-4', showCheckbox && 'ml-8')}>
        {/* Game Cover */}
        <div className="relative h-24 w-24 flex-shrink-0 overflow-hidden rounded-md border border-border/30">
          {game.thumbnailUrl ? (
            <Image
              src={game.thumbnailUrl}
              alt={`${game.title} cover`}
              fill
              className="object-cover"
              sizes="96px"
              data-testid={`${testId}-cover`}
            />
          ) : (
            <div
              className="flex h-full w-full items-center justify-center bg-muted text-muted-foreground"
              data-testid={`${testId}-cover-placeholder`}
            >
              <span className="text-xs">No cover</span>
            </div>
          )}
        </div>

        {/* Game Info */}
        <div className="flex-1 min-w-0">
          {/* Title */}
          <h3
            className="text-lg font-semibold text-foreground line-clamp-2 mb-1 pr-24"
            data-testid={`${testId}-title`}
          >
            {game.title}
          </h3>

          {/* Metadata Row */}
          <div className="flex items-center gap-2 mb-3">
            {/* Submission Time */}
            <span
              className="text-sm text-muted-foreground"
              data-testid={`${testId}-submitted-time`}
            >
              Inviato {submittedTime}
            </span>

            {/* Changes Badge */}
            <Badge
              variant="outline"
              className="text-xs bg-amber-50 text-amber-700 border-amber-200"
              data-testid={`${testId}-changes-badge`}
            >
              Vedi modifiche
            </Badge>
          </div>

          {/* Action Buttons */}
          <div className="flex items-center gap-2">
            {/* Review Button (Primary) */}
            <Button
              size="sm"
              variant="default"
              onClick={() => onReview(game.id)}
              className="gap-1.5"
              data-testid={`${testId}-review-button`}
            >
              <Eye className="h-4 w-4" />
              Revisiona
            </Button>

            {/* Approve Button (Green) */}
            <Button
              size="sm"
              variant="outline"
              onClick={() => onApprove(game.id)}
              className="gap-1.5 border-green-200 text-green-700 hover:bg-green-50 hover:border-green-300"
              data-testid={`${testId}-approve-button`}
            >
              <Check className="h-4 w-4" />
              Approva
            </Button>

            {/* Reject Button (Red) */}
            <Button
              size="sm"
              variant="outline"
              onClick={() => onReject(game.id)}
              className="gap-1.5 border-red-200 text-red-700 hover:bg-red-50 hover:border-red-300"
              data-testid={`${testId}-reject-button`}
            >
              <X className="h-4 w-4" />
              Rifiuta
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}

EditorApprovalQueueItem.displayName = 'EditorApprovalQueueItem';
