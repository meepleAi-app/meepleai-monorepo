/**
 * ChatStatsDisplay - Chat Session Statistics
 * Issue #4400 - ChatSession-Specific Metadata & Status Display
 *
 * Displays message count, last message time, and session duration.
 * Reuses formatInvocationCount from AgentStatsDisplay for message count formatting.
 */

'use client';

import React from 'react';

import { Clock, MessageSquare, Timer } from 'lucide-react';

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

import { formatInvocationCount, formatRelativeTime } from './AgentStatsDisplay';

// ============================================================================
// Types
// ============================================================================

export interface ChatStats {
  /** Total message count */
  messageCount: number;
  /** Last message timestamp */
  lastMessageAt?: Date | string;
  /** Session duration in minutes */
  durationMinutes?: number;
}

export interface ChatStatsDisplayProps {
  /** Chat statistics */
  stats: ChatStats;
  /** Layout orientation */
  layout?: 'horizontal' | 'vertical';
  /** Custom className */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Format duration in minutes to human-readable string
 */
export function formatDuration(minutes: number): string {
  if (minutes < 60) {
    return `${minutes}m`;
  }
  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;
  if (remainingMinutes === 0) {
    return `${hours}h`;
  }
  return `${hours}h ${remainingMinutes}m`;
}

// ============================================================================
// Component
// ============================================================================

export const ChatStatsDisplay = React.memo(function ChatStatsDisplay({
  stats,
  layout = 'horizontal',
  className,
}: ChatStatsDisplayProps) {
  const { messageCount, lastMessageAt, durationMinutes } = stats;

  return (
    <div
      className={cn(
        'flex gap-3 text-xs text-muted-foreground',
        layout === 'vertical' ? 'flex-col' : 'flex-row items-center',
        className
      )}
      data-testid="chat-stats-display"
    >
      {/* Message Count */}
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="flex items-center gap-1" data-testid="message-count">
              <MessageSquare className="w-3.5 h-3.5" aria-hidden="true" />
              <span className="font-semibold">{formatInvocationCount(messageCount)}</span>
              <span className="text-[10px]">msgs</span>
            </span>
          </TooltipTrigger>
          <TooltipContent>
            <p className="text-xs">{messageCount.toLocaleString()} total messages</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>

      {/* Last Message */}
      {lastMessageAt && (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="flex items-center gap-1" data-testid="last-message-at">
                <Clock className="w-3.5 h-3.5" aria-hidden="true" />
                <span>{formatRelativeTime(lastMessageAt)}</span>
              </span>
            </TooltipTrigger>
            <TooltipContent>
              <p className="text-xs">
                Last message: {typeof lastMessageAt === 'string' ? new Date(lastMessageAt).toLocaleString() : lastMessageAt.toLocaleString()}
              </p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      )}

      {/* Duration */}
      {durationMinutes !== undefined && (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="flex items-center gap-1" data-testid="chat-duration">
                <Timer className="w-3.5 h-3.5" aria-hidden="true" />
                <span>{formatDuration(durationMinutes)}</span>
              </span>
            </TooltipTrigger>
            <TooltipContent>
              <p className="text-xs">Session duration: {formatDuration(durationMinutes)}</p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      )}
    </div>
  );
});

ChatStatsDisplay.displayName = 'ChatStatsDisplay';
