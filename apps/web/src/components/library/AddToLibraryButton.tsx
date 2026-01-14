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
  useGameInLibraryStatus,
  useAddGameToLibrary,
  useRemoveGameFromLibrary,
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

  // Mutations
  const addMutation = useAddGameToLibrary();
  const removeMutation = useRemoveGameFromLibrary();

  const isInLibrary = status?.inLibrary ?? false;
  const isLoading = isLoadingStatus || addMutation.isPending || removeMutation.isPending;

  const handleClick = async () => {
    if (isLoading) return;

    try {
      if (isInLibrary) {
        await removeMutation.mutateAsync(gameId);
        toast.success(`${gameTitle} has been removed from your library.`);
        onRemoved?.();
      } else {
        await addMutation.mutateAsync({ gameId });
        toast.success(`${gameTitle} has been added to your library.`);
        onAdded?.();
      }
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : `Failed to ${isInLibrary ? 'remove from' : 'add to'} library.`
      );
    }
  };

  const Icon = isInLibrary ? BookmarkMinus : BookmarkPlus;
  const label = isInLibrary ? 'Remove from Library' : 'Add to Library';

  return (
    <Button
      variant={isInLibrary ? 'secondary' : variant}
      size={size}
      className={cn('gap-2', className)}
      onClick={handleClick}
      disabled={disabled || isLoading}
      aria-label={label}
      {...props}
    >
      {isLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Icon className="h-4 w-4" />}
      {showLabel && <span>{label}</span>}
    </Button>
  );
});
