/**
 * ChatStatusBadge - Chat Session Status Indicator
 * Issue #4400 - ChatSession-Specific Metadata & Status Display
 *
 * Displays chat status with color-coded badge and pulsating dot animation.
 * Blue-tinted colors aligned with chatSession entity color (220 80% 55%).
 */

'use client';

import React from 'react';

import { Archive, Loader2, MessageCircle, XCircle } from 'lucide-react';

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type ChatStatus = 'active' | 'waiting' | 'archived' | 'closed';

export interface ChatStatusBadgeProps {
  /** Current chat status */
  status: ChatStatus;
  /** Show status label text */
  showLabel?: boolean;
  /** Size variant */
  size?: 'sm' | 'md';
  /** Custom className */
  className?: string;
}

// ============================================================================
// Configuration
// ============================================================================

const statusConfig: Record<
  ChatStatus,
  {
    label: string;
    bgClass: string;
    textClass: string;
    dotClass: string;
    icon: typeof MessageCircle;
    description: string;
  }
> = {
  active: {
    label: 'Active',
    bgClass: 'bg-blue-100 dark:bg-blue-900/30',
    textClass: 'text-blue-900 dark:text-blue-100',
    dotClass: 'bg-blue-500',
    icon: MessageCircle,
    description: 'Chat session is currently active',
  },
  waiting: {
    label: 'Waiting',
    bgClass: 'bg-yellow-100 dark:bg-yellow-900/30',
    textClass: 'text-yellow-900 dark:text-yellow-100',
    dotClass: 'bg-yellow-500',
    icon: Loader2,
    description: 'Waiting for a response',
  },
  archived: {
    label: 'Archived',
    bgClass: 'bg-gray-100 dark:bg-gray-900/30',
    textClass: 'text-gray-900 dark:text-gray-100',
    dotClass: 'bg-gray-400',
    icon: Archive,
    description: 'Chat session has been archived',
  },
  closed: {
    label: 'Closed',
    bgClass: 'bg-slate-100 dark:bg-slate-900/30',
    textClass: 'text-slate-500 dark:text-slate-400',
    dotClass: 'bg-slate-400',
    icon: XCircle,
    description: 'Chat session is closed',
  },
};

// ============================================================================
// Component
// ============================================================================

export const ChatStatusBadge = React.memo(function ChatStatusBadge({
  status,
  showLabel = true,
  size = 'sm',
  className,
}: ChatStatusBadgeProps) {
  const config = statusConfig[status];
  const isPulsating = status === 'active' || status === 'waiting';

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <span
            className={cn(
              'inline-flex items-center gap-1.5 px-2 py-1 rounded-md font-semibold transition-colors',
              size === 'sm' ? 'text-[10px]' : 'text-xs',
              config.bgClass,
              config.textClass,
              className
            )}
            data-testid={`chat-status-${status}`}
            aria-label={`Chat status: ${config.label}`}
          >
            {/* Pulsating status dot */}
            <span className="relative flex items-center justify-center">
              <span
                className={cn(
                  'w-2 h-2 rounded-full',
                  config.dotClass,
                  isPulsating && 'animate-pulse'
                )}
                aria-hidden="true"
              />
              {isPulsating && (
                <span
                  className={cn(
                    'absolute w-2 h-2 rounded-full opacity-75 animate-ping',
                    config.dotClass
                  )}
                  aria-hidden="true"
                />
              )}
            </span>

            {/* Status label */}
            {showLabel && <span>{config.label}</span>}
          </span>
        </TooltipTrigger>
        <TooltipContent>
          <p className="text-xs">{config.description}</p>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
});

ChatStatusBadge.displayName = 'ChatStatusBadge';
