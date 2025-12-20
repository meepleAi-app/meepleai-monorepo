/**
 * OfflineBanner Component (Issue #2054)
 *
 * Displays a dismissible banner when the user is offline.
 * Shows connection quality warnings and reconnection status.
 *
 * Features:
 * - Automatic show/hide based on network status
 * - Reconnection progress indicator
 * - Queued actions counter
 * - Dismissible with smooth animation
 *
 * @example
 * ```tsx
 * // In your layout or app wrapper:
 * <OfflineBanner />
 * ```
 */

'use client';

import * as React from 'react';
import { WifiOff, RefreshCw, AlertTriangle, X, Loader2 } from 'lucide-react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';
import { useNetworkStatus } from '@/hooks/useNetworkStatus';
import { Button } from '@/components/ui/button';

// ============================================================================
// Variants
// ============================================================================

const offlineBannerVariants = cva(
  'fixed top-0 left-0 right-0 z-50 flex items-center justify-between px-4 py-2 text-sm transition-all duration-300 ease-in-out',
  {
    variants: {
      variant: {
        offline: 'bg-destructive text-destructive-foreground',
        reconnecting: 'bg-yellow-500 text-yellow-950 dark:bg-yellow-600 dark:text-yellow-50',
        poor: 'bg-orange-500 text-orange-950 dark:bg-orange-600 dark:text-orange-50',
      },
      visibility: {
        visible: 'translate-y-0 opacity-100',
        hidden: '-translate-y-full opacity-0 pointer-events-none',
      },
    },
    defaultVariants: {
      variant: 'offline',
      visibility: 'visible',
    },
  }
);

// ============================================================================
// Types
// ============================================================================

export interface OfflineBannerProps
  extends React.HTMLAttributes<HTMLDivElement>, VariantProps<typeof offlineBannerVariants> {
  /** Number of queued actions waiting to be sent */
  queuedActionsCount?: number;

  /** Callback when retry button is clicked */
  onRetry?: () => void;

  /** Whether the banner can be dismissed */
  dismissible?: boolean;

  /** Custom message override */
  message?: string;
}

// ============================================================================
// Component
// ============================================================================

const OfflineBanner = React.forwardRef<HTMLDivElement, OfflineBannerProps>(
  ({ className, queuedActionsCount = 0, onRetry, dismissible = false, message, ...props }, ref) => {
    const [isDismissed, setIsDismissed] = React.useState(false);
    const {
      isOnline: _isOnline,
      isOffline,
      connectionQuality,
      isReconnecting,
      reconnectAttempts,
    } = useNetworkStatus();

    // Reset dismissed state when going offline again
    React.useEffect(() => {
      if (isOffline) {
        setIsDismissed(false);
      }
    }, [isOffline]);

    // Determine visibility and variant
    const shouldShow =
      !isDismissed && (isOffline || isReconnecting || connectionQuality === 'poor');
    const variant = isOffline
      ? 'offline'
      : isReconnecting
        ? 'reconnecting'
        : connectionQuality === 'poor'
          ? 'poor'
          : 'offline';

    // Build message
    const displayMessage = React.useMemo(() => {
      if (message) return message;

      if (isOffline) {
        const queueText =
          queuedActionsCount > 0
            ? ` (${queuedActionsCount} azione${queuedActionsCount > 1 ? 'i' : ''} in coda)`
            : '';
        return `Sei offline. I messaggi verranno inviati quando tornerai online${queueText}.`;
      }

      if (isReconnecting) {
        return `Riconnessione in corso... (tentativo ${reconnectAttempts})`;
      }

      if (connectionQuality === 'poor') {
        return 'Connessione lenta. Alcune funzionalit potrebbero essere limitate.';
      }

      return '';
    }, [
      message,
      isOffline,
      isReconnecting,
      connectionQuality,
      queuedActionsCount,
      reconnectAttempts,
    ]);

    // Icon based on state
    const Icon = React.useMemo(() => {
      if (isOffline) return WifiOff;
      if (isReconnecting) return Loader2;
      if (connectionQuality === 'poor') return AlertTriangle;
      return WifiOff;
    }, [isOffline, isReconnecting, connectionQuality]);

    const handleDismiss = React.useCallback(() => {
      setIsDismissed(true);
    }, []);

    const handleRetry = React.useCallback(() => {
      onRetry?.();
    }, [onRetry]);

    return (
      <div
        ref={ref}
        role="alert"
        aria-live="polite"
        className={cn(
          offlineBannerVariants({
            variant,
            visibility: shouldShow ? 'visible' : 'hidden',
          }),
          className
        )}
        {...props}
      >
        <div className="flex items-center gap-2">
          <Icon
            className={cn('h-4 w-4 flex-shrink-0', isReconnecting && 'animate-spin')}
            aria-hidden="true"
          />
          <span>{displayMessage}</span>
        </div>

        <div className="flex items-center gap-2">
          {isOffline && onRetry && (
            <Button
              variant="ghost"
              size="sm"
              onClick={handleRetry}
              className="h-7 px-2 text-current hover:bg-white/20"
            >
              <RefreshCw className="mr-1 h-3 w-3" />
              Riprova
            </Button>
          )}

          {dismissible && !isOffline && (
            <Button
              variant="ghost"
              size="sm"
              onClick={handleDismiss}
              className="h-7 w-7 p-0 text-current hover:bg-white/20"
              aria-label="Chiudi banner"
            >
              <X className="h-4 w-4" />
            </Button>
          )}
        </div>
      </div>
    );
  }
);

OfflineBanner.displayName = 'OfflineBanner';

export { OfflineBanner, offlineBannerVariants };
