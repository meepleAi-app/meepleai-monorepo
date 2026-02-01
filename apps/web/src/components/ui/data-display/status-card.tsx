/* eslint-disable security/detect-object-injection -- Safe health state config Record access */
/**
 * StatusCard Component - Generic Health/Status Display Card
 *
 * Extracted from Admin ServiceCard for reuse across the application.
 * Displays health status for any entity (services, systems, processes).
 *
 * @module components/ui/data-display/status-card
 * @see Issue #2925 - Component Library extraction
 *
 * Features:
 * - Health state badge (healthy/degraded/unhealthy)
 * - Pulsating status dot indicator
 * - Response time/latency metric
 * - Last check timestamp
 * - Error message display
 * - Loading skeleton state
 *
 * @example
 * ```tsx
 * // Basic usage
 * <StatusCard
 *   name="Database"
 *   status="healthy"
 *   latencyMs={45}
 * />
 *
 * // With error message
 * <StatusCard
 *   name="API Service"
 *   status="degraded"
 *   errorMessage="Connection timeout"
 *   latencyMs={1200}
 *   lastCheck={new Date()}
 * />
 * ```
 */

import { CheckCircleIcon, AlertTriangleIcon, XCircleIcon, ActivityIcon } from 'lucide-react';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

/**
 * Status state types
 */
export type StatusState = 'healthy' | 'degraded' | 'unhealthy';

/**
 * Props for the StatusCard component
 */
export interface StatusCardProps {
  /** Display name of the entity being monitored */
  name: string;
  /** Current health/status state */
  status: StatusState;
  /** Optional error message for degraded/unhealthy states */
  errorMessage?: string | null;
  /** Response time/latency in milliseconds */
  latencyMs?: number;
  /** Last check timestamp */
  lastCheck?: Date;
  /** Show loading skeleton */
  loading?: boolean;
  /** Additional CSS classes */
  className?: string;
  /** Custom icon to display (defaults to status-based icon) */
  icon?: React.ComponentType<{ className?: string }>;
  /** Labels for i18n (defaults to English) */
  labels?: {
    status?: string;
    responseTime?: string;
    lastCheck?: string;
    errorMessage?: string;
    now?: string;
    minAgo?: string;
    hoursAgo?: string;
    /** Translated status labels */
    healthy?: string;
    degraded?: string;
    unhealthy?: string;
  };
}

/**
 * Status configuration with colors and icons
 */
const statusConfig: Record<StatusState, {
  icon: typeof CheckCircleIcon;
  bgColor: string;
  borderColor: string;
  badgeBg: string;
  badgeText: string;
  iconColor: string;
  dotColor: string;
  label: string;
}> = {
  healthy: {
    icon: CheckCircleIcon,
    bgColor: 'bg-green-50/50 dark:bg-green-950/30',
    borderColor: 'border-green-200 dark:border-green-800',
    badgeBg: 'bg-green-100 dark:bg-green-900/50',
    badgeText: 'text-green-700 dark:text-green-300',
    iconColor: 'text-green-600 dark:text-green-400',
    dotColor: 'bg-green-500',
    label: 'Healthy',
  },
  degraded: {
    icon: AlertTriangleIcon,
    bgColor: 'bg-yellow-50/50 dark:bg-amber-950/30',
    borderColor: 'border-yellow-200 dark:border-amber-800',
    badgeBg: 'bg-yellow-100 dark:bg-amber-900/50',
    badgeText: 'text-yellow-700 dark:text-amber-300',
    iconColor: 'text-yellow-600 dark:text-amber-400',
    dotColor: 'bg-amber-500',
    label: 'Degraded',
  },
  unhealthy: {
    icon: XCircleIcon,
    bgColor: 'bg-red-50/50 dark:bg-red-950/30',
    borderColor: 'border-red-200 dark:border-red-800',
    badgeBg: 'bg-red-100 dark:bg-red-900/50',
    badgeText: 'text-red-700 dark:text-red-300',
    iconColor: 'text-red-600 dark:text-red-400',
    dotColor: 'bg-red-500',
    label: 'Unhealthy',
  },
};

/**
 * Default labels for i18n
 */
const defaultLabels = {
  status: 'Status',
  responseTime: 'Response time',
  lastCheck: 'Last check',
  errorMessage: 'Error',
  now: 'Now',
  minAgo: 'min ago',
  hoursAgo: 'h ago',
  healthy: 'Healthy',
  degraded: 'Degraded',
  unhealthy: 'Unhealthy',
};

/**
 * Format response time for display
 */
function formatResponseTime(ms: number | undefined): string {
  if (ms === undefined) return '-';
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
}

/**
 * Format timestamp as relative time
 */
function formatTimestamp(date: Date | undefined, labels: typeof defaultLabels): string {
  if (!date) return '-';
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMinutes = Math.floor(diffMs / 60000);

  if (diffMinutes < 1) return labels.now;
  if (diffMinutes < 60) return `${diffMinutes} ${labels.minAgo}`;
  const diffHours = Math.floor(diffMinutes / 60);
  return `${diffHours} ${labels.hoursAgo}`;
}

/**
 * StatusCard - A reusable health/status display card
 *
 * Displays the health status of any monitored entity with visual indicators,
 * metrics, and error information.
 */
export function StatusCard({
  name,
  status,
  errorMessage,
  latencyMs,
  lastCheck,
  loading = false,
  className,
  icon: CustomIcon,
  labels: customLabels,
}: StatusCardProps) {
  const labels = { ...defaultLabels, ...customLabels };
  const config = statusConfig[status];
  const Icon = CustomIcon ?? config.icon;
  // Get translated status label
  const statusLabel = labels[status] ?? config.label;

  if (loading) {
    return (
      <Card
        className={cn('border-border/50 dark:border-border/30', className)}
        data-testid="status-card-loading"
      >
        <CardContent className="p-6">
          <div className="flex items-start gap-4">
            <Skeleton className="h-10 w-10 rounded-lg shrink-0" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-4 w-24" />
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-3 w-32" />
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card
      className={cn(
        config.bgColor,
        config.borderColor,
        'transition-all duration-200 hover:shadow-md',
        className
      )}
      data-testid={`status-card-${name.toLowerCase().replace(/\s+/g, '-')}`}
    >
      <CardContent className="p-6">
        <div className="flex items-start gap-4">
          {/* Status Icon */}
          <div className={cn('p-2 rounded-lg shrink-0', config.badgeBg)}>
            <Icon className={cn('h-6 w-6', config.iconColor)} aria-hidden="true" />
          </div>

          {/* Content */}
          <div className="flex-1 min-w-0">
            {/* Name + Pulsating Dot + Badge */}
            <div className="flex items-center gap-2 mb-2">
              <h3 className="text-sm font-semibold text-foreground truncate">
                {name}
              </h3>
              {/* Pulsating Status Dot */}
              <span className="relative flex h-3 w-3" aria-hidden="true">
                <span
                  className={cn(
                    'absolute inline-flex h-full w-full animate-ping rounded-full opacity-75',
                    config.dotColor
                  )}
                />
                <span
                  className={cn('relative inline-flex h-3 w-3 rounded-full', config.dotColor)}
                />
              </span>
              <span
                className={cn(
                  'inline-flex items-center px-2 py-0.5 rounded text-xs font-medium',
                  config.badgeBg,
                  config.badgeText
                )}
                aria-label={`${labels.status}: ${statusLabel}`}
                data-testid="status-badge"
              >
                {statusLabel}
              </span>
            </div>

            {/* Metrics */}
            <div className="space-y-1 text-xs text-muted-foreground">
              {latencyMs !== undefined && (
                <div className="flex items-center gap-1.5">
                  <ActivityIcon className="h-3 w-3 text-muted-foreground/70" aria-hidden="true" />
                  <span>
                    {labels.responseTime}: {formatResponseTime(latencyMs)}
                  </span>
                </div>
              )}

              {lastCheck && (
                <div className="text-muted-foreground">
                  {labels.lastCheck}: {formatTimestamp(lastCheck, labels)}
                </div>
              )}

              {/* Error Message */}
              {errorMessage && status !== 'healthy' && (
                <div className="mt-2 p-2 bg-card/60 backdrop-blur-[8px] dark:bg-card/60 dark:backdrop-blur-none rounded border border-border/50 dark:border-border/30">
                  <p className="text-xs text-foreground break-words">
                    <span className="font-medium">{labels.errorMessage}:</span> {errorMessage}
                  </p>
                </div>
              )}
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
