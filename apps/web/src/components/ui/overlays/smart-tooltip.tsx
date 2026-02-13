/**
 * Smart Tooltip Component
 * Epic #4068 - Issue #4186
 *
 * Tooltip with automatic positioning and accessibility features
 */

'use client';

import React, { type ReactNode } from 'react';

import { useFocusTrap } from '@/hooks/useFocusTrap';
import { useSmartTooltip } from '@/hooks/useSmartTooltip';

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from './tooltip';

export interface SmartTooltipProps {
  /** Trigger element */
  children: ReactNode;
  /** Tooltip content */
  content: ReactNode;
  /** Positioning mode: 'auto' (smart) or fixed side */
  side?: 'auto' | 'top' | 'bottom' | 'left' | 'right';
  /** Delay before showing (ms) */
  delayDuration?: number;
  /** Enable focus trap for interactive tooltips */
  trapFocus?: boolean;
  /** Additional props for TooltipContent */
  contentProps?: React.ComponentProps<typeof TooltipContent>;
}

/**
 * SmartTooltip with automatic positioning and accessibility
 *
 * Features:
 * - Auto-positioning with viewport detection (when side="auto")
 * - Keyboard navigation (Tab, Enter, Escape)
 * - Screen reader support (ARIA attributes)
 * - Focus trap for interactive content
 * - Performance optimized (<16ms positioning)
 *
 * @example
 * <SmartTooltip content="This feature requires Pro tier" side="auto">
 *   <Button>Premium Feature</Button>
 * </SmartTooltip>
 */
export function SmartTooltip({
  children,
  content,
  side = 'auto',
  delayDuration = 300,
  trapFocus = false,
  contentProps
}: SmartTooltipProps) {
  const {
    position,
    isVisible,
    setIsVisible,
    triggerRef,
    tooltipRef
  } = useSmartTooltip({
    enabled: side === 'auto'
  });

  // Enable focus trap for interactive tooltips
  useFocusTrap(tooltipRef, trapFocus && isVisible);

  // If fixed side specified, use default Tooltip
  if (side !== 'auto') {
    return (
      <TooltipProvider delayDuration={delayDuration}>
        <Tooltip>
          <TooltipTrigger asChild>{children}</TooltipTrigger>
          <TooltipContent side={side} {...contentProps}>
            {content}
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }

  // Smart positioning mode
  return (
    <TooltipProvider delayDuration={delayDuration}>
      <Tooltip open={isVisible} onOpenChange={setIsVisible}>
        <TooltipTrigger ref={triggerRef as React.Ref<HTMLButtonElement>} asChild>
          {children}
        </TooltipTrigger>
        <TooltipContent
          ref={tooltipRef as React.Ref<HTMLDivElement>}
          style={position ? {
            position: 'fixed',
            top: position.top,
            bottom: position.bottom,
            left: position.left,
            right: position.right
          } : undefined}
          data-placement={position?.placement}
          sideOffset={0}
          {...contentProps}
        >
          {content}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
