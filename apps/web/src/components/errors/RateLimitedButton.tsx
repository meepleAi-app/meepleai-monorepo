/**
 * Rate Limited Button Component
 *
 * Wrapper around Button component that automatically disables during rate limit periods
 * and displays user-friendly countdown messages.
 *
 * @example
 * ```tsx
 * const { isRateLimited, remainingSeconds, message, handleError } = useRateLimitHandler();
 *
 * <RateLimitedButton
 *   isRateLimited={isRateLimited}
 *   remainingSeconds={remainingSeconds}
 *   message={message}
 *   onClick={handleClick}
 * >
 *   Submit
 * </RateLimitedButton>
 * ```
 */

import React from 'react';

import { Button, ButtonProps } from '@/components/ui/button';
import { cn } from '@/lib/utils';

export interface RateLimitedButtonProps extends ButtonProps {
  /** Whether the button is currently rate limited */
  isRateLimited?: boolean;
  /** Seconds remaining in countdown (optional, for tooltip or label) */
  remainingSeconds?: number;
  /** User-friendly rate limit message */
  message?: string | null;
  /** Whether to show countdown in the button text */
  showCountdownInButton?: boolean;
  /** Original button text (used when showing countdown) */
  originalText?: string;
}

/**
 * Button component that respects rate limiting state
 */
export const RateLimitedButton = React.forwardRef<HTMLButtonElement, RateLimitedButtonProps>(
  (
    {
      isRateLimited = false,
      remainingSeconds = 0,
      message,
      showCountdownInButton = false,
      originalText,
      children,
      disabled,
      className,
      ...props
    },
    ref
  ) => {
    // Button is disabled if rate limited OR explicitly disabled
    const isDisabled = disabled || isRateLimited;

    // Determine button content
    let buttonContent = children;
    if (showCountdownInButton && isRateLimited && remainingSeconds > 0) {
      buttonContent = originalText
        ? `${originalText} (${remainingSeconds}s)`
        : `Wait ${remainingSeconds}s`;
    }

    // Determine tooltip/title
    const title = isRateLimited && message ? message : undefined;

    return (
      <Button
        ref={ref}
        disabled={isDisabled}
        className={cn(isRateLimited && 'cursor-not-allowed', className)}
        title={title}
        aria-disabled={isDisabled}
        aria-label={isRateLimited && message ? message : undefined}
        {...props}
      >
        {buttonContent}
      </Button>
    );
  }
);

RateLimitedButton.displayName = 'RateLimitedButton';
