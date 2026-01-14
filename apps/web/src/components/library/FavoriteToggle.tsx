/**
 * FavoriteToggle Component
 *
 * Button to toggle favorite status of a game in user's library.
 * Features:
 * - Heart icon with filled/outline state
 * - Optimistic UI updates
 * - Loading state during mutation
 */

'use client';

import React from 'react';

import { Heart, Loader2 } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Button, type ButtonProps } from '@/components/ui/button';
import { useToggleLibraryFavorite } from '@/hooks/queries';
import { cn } from '@/lib/utils';

export interface FavoriteToggleProps extends Omit<ButtonProps, 'onClick'> {
  /** Game ID to toggle favorite */
  gameId: string;
  /** Current favorite status */
  isFavorite: boolean;
  /** Game title for toast messages */
  gameTitle?: string;
  /** Callback when favorite is toggled */
  onToggled?: (isFavorite: boolean) => void;
}

export const FavoriteToggle = React.memo(function FavoriteToggle({
  gameId,
  isFavorite,
  gameTitle = 'Game',
  onToggled,
  variant = 'ghost',
  size = 'icon',
  className,
  disabled,
  ...props
}: FavoriteToggleProps) {
  const toggleMutation = useToggleLibraryFavorite();

  const isLoading = toggleMutation.isPending;

  const handleClick = async () => {
    if (isLoading) return;

    try {
      await toggleMutation.mutateAsync({
        gameId,
        isFavorite: !isFavorite,
      });
      toast.success(
        `${gameTitle} has been ${!isFavorite ? 'added to' : 'removed from'} your favorites.`
      );
      onToggled?.(!isFavorite);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to update favorite status.');
    }
  };

  return (
    <Button
      variant={variant}
      size={size}
      className={cn(
        'transition-colors',
        isFavorite && 'text-red-500 hover:text-red-600',
        className
      )}
      onClick={handleClick}
      disabled={disabled || isLoading}
      aria-label={isFavorite ? 'Remove from favorites' : 'Add to favorites'}
      {...props}
    >
      {isLoading ? (
        <Loader2 className="h-4 w-4 animate-spin" />
      ) : (
        <Heart className={cn('h-4 w-4', isFavorite && 'fill-current')} />
      )}
    </Button>
  );
});
