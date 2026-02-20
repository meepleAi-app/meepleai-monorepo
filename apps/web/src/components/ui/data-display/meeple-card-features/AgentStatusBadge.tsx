/**
 * AgentStatusBadge - Agent Activity Status Indicator
 * Issue #4184 - Agent-Specific Metadata & Status Display
 *
 * Displays agent operational status with color-coded badge and pulsating dot animation.
 */

'use client';

import React from 'react';

import { Activity, AlertCircle, Circle, Loader2 } from 'lucide-react';

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type AgentStatus = 'active' | 'idle' | 'training' | 'error';

export interface AgentStatusBadgeProps {
  /** Current agent status */
  status: AgentStatus;
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
  AgentStatus,
  {
    label: string;
    color: string;
    bgClass: string;
    textClass: string;
    dotClass: string;
    icon: typeof Circle;
    description: string;
  }
> = {
  active: {
    label: 'Active',
    color: 'green',
    bgClass: 'bg-green-100 dark:bg-green-900/30',
    textClass: 'text-green-900 dark:text-green-100',
    dotClass: 'bg-green-500',
    icon: Activity,
    description: 'Agent is currently processing requests',
  },
  idle: {
    label: 'Idle',
    color: 'gray',
    bgClass: 'bg-gray-100 dark:bg-gray-900/30',
    textClass: 'text-gray-900 dark:text-gray-100',
    dotClass: 'bg-gray-400',
    icon: Circle,
    description: 'Agent is idle and ready for requests',
  },
  training: {
    label: 'Training',
    color: 'yellow',
    bgClass: 'bg-yellow-100 dark:bg-yellow-900/30',
    textClass: 'text-yellow-900 dark:text-yellow-100',
    dotClass: 'bg-yellow-500',
    icon: Loader2,
    description: 'Agent is currently training or updating',
  },
  error: {
    label: 'Error',
    color: 'red',
    bgClass: 'bg-red-100 dark:bg-red-900/30',
    textClass: 'text-red-900 dark:text-red-100',
    dotClass: 'bg-red-500',
    icon: AlertCircle,
    description: 'Agent encountered an error',
  },
};

// ============================================================================
// Component
// ============================================================================

export const AgentStatusBadge = React.memo(function AgentStatusBadge({
  status,
  showLabel = true,
  size = 'sm',
  className,
}: AgentStatusBadgeProps) {
  const config = statusConfig[status];
  const _Icon = config.icon;
  const isPulsating = status === 'active' || status === 'training';

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
            data-testid={`agent-status-${status}`}
            aria-label={`Agent status: ${config.label}`}
          >
            {/* Pulsating status dot — v2 live-pulse animation (Issue #4604) */}
            <span className="relative flex items-center justify-center">
              <span
                className={cn(
                  'w-2 h-2 rounded-full',
                  config.dotClass,
                  // v2: Use mc-live-pulse for active status (smoother than animate-pulse)
                  status === 'active' && 'animate-mc-live-pulse',
                  status === 'training' && 'animate-pulse'
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

AgentStatusBadge.displayName = 'AgentStatusBadge';
