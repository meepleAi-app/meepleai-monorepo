/**
 * ConnectionStatus Component (Issue #3324)
 *
 * Visual indicator for SSE connection status in the dashboard.
 * Shows real-time connection state with minimal footprint.
 *
 * @example
 * ```tsx
 * <ConnectionStatus
 *   state="connected"
 *   showLabel={true}
 * />
 * ```
 */

'use client';

import * as React from 'react';

import { cva, type VariantProps } from 'class-variance-authority';
import { Wifi, WifiOff, Loader2 } from 'lucide-react';

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/overlays/tooltip';
import type { ConnectionState } from '@/lib/hooks/useDashboardStream';
import { cn } from '@/lib/utils';

// ============================================================================
// Variants
// ============================================================================

const statusVariants = cva('flex items-center gap-1.5 text-xs font-medium transition-colors duration-200', {
  variants: {
    state: {
      connected: 'text-green-600 dark:text-green-400',
      connecting: 'text-yellow-600 dark:text-yellow-400',
      reconnecting: 'text-yellow-600 dark:text-yellow-400',
      disconnected: 'text-muted-foreground',
      error: 'text-destructive',
    },
    size: {
      sm: 'text-xs',
      md: 'text-sm',
      lg: 'text-base',
    },
  },
  defaultVariants: {
    state: 'disconnected',
    size: 'sm',
  },
});

const dotVariants = cva('rounded-full', {
  variants: {
    state: {
      connected: 'bg-green-500 dark:bg-green-400',
      connecting: 'bg-yellow-500 dark:bg-yellow-400 animate-pulse',
      reconnecting: 'bg-yellow-500 dark:bg-yellow-400 animate-pulse',
      disconnected: 'bg-muted-foreground/50',
      error: 'bg-destructive',
    },
    size: {
      sm: 'h-1.5 w-1.5',
      md: 'h-2 w-2',
      lg: 'h-2.5 w-2.5',
    },
  },
  defaultVariants: {
    state: 'disconnected',
    size: 'sm',
  },
});

// ============================================================================
// Types
// ============================================================================

export interface ConnectionStatusProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof statusVariants> {
  /** Current connection state */
  state: ConnectionState;

  /** Whether to show the text label */
  showLabel?: boolean;

  /** Whether to show the connection icon */
  showIcon?: boolean;

  /** Custom label text (overrides default) */
  label?: string;

  /** Last heartbeat timestamp */
  lastHeartbeat?: Date | null;

  /** Reconnection attempt count */
  reconnectAttempts?: number;
}

// ============================================================================
// Helpers
// ============================================================================

const getStateLabel = (state: ConnectionState): string => {
  switch (state) {
    case 'connected':
      return 'Live';
    case 'connecting':
      return 'Connecting...';
    case 'reconnecting':
      return 'Reconnecting...';
    case 'disconnected':
      return 'Offline';
    case 'error':
      return 'Connection Error';
    default:
      return 'Unknown';
  }
};

const getTooltipMessage = (state: ConnectionState, reconnectAttempts: number, lastHeartbeat?: Date | null): string => {
  switch (state) {
    case 'connected':
      return lastHeartbeat
        ? `Real-time updates active. Last heartbeat: ${lastHeartbeat.toLocaleTimeString()}`
        : 'Real-time updates active';
    case 'connecting':
      return 'Establishing real-time connection...';
    case 'reconnecting':
      return `Reconnection attempt ${reconnectAttempts}. Using polling as fallback.`;
    case 'disconnected':
      return 'Real-time updates disabled. Refresh to reconnect.';
    case 'error':
      return 'Connection failed. Using polling for updates.';
    default:
      return '';
  }
};

// ============================================================================
// Component
// ============================================================================

const ConnectionStatus = React.forwardRef<HTMLDivElement, ConnectionStatusProps>(
  (
    {
      className,
      state,
      size = 'sm',
      showLabel = true,
      showIcon = false,
      label,
      lastHeartbeat,
      reconnectAttempts = 0,
      ...props
    },
    ref
  ) => {
    const displayLabel = label || getStateLabel(state);
    const tooltipMessage = getTooltipMessage(state, reconnectAttempts, lastHeartbeat);

    const Icon = React.useMemo(() => {
      if (!showIcon) return null;

      switch (state) {
        case 'connected':
          return Wifi;
        case 'connecting':
        case 'reconnecting':
          return Loader2;
        case 'disconnected':
        case 'error':
          return WifiOff;
        default:
          return null;
      }
    }, [state, showIcon]);

    const iconClassName = cn(
      size === 'sm' ? 'h-3 w-3' : size === 'md' ? 'h-4 w-4' : 'h-5 w-5',
      (state === 'connecting' || state === 'reconnecting') && 'animate-spin'
    );

    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <div
              ref={ref}
              role="status"
              aria-label={`Connection status: ${displayLabel}`}
              className={cn(statusVariants({ state, size }), className)}
              {...props}
            >
              {/* Status dot */}
              <span className={dotVariants({ state, size })} aria-hidden="true" />

              {/* Icon (optional) */}
              {Icon && <Icon className={iconClassName} aria-hidden="true" />}

              {/* Label (optional) */}
              {showLabel && <span>{displayLabel}</span>}
            </div>
          </TooltipTrigger>
          <TooltipContent side="bottom" className="max-w-[250px] text-center">
            <p>{tooltipMessage}</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }
);

ConnectionStatus.displayName = 'ConnectionStatus';

export { ConnectionStatus, statusVariants, dotVariants };
