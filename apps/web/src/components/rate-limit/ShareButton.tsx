/**
 * ShareButton Component
 * Issue #2749: Frontend - Rate Limit Feedback UI
 *
 * Share button with rate limit awareness:
 * - Disabled when rate limit reached
 * - Tooltip showing block reason
 * - Checks for existing pending requests
 */

'use client';

import { Share2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { useRateLimitStatus } from '@/hooks/queries/useShareRequests';

export interface ShareButtonProps {
  /**
   * Game ID to share
   */
  gameId: string;

  /**
   * Callback when share button is clicked
   */
  onShare: () => void;

  /**
   * Optional existing pending request for this game
   */
  existingPendingRequest?: boolean;

  /**
   * Optional className for custom styling
   */
  className?: string;
}

/**
 * Calculate if user can create a new share request
 */
function canCreateRequest(status: {
  currentPendingCount: number;
  maxPendingAllowed: number;
  currentMonthlyCount: number;
  maxMonthlyAllowed: number;
  isInCooldown: boolean;
}): boolean {
  if (status.isInCooldown) return false;
  if (status.currentPendingCount >= status.maxPendingAllowed) return false;
  if (status.currentMonthlyCount >= status.maxMonthlyAllowed) return false;
  return true;
}

/**
 * Generate tooltip content for disabled button
 */
function getTooltipContent(
  status: {
    currentPendingCount: number;
    maxPendingAllowed: number;
    currentMonthlyCount: number;
    maxMonthlyAllowed: number;
    isInCooldown: boolean;
    cooldownEndsAt: string | null;
  },
  existingPendingRequest?: boolean
): string | null {
  if (existingPendingRequest) {
    return 'You already have a pending request for this game';
  }

  if (status.isInCooldown) {
    return status.cooldownEndsAt
      ? `Cooldown active until ${new Date(status.cooldownEndsAt).toLocaleDateString()}`
      : 'You are currently in cooldown period';
  }

  if (status.currentPendingCount >= status.maxPendingAllowed) {
    return 'Maximum pending requests reached';
  }

  if (status.currentMonthlyCount >= status.maxMonthlyAllowed) {
    return 'Monthly request limit reached';
  }

  return null;
}

/**
 * ShareButton Component
 *
 * Button for sharing games to community catalog with:
 * - Rate limit checking
 * - Disabled state when limits reached
 * - Tooltip with explanation
 *
 * @example
 * ```tsx
 * <ShareButton
 *   gameId="123e4567-e89b-12d3-a456-426614174000"
 *   onShare={() => console.log('Share clicked')}
 *   existingPendingRequest={false}
 * />
 * ```
 */
export function ShareButton({
  gameId,
  onShare,
  existingPendingRequest = false,
  className,
}: ShareButtonProps): JSX.Element {
  const { data: status } = useRateLimitStatus();

  // Default to disabled if status not loaded yet (conservative approach)
  const isDisabled =
    !status || !canCreateRequest(status) || existingPendingRequest;

  const tooltipContent = status
    ? getTooltipContent(status, existingPendingRequest)
    : null;

  const button = (
    <Button
      onClick={onShare}
      disabled={isDisabled}
      className={className}
      aria-label="Share with Community"
    >
      <Share2 className="mr-2 h-4 w-4" />
      Share with Community
    </Button>
  );

  // Wrap in tooltip only if disabled
  if (isDisabled && tooltipContent) {
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="inline-block">{button}</span>
          </TooltipTrigger>
          <TooltipContent className="max-w-xs">
            <p>{tooltipContent}</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }

  return button;
}
