'use client';

/**
 * Offline Indicator Component (Issue #3346)
 *
 * Shows a banner when the user is offline with sync status.
 */

import { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { WifiOff, RefreshCw, Cloud, CloudOff, Check, AlertTriangle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';
import { usePWA } from '@/lib/hooks/usePWA';

// ============================================================================
// Types
// ============================================================================

export interface OfflineIndicatorProps {
  /** Position of the banner */
  position?: 'top' | 'bottom';

  /** Whether to show sync status */
  showSyncStatus?: boolean;

  /** Custom class name */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function OfflineIndicator({
  position = 'bottom',
  showSyncStatus = true,
  className,
}: OfflineIndicatorProps) {
  const { isOnline, syncState, actions } = usePWA();
  const [showBanner, setShowBanner] = useState(false);
  const [syncing, setSyncing] = useState(false);

  // Show banner when offline or syncing
  useEffect(() => {
    if (!isOnline) {
      setShowBanner(true);
    } else if (syncState.pendingCount > 0 && showSyncStatus) {
      setShowBanner(true);
    } else {
      // Delay hiding to show success message
      const timeout = setTimeout(() => setShowBanner(false), 2000);
      return () => clearTimeout(timeout);
    }
  }, [isOnline, syncState.pendingCount, showSyncStatus]);

  const handleSync = async () => {
    setSyncing(true);
    await actions.sync();
    setSyncing(false);
  };

  const getBannerContent = () => {
    if (!isOnline) {
      return {
        icon: <WifiOff className="h-4 w-4" />,
        message: 'You are offline',
        subMessage: syncState.pendingCount > 0
          ? `${syncState.pendingCount} action${syncState.pendingCount > 1 ? 's' : ''} pending`
          : 'Changes will sync when connected',
        variant: 'offline' as const,
      };
    }

    if (syncing || syncState.status === 'syncing') {
      return {
        icon: <RefreshCw className="h-4 w-4 animate-spin" />,
        message: 'Syncing...',
        subMessage: `${syncState.pendingCount} action${syncState.pendingCount > 1 ? 's' : ''} remaining`,
        variant: 'syncing' as const,
      };
    }

    if (syncState.status === 'failed') {
      return {
        icon: <AlertTriangle className="h-4 w-4" />,
        message: 'Sync failed',
        subMessage: 'Some actions could not be synced',
        variant: 'error' as const,
      };
    }

    if (syncState.pendingCount > 0) {
      return {
        icon: <CloudOff className="h-4 w-4" />,
        message: 'Pending sync',
        subMessage: `${syncState.pendingCount} action${syncState.pendingCount > 1 ? 's' : ''} waiting`,
        variant: 'pending' as const,
      };
    }

    if (syncState.status === 'completed') {
      return {
        icon: <Check className="h-4 w-4" />,
        message: 'Synced',
        subMessage: 'All changes saved',
        variant: 'success' as const,
      };
    }

    return null;
  };

  const content = getBannerContent();

  if (!content) return null;

  const variantStyles = {
    offline: 'bg-yellow-500/90 text-yellow-50',
    syncing: 'bg-blue-500/90 text-blue-50',
    error: 'bg-red-500/90 text-red-50',
    pending: 'bg-orange-500/90 text-orange-50',
    success: 'bg-green-500/90 text-green-50',
  };

  return (
    <AnimatePresence>
      {showBanner && (
        <motion.div
          initial={{ opacity: 0, y: position === 'top' ? -20 : 20 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: position === 'top' ? -20 : 20 }}
          transition={{ duration: 0.2 }}
          className={cn(
            'fixed left-0 right-0 z-50 px-4',
            position === 'top' ? 'top-0' : 'bottom-0',
            className
          )}
        >
          <div
            className={cn(
              'mx-auto max-w-md rounded-lg px-4 py-3 shadow-lg backdrop-blur-sm',
              position === 'bottom' ? 'mb-4' : 'mt-4',
              variantStyles[content.variant]
            )}
          >
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-3">
                {content.icon}
                <div className="flex flex-col">
                  <span className="text-sm font-medium">{content.message}</span>
                  <span className="text-xs opacity-90">{content.subMessage}</span>
                </div>
              </div>

              {/* Sync button */}
              {isOnline && syncState.pendingCount > 0 && !syncing && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={handleSync}
                  className="h-8 px-2 text-current hover:bg-white/20"
                >
                  <RefreshCw className="h-4 w-4 mr-1" />
                  Sync
                </Button>
              )}
            </div>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

export default OfflineIndicator;
