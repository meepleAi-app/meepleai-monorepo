/**
 * WishlistButton - Wishlist Toggle Feature for MeepleCard
 * Issue #3824 - MeepleCard Feature Extensions
 *
 * Standalone wishlist toggle button for adding/removing games from wishlist.
 *
 * Features:
 * - Heart icon (outline/filled states)
 * - Toggle animation (heart beat)
 * - Tooltip with state-aware message
 * - Event propagation stopped (prevents card onClick)
 * - Keyboard accessible (Enter/Space)
 * - Loading state support
 *
 * @example
 * ```tsx
 * <WishlistButton
 *   gameId="game-123"
 *   isWishlisted={false}
 *   onToggle={(id, state) => console.log('Wishlist toggled:', id, state)}
 * />
 * ```
 */

'use client';

import { Heart } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface WishlistButtonProps {
  /** Game ID for wishlist operations */
  gameId: string;
  /** Current wishlist state */
  isWishlisted: boolean;
  /** Toggle callback (can be async for API calls) */
  onToggle: (gameId: string, isWishlisted: boolean) => void | Promise<void>;
  /** Loading state (e.g., during API call) */
  loading?: boolean;
  /** Size variant */
  size?: 'sm' | 'md' | 'lg';
  /** Additional CSS classes */
  className?: string;
  /** Test ID for testing */
  'data-testid'?: string;
}

// ============================================================================
// Component
// ============================================================================

export function WishlistButton({
  gameId,
  isWishlisted,
  onToggle,
  loading = false,
  size = 'md',
  className,
  'data-testid': testId,
}: WishlistButtonProps) {
  const handleClick = async (e: React.MouseEvent) => {
    // CRITICAL: Stop propagation to prevent card onClick
    e.stopPropagation();

    if (loading) return;

    try {
      await onToggle(gameId, !isWishlisted);
    } catch (error) {
      // Error handling delegated to parent (onToggle callback)
      // Component remains stable even if callback throws
      logger.error('WishlistButton: onToggle error:', error);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      e.stopPropagation();
      handleClick(e as unknown as React.MouseEvent);
    }
  };

  const tooltipContent = isWishlisted ? 'Rimuovi dalla wishlist' : 'Aggiungi alla wishlist';
  const ariaLabel = `Wishlist button, currently ${isWishlisted ? 'added' : 'not added'}`;

  const iconSize = size === 'sm' ? 'h-4 w-4' : size === 'lg' ? 'h-6 w-6' : 'h-5 w-5';

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <Button
            variant="ghost"
            size={size === 'sm' ? 'sm' : size === 'lg' ? 'lg' : 'icon'}
            onClick={handleClick}
            onKeyDown={handleKeyDown}
            disabled={loading}
            aria-label={ariaLabel}
            className={cn(
              'rounded-full transition-all duration-200',
              'hover:scale-110 active:scale-95',
              isWishlisted && 'text-rose-500 hover:text-rose-600',
              loading && 'opacity-50 cursor-wait',
              className
            )}
            data-testid={testId || 'wishlist-button'}
          >
            <Heart
              className={cn(
                iconSize,
                'transition-all duration-300',
                isWishlisted && [
                  'fill-current',
                  // Heart beat animation on wishlisted state
                  'animate-heart-beat',
                ]
              )}
            />
          </Button>
        </TooltipTrigger>
        <TooltipContent>
          <p>{tooltipContent}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

export default WishlistButton;
