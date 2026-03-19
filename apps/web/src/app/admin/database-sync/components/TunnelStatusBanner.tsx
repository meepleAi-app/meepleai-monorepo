'use client';

import { Wifi, WifiOff, Loader2, AlertCircle } from 'lucide-react';

import { Button } from '@/components/ui/button';

import { useTunnelStatus, useOpenTunnel, useCloseTunnel } from '../hooks/useTunnelStatus';

import type { TunnelState } from '../types/db-sync';

const stateConfig: Record<TunnelState, { color: string; icon: typeof Wifi; label: string }> = {
  Open: {
    color:
      'bg-emerald-100 text-emerald-800 border-emerald-200 dark:bg-emerald-900/30 dark:text-emerald-400 dark:border-emerald-800/40',
    icon: Wifi,
    label: 'Connected',
  },
  Opening: {
    color:
      'bg-amber-100 text-amber-800 border-amber-200 dark:bg-amber-900/30 dark:text-amber-400 dark:border-amber-800/40',
    icon: Loader2,
    label: 'Connecting...',
  },
  Error: {
    color:
      'bg-red-100 text-red-800 border-red-200 dark:bg-red-900/30 dark:text-red-400 dark:border-red-800/40',
    icon: AlertCircle,
    label: 'Error',
  },
  Closed: {
    color:
      'bg-slate-100 text-slate-600 border-slate-200 dark:bg-zinc-800/50 dark:text-zinc-400 dark:border-zinc-700/40',
    icon: WifiOff,
    label: 'Disconnected',
  },
};

function formatUptime(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  const mins = Math.floor(seconds / 60);
  const secs = seconds % 60;
  if (mins < 60) return `${mins}m ${secs}s`;
  const hrs = Math.floor(mins / 60);
  return `${hrs}h ${mins % 60}m`;
}

export function TunnelStatusBanner() {
  const { data: status, isLoading, error } = useTunnelStatus();
  const openTunnel = useOpenTunnel();
  const closeTunnel = useCloseTunnel();

  const state: TunnelState = status?.status ?? 'Closed';
  const config = stateConfig[state];
  const Icon = config.icon;
  const isOpen = state === 'Open';
  const isTransitioning = state === 'Opening' || openTunnel.isPending || closeTunnel.isPending;

  return (
    <div
      className={`flex items-center justify-between rounded-lg border px-4 py-3 ${config.color}`}
    >
      <div className="flex items-center gap-3">
        <Icon
          className={`h-5 w-5 ${state === 'Opening' ? 'animate-spin' : ''}`}
          aria-hidden="true"
        />
        <div>
          <span className="text-sm font-medium">{config.label}</span>
          {isOpen && status && status.uptimeSeconds > 0 && (
            <span className="ml-2 text-xs opacity-75">
              Uptime: {formatUptime(status.uptimeSeconds)}
            </span>
          )}
          {status?.message && <p className="mt-0.5 text-xs opacity-75">{status.message}</p>}
          {error && !status && (
            <p className="mt-0.5 text-xs opacity-75">Failed to fetch tunnel status</p>
          )}
        </div>
      </div>

      <div className="flex items-center gap-2">
        {isLoading && !status && <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />}
        {isOpen ? (
          <Button
            variant="outline"
            size="sm"
            onClick={() => closeTunnel.mutate()}
            disabled={isTransitioning}
          >
            Disconnect
          </Button>
        ) : (
          <Button
            variant="outline"
            size="sm"
            onClick={() => openTunnel.mutate()}
            disabled={isTransitioning}
          >
            {isTransitioning ? 'Connecting...' : 'Connect'}
          </Button>
        )}
      </div>
    </div>
  );
}
