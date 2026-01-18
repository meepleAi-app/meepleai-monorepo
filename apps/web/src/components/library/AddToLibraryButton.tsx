/**
 * AddToLibraryButton Component
 *
 * Toggle button for adding/removing games from user's library.
 * Features:
 * - Optimistic UI updates
 * - Loading state during mutation
 * - Toast notifications for feedback
 * - Authentication awareness
 */

'use client';

import React from 'react';

import { BookmarkPlus, BookmarkMinus, Loader2 } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Button, type ButtonProps } from '@/components/ui/button';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import {
  useGameInLibraryStatus,
  useAddGameToLibrary,
  useRemoveGameFromLibrary,
  useLibraryQuota,
} from '@/hooks/queries';
import { cn } from '@/lib/utils';

export interface AddToLibraryButtonProps extends Omit<ButtonProps, 'onClick'> {
  /** Game ID to add/remove from library */
  gameId: string;
  /** Game title for toast messages */
  gameTitle?: string;
  /** Show text label (default: true for larger sizes) */
  showLabel?: boolean;
  /** Callback when game is added */
  onAdded?: () => void;
  /** Callback when game is removed */
  onRemoved?: () => void;
}

export const AddToLibraryButton = React.memo(function AddToLibraryButton({
  gameId,
  gameTitle = 'Game',
  showLabel = true,
  onAdded,
  onRemoved,
  variant = 'outline',
  size = 'default',
  className,
  disabled,
  ...props
}: AddToLibraryButtonProps) {
  // Query game status in library
  const { data: status, isLoading: isLoadingStatus } = useGameInLibraryStatus(gameId);

  // Query library quota (Issue #2445)
  const { data: quota, isLoading: isLoadingQuota } = useLibraryQuota();

  // Mutations
  const addMutation = useAddGameToLibrary();
  const removeMutation = useRemoveGameFromLibrary();

  const isInLibrary = status?.inLibrary ?? false;
  const isLoading =
    isLoadingStatus || isLoadingQuota || addMutation.isPending || removeMutation.isPending;

  // Check quota limit (Issue #2445)
  const isQuotaReached = quota ? quota.currentCount >= quota.maxAllowed : false;
  const quotaTooltip = quota
    ? `Hai raggiunto il limite di ${quota.maxAllowed} giochi. Passa a ${
        quota.userTier === 'free' ? 'Normal' : 'Premium'
      } per aggiungerne altri.`
    : 'Loading quota...';

  const handleClick = async () => {
    if (isLoading) return;

    try {
      if (isInLibrary) {
        await removeMutation.mutateAsync(gameId);
        toast.success(`${gameTitle} rimosso dalla tua libreria.`);
        onRemoved?.();
      } else {
        // Check quota before adding (Issue #2445)
        if (isQuotaReached) {
          toast.error(quotaTooltip);
          return;
        }

        await addMutation.mutateAsync({ gameId });
        toast.success(`${gameTitle} aggiunto alla tua libreria.`);
        onAdded?.();
      }
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : `Impossibile ${isInLibrary ? 'rimuovere' : 'aggiungere'} il gioco.`
      );
    }
  };

  const Icon = isInLibrary ? BookmarkMinus : BookmarkPlus;
  const label = isInLibrary ? 'Rimuovi dalla Libreria' : 'Aggiungi alla Collezione';

  // Check if button should be disabled (Issue #2445)
  const isDisabled = disabled || isLoading || (!isInLibrary && isQuotaReached);

  const button = (
    <Button
      variant={isInLibrary ? 'secondary' : variant}
      size={size}
      className={cn('gap-2', className)}
      onClick={handleClick}
      disabled={isDisabled}
      aria-label={label}
      {...props}
    >
      {isLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Icon className="h-4 w-4" />}
      {showLabel && <span>{label}</span>}
    </Button>
  );

  // Wrap in tooltip when quota is reached (Issue #2445)
  if (!isInLibrary && isQuotaReached) {
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>{button}</TooltipTrigger>
          <TooltipContent className="max-w-xs">
            <p>{quotaTooltip}</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }

  return button;
});
