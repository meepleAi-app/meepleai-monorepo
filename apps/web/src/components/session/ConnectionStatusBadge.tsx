/**
 * ConnectionStatusBadge - SSE Connection Status Indicator
 * Issue #4767 - SSE Client + Player/Spectator Mode UI
 */

'use client';

import { Wifi, WifiOff, Loader2 } from 'lucide-react';

import type { ConnectionStatus } from '@/lib/domain-hooks/useSessionStream';

interface ConnectionStatusBadgeProps {
  status: ConnectionStatus;
  reconnectCount?: number;
  className?: string;
}

const statusConfig: Record<ConnectionStatus, { label: string; color: string; icon: typeof Wifi }> =
  {
    connected: {
      label: 'Connected',
      color: 'bg-emerald-100 text-emerald-700 border-emerald-200',
      icon: Wifi,
    },
    connecting: {
      label: 'Connecting...',
      color: 'bg-amber-100 text-amber-700 border-amber-200',
      icon: Loader2,
    },
    reconnecting: {
      label: 'Reconnecting...',
      color: 'bg-amber-100 text-amber-700 border-amber-200',
      icon: Loader2,
    },
    disconnected: {
      label: 'Offline',
      color: 'bg-gray-100 text-gray-500 border-gray-200',
      icon: WifiOff,
    },
    failed: {
      label: 'Connection Failed',
      color: 'bg-red-100 text-red-700 border-red-200',
      icon: WifiOff,
    },
  };

export function ConnectionStatusBadge({
  status,
  reconnectCount = 0,
  className = '',
}: ConnectionStatusBadgeProps) {
  const config = statusConfig[status];
  const Icon = config.icon;
  const isAnimated = status === 'connecting' || status === 'reconnecting';

  return (
    <div
      className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium ${config.color} ${className}`}
      role="status"
      aria-live="polite"
    >
      <Icon className={`h-3 w-3 ${isAnimated ? 'animate-spin' : ''}`} />
      <span>
        {config.label}
        {status === 'reconnecting' && reconnectCount > 0 && ` (${reconnectCount})`}
      </span>
    </div>
  );
}
