/**
 * OfflineBanner — shows when the device is offline and queue is pending
 *
 * Game Session Flow v2.0 — Task 10
 */

'use client';

import { useEffect, useState } from 'react';

import { WifiOff, RefreshCw } from 'lucide-react';

import { useSyncQueueStore } from '@/lib/stores/sync-queue-store';
import { cn } from '@/lib/utils';

export function OfflineBanner() {
  const [isOffline, setIsOffline] = useState(false);
  const queueLength = useSyncQueueStore(s => s.syncQueue.length);
  const isSyncing = useSyncQueueStore(s => s.isSyncing);

  useEffect(() => {
    setIsOffline(!navigator.onLine);

    const onOnline = () => setIsOffline(false);
    const onOffline = () => setIsOffline(true);

    window.addEventListener('online', onOnline);
    window.addEventListener('offline', onOffline);

    return () => {
      window.removeEventListener('online', onOnline);
      window.removeEventListener('offline', onOffline);
    };
  }, []);

  const showBanner = isOffline || (queueLength > 0 && !isSyncing);

  if (!showBanner) return null;

  return (
    <div
      role="status"
      aria-live="polite"
      className={cn(
        'flex items-center gap-2 px-4 py-2 text-sm font-medium',
        isOffline
          ? 'bg-amber-500/20 text-amber-300 border-b border-amber-500/30'
          : 'bg-blue-500/20 text-blue-300 border-b border-blue-500/30'
      )}
    >
      {isOffline ? (
        <>
          <WifiOff className="h-4 w-4 shrink-0" />
          <span>
            Offline — le azioni verranno sincronizzate al ripristino della connessione
            {queueLength > 0 && ` (${queueLength} in coda)`}
          </span>
        </>
      ) : (
        <>
          <RefreshCw className="h-4 w-4 shrink-0 animate-spin" />
          <span>Sincronizzazione in corso ({queueLength} operazioni in coda)...</span>
        </>
      )}
    </div>
  );
}
