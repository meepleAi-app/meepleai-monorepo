'use client';

/**
 * Offline Page (Issue #3346)
 *
 * Displayed when the user is offline and the requested page isn't cached.
 */

import { useEffect, useState } from 'react';

import { WifiOff, RefreshCw, Home, Gamepad2 } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { usePWA } from '@/lib/domain-hooks/usePWA';

export default function OfflinePage() {
  const { isOnline, storageStats, actions: _actions } = usePWA();
  const [retrying, setRetrying] = useState(false);

  // Auto-redirect when back online
  useEffect(() => {
    if (isOnline) {
      // Try to go back to the previous page
      window.history.back();
    }
  }, [isOnline]);

  const handleRetry = async () => {
    setRetrying(true);
    // Wait a moment then reload
    await new Promise(resolve => setTimeout(resolve, 1000));
    window.location.reload();
  };

  return (
    <div className="min-h-screen flex flex-col items-center justify-center p-4 bg-background">
      <div className="max-w-md w-full text-center space-y-8">
        {/* Icon */}
        <div className="flex justify-center">
          <div className="relative">
            <div className="absolute inset-0 animate-ping rounded-full bg-yellow-500/20" />
            <div className="relative rounded-full bg-yellow-500/10 p-6">
              <WifiOff className="h-16 w-16 text-yellow-500" />
            </div>
          </div>
        </div>

        {/* Title */}
        <div className="space-y-2">
          <h1 className="text-3xl font-bold tracking-tight">You're Offline</h1>
          <p className="text-muted-foreground">
            It looks like you've lost your internet connection. Some features may be limited.
          </p>
        </div>

        {/* Cached content info */}
        {storageStats && (storageStats.sessions > 0 || storageStats.cachedGames > 0) && (
          <div className="bg-muted/50 rounded-lg p-4 space-y-2">
            <h3 className="font-medium text-sm">Available Offline</h3>
            <div className="flex justify-center gap-6 text-sm text-muted-foreground">
              {storageStats.sessions > 0 && (
                <div className="flex items-center gap-1">
                  <Gamepad2 className="h-4 w-4" />
                  <span>{storageStats.sessions} sessions</span>
                </div>
              )}
              {storageStats.cachedGames > 0 && (
                <div className="flex items-center gap-1">
                  <span>{storageStats.cachedGames} games</span>
                </div>
              )}
            </div>
            {storageStats.pendingActions > 0 && (
              <p className="text-xs text-yellow-600 dark:text-yellow-400">
                {storageStats.pendingActions} action{storageStats.pendingActions > 1 ? 's' : ''}{' '}
                waiting to sync
              </p>
            )}
          </div>
        )}

        {/* Actions */}
        <div className="flex flex-col sm:flex-row gap-3 justify-center">
          <Button onClick={handleRetry} disabled={retrying} variant="default" className="gap-2">
            <RefreshCw className={retrying ? 'h-4 w-4 animate-spin' : 'h-4 w-4'} />
            {retrying ? 'Retrying...' : 'Try Again'}
          </Button>

          <Button asChild variant="outline" className="gap-2">
            <Link href="/">
              <Home className="h-4 w-4" />
              Go Home
            </Link>
          </Button>
        </div>

        {/* Tips */}
        <div className="text-sm text-muted-foreground space-y-2 pt-4 border-t">
          <p className="font-medium">Tips while offline:</p>
          <ul className="text-left list-disc list-inside space-y-1">
            <li>Check your Wi-Fi or mobile data connection</li>
            <li>Previously visited pages may still be available</li>
            <li>Your actions will sync when you're back online</li>
          </ul>
        </div>
      </div>
    </div>
  );
}
