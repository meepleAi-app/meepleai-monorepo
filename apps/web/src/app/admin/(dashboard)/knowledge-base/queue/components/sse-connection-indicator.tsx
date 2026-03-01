'use client';

import { WifiIcon, WifiOffIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { SSEConnectionState } from '../hooks/use-queue-sse';

interface SSEConnectionIndicatorProps {
  state: SSEConnectionState;
  onReconnect?: () => void;
}

const STATE_CONFIG: Record<SSEConnectionState, { label: string; dotClass: string; animate: boolean }> = {
  connected: { label: 'Live', dotClass: 'bg-emerald-500', animate: true },
  connecting: { label: 'Connecting...', dotClass: 'bg-amber-500', animate: true },
  reconnecting: { label: 'Reconnecting...', dotClass: 'bg-amber-500', animate: true },
  closed: { label: 'Offline', dotClass: 'bg-slate-400', animate: false },
  error: { label: 'Disconnected', dotClass: 'bg-red-500', animate: false },
};

export function SSEConnectionIndicator({ state, onReconnect }: SSEConnectionIndicatorProps) {
  const config = STATE_CONFIG[state];
  const isConnected = state === 'connected';
  const canReconnect = state === 'error' || state === 'closed';

  return (
    <button
      type="button"
      onClick={canReconnect ? onReconnect : undefined}
      disabled={!canReconnect}
      className={cn(
        'flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium transition-colors',
        isConnected
          ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400'
          : canReconnect
            ? 'bg-red-50 text-red-700 dark:bg-red-900/20 dark:text-red-400 cursor-pointer hover:bg-red-100 dark:hover:bg-red-900/30'
            : 'bg-amber-50 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400',
      )}
      title={canReconnect ? 'Click to reconnect' : config.label}
      aria-label={`SSE connection: ${config.label}${canReconnect ? '. Click to reconnect.' : ''}`}
    >
      <span className="relative flex h-2 w-2">
        {config.animate && (
          <span
            className={cn(
              'absolute inline-flex h-full w-full rounded-full opacity-75 animate-ping',
              config.dotClass,
            )}
          />
        )}
        <span className={cn('relative inline-flex rounded-full h-2 w-2', config.dotClass)} />
      </span>
      {isConnected ? (
        <WifiIcon className="h-3 w-3" />
      ) : canReconnect ? (
        <WifiOffIcon className="h-3 w-3" />
      ) : null}
      {config.label}
    </button>
  );
}
