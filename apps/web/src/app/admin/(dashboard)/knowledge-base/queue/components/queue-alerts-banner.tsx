'use client';

import {
  AlertTriangleIcon,
  AlertOctagonIcon,
  ClockIcon,
  LayersIcon,
  TrendingDownIcon,
  XIcon,
} from 'lucide-react';

import { cn } from '@/lib/utils';

import { useActiveAlerts } from '../lib/queue-api';

import type { QueueAlertDto, QueueAlertType } from '../lib/queue-api';

const ALERT_ICONS: Record<QueueAlertType, typeof ClockIcon> = {
  DocumentStuck: ClockIcon,
  QueueDepthHigh: LayersIcon,
  HighFailureRate: TrendingDownIcon,
};

function AlertItem({ alert, onDismiss }: { alert: QueueAlertDto; onDismiss: () => void }) {
  const Icon = ALERT_ICONS[alert.type];
  const isCritical = alert.severity === 'Critical';

  return (
    <div
      className={cn(
        'flex items-start gap-2 px-3 py-2 rounded-lg text-sm',
        isCritical
          ? 'bg-red-50 dark:bg-red-900/20 text-red-900 dark:text-red-200 border border-red-200 dark:border-red-800/50'
          : 'bg-amber-50 dark:bg-amber-900/20 text-amber-900 dark:text-amber-200 border border-amber-200 dark:border-amber-800/50'
      )}
      data-testid={`alert-${alert.type}`}
    >
      {isCritical ? (
        <AlertOctagonIcon className="h-4 w-4 shrink-0 mt-0.5" />
      ) : (
        <AlertTriangleIcon className="h-4 w-4 shrink-0 mt-0.5" />
      )}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1.5">
          <Icon className="h-3.5 w-3.5 shrink-0" />
          <span className="font-medium">{alert.message}</span>
        </div>
      </div>
      <button
        type="button"
        onClick={onDismiss}
        className="shrink-0 p-0.5 rounded hover:bg-black/10 dark:hover:bg-white/10"
        aria-label="Dismiss alert"
      >
        <XIcon className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}

export function QueueAlertsBanner() {
  const { data: alerts } = useActiveAlerts();

  if (!alerts || alerts.length === 0) return null;

  return (
    <div className="space-y-2" data-testid="queue-alerts-banner">
      {alerts.map((alert, i) => (
        <AlertItem
          key={`${alert.type}-${i}`}
          alert={alert}
          onDismiss={() => {
            // Dismiss is client-side only — alert will reappear on next poll if condition persists
          }}
        />
      ))}
    </div>
  );
}
