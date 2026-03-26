/**
 * SessionStatusBadge - Live Session Status Indicator
 * Issue #4751 - MeepleCard Session Front
 *
 * Displays session status with color-coded badge and pulsating dot.
 * Indigo-tinted colors aligned with session entity color (240 60% 55%).
 *
 * Statuses:
 * - Setup (blue) - Session being configured
 * - InProgress (green + pulse) - Actively playing
 * - Paused (amber) - Temporarily paused
 * - Completed (purple) - Game finished
 */

'use client';

import React from 'react';

import { CheckCircle2, Pause, Play, Settings } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

import type { SessionStatus } from './session-types';

// ============================================================================
// Types
// ============================================================================

export interface SessionStatusBadgeProps {
  status: SessionStatus;
  showLabel?: boolean;
  size?: 'sm' | 'md';
  className?: string;
}

// ============================================================================
// Configuration
// ============================================================================

const statusConfig: Record<
  SessionStatus,
  {
    label: string;
    bgClass: string;
    textClass: string;
    dotClass: string;
    icon: typeof Play;
    description: string;
  }
> = {
  setup: {
    label: 'Setup',
    bgClass: 'bg-blue-50 dark:bg-blue-900/20',
    textClass: 'text-blue-700 dark:text-blue-200',
    dotClass: 'bg-blue-500',
    icon: Settings,
    description: 'Sessione in configurazione',
  },
  inProgress: {
    label: 'In Corso',
    bgClass: 'bg-emerald-50 dark:bg-emerald-900/20',
    textClass: 'text-emerald-700 dark:text-emerald-200',
    dotClass: 'bg-emerald-500',
    icon: Play,
    description: 'Partita in corso',
  },
  paused: {
    label: 'In Pausa',
    bgClass: 'bg-amber-50 dark:bg-amber-900/20',
    textClass: 'text-amber-700 dark:text-amber-200',
    dotClass: 'bg-amber-500',
    icon: Pause,
    description: 'Sessione in pausa',
  },
  completed: {
    label: 'Completata',
    bgClass: 'bg-purple-50 dark:bg-purple-900/20',
    textClass: 'text-purple-700 dark:text-purple-200',
    dotClass: 'bg-purple-500',
    icon: CheckCircle2,
    description: 'Partita completata',
  },
};

// ============================================================================
// Component
// ============================================================================

export const SessionStatusBadge = React.memo(function SessionStatusBadge({
  status,
  showLabel = true,
  size = 'sm',
  className,
}: SessionStatusBadgeProps) {
  const config = statusConfig[status];
  const isPulsating = status === 'inProgress';

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
            data-testid={`session-status-${status}`}
            aria-label={`Session status: ${config.label}`}
          >
            {/* Pulsating status dot */}
            <span className="relative flex items-center justify-center">
              <span
                className={cn(
                  'w-2 h-2 rounded-full',
                  config.dotClass,
                  isPulsating && 'animate-mc-live-pulse'
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
