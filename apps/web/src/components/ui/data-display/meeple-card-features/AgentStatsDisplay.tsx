/**
 * AgentStatsDisplay - Agent Invocation Statistics
 * Issue #4184 - Agent-Specific Metadata & Status Display
 *
 * Displays formatted invocation count, last execution relative time, and average response time.
 */

'use client';

import React from 'react';

import { Clock, TrendingUp, Zap } from 'lucide-react';

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface AgentStats {
  /** Total invocation count */
  invocationCount: number;
  /** Last execution timestamp */
  lastExecutedAt?: Date | string;
  /** Average response time in milliseconds */
  avgResponseTimeMs?: number;
}

export interface AgentStatsDisplayProps {
  /** Agent statistics */
  stats: AgentStats;
  /** Layout orientation */
  layout?: 'horizontal' | 'vertical';
  /** Custom className */
  className?: string;
}

// ============================================================================
// Utility Functions
// ============================================================================

/**
 * Format large numbers: 342 → 1.2K → 3.4M
 */
export function formatInvocationCount(count: number): string {
  if (count >= 1_000_000) {
    return `${(count / 1_000_000).toFixed(1)}M`;
  }
  if (count >= 1_000) {
    return `${(count / 1_000).toFixed(1)}K`;
  }
  return count.toString();
}

/**
 * Format relative time: "2 hours ago"
 */
export function formatRelativeTime(timestamp: Date | string): string {
  const date = typeof timestamp === 'string' ? new Date(timestamp) : timestamp;
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffSec = Math.floor(diffMs / 1000);
  const diffMin = Math.floor(diffSec / 60);
  const diffHour = Math.floor(diffMin / 60);
  const diffDay = Math.floor(diffHour / 24);

  if (diffSec < 60) return 'just now';
  if (diffMin < 60) return `${diffMin} minute${diffMin !== 1 ? 's' : ''} ago`;
  if (diffHour < 24) return `${diffHour} hour${diffHour !== 1 ? 's' : ''} ago`;
  if (diffDay < 30) return `${diffDay} day${diffDay !== 1 ? 's' : ''} ago`;
  return date.toLocaleDateString();
}

/**
 * Format response time: 1234ms → "1.2s"
 */
export function formatResponseTime(ms: number): string {
  if (ms >= 1000) {
    return `${(ms / 1000).toFixed(1)}s`;
  }
  return `${ms}ms`;
}

// ============================================================================
// Component
// ============================================================================

export const AgentStatsDisplay = React.memo(function AgentStatsDisplay({
  stats,
  layout = 'horizontal',
  className,
}: AgentStatsDisplayProps) {
  const { invocationCount, lastExecutedAt, avgResponseTimeMs } = stats;

  return (
    <div
      className={cn(
        'flex gap-3 text-xs text-muted-foreground',
        layout === 'vertical' ? 'flex-col' : 'flex-row items-center',
        className
      )}
      data-testid="agent-stats-display"
    >
      {/* Invocation Count */}
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <span className="flex items-center gap-1" data-testid="invocation-count">
              <TrendingUp className="w-3.5 h-3.5" aria-hidden="true" />
              <span className="font-semibold">{formatInvocationCount(invocationCount)}</span>
              <span className="text-[10px]">runs</span>
            </span>
          </TooltipTrigger>
          <TooltipContent>
            <p className="text-xs">{invocationCount.toLocaleString()} total invocations</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>

      {/* Last Execution */}
      {lastExecutedAt && (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="flex items-center gap-1" data-testid="last-execution">
                <Clock className="w-3.5 h-3.5" aria-hidden="true" />
                <span>{formatRelativeTime(lastExecutedAt)}</span>
              </span>
            </TooltipTrigger>
            <TooltipContent>
              <p className="text-xs">
                Last run: {typeof lastExecutedAt === 'string' ? new Date(lastExecutedAt).toLocaleString() : lastExecutedAt.toLocaleString()}
              </p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      )}

      {/* Average Response Time */}
      {avgResponseTimeMs !== undefined && (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="flex items-center gap-1" data-testid="avg-response-time">
                <Zap className="w-3.5 h-3.5" aria-hidden="true" />
                <span>{formatResponseTime(avgResponseTimeMs)}</span>
              </span>
            </TooltipTrigger>
            <TooltipContent>
              <p className="text-xs">Average response time</p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      )}
    </div>
  );
});

AgentStatsDisplay.displayName = 'AgentStatsDisplay';
