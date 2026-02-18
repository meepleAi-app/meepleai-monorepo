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

// v2: More vibrant colors aligned with entity colors (Issue #4604)
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
    // v2: Brighter blue using --e-chat base color
    bgClass: 'bg-blue-50 dark:bg-blue-900/20',
    textClass: 'text-blue-700 dark:text-blue-200',
    dotClass: 'bg-blue-500',
    icon: MessageCircle,
    description: 'Chat session is currently active',
  },
  waiting: {
    label: 'Waiting',
    // v2: Brighter yellow
    bgClass: 'bg-yellow-50 dark:bg-yellow-900/20',
    textClass: 'text-yellow-700 dark:text-yellow-200',
    dotClass: 'bg-yellow-500',
    icon: Loader2,
    description: 'Waiting for a response',
  },
  archived: {
    label: 'Archived',
    bgClass: 'bg-gray-50 dark:bg-gray-900/20',
    textClass: 'text-gray-600 dark:text-gray-300',
    dotClass: 'bg-gray-400',
    icon: Archive,
    description: 'Chat session has been archived',
  },
  closed: {
    label: 'Closed',
    bgClass: 'bg-slate-50 dark:bg-slate-900/20',
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
            {/* Pulsating status dot — v2 live-pulse animation (Issue #4604) */}
            <span className="relative flex items-center justify-center">
              <span
                className={cn(
                  'w-2 h-2 rounded-full',
                  config.dotClass,
                  // v2: Use mc-live-pulse for active status (smoother than animate-pulse)
                  status === 'active' && 'animate-mc-live-pulse',
                  status === 'waiting' && 'animate-pulse'
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
