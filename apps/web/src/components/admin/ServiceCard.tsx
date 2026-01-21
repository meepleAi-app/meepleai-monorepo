/* eslint-disable security/detect-object-injection -- Safe health state config Record access */
/**
 * ServiceCard Component - Issue #896
 *
 * Displays infrastructure service health status in a card format.
 * Reuses StatCard pattern with service-specific health indicators.
 *
 * Features:
 * - Health state badge (healthy/degraded/unhealthy)
 * - Response time metric
 * - Last check timestamp
 * - Error message display
 * - i18n support (IT/EN)
 */

import { CheckCircleIcon, AlertTriangleIcon, XCircleIcon, ActivityIcon } from 'lucide-react';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import type { HealthState } from '@/lib/api';
import { getInfrastructureI18n, type Locale } from '@/lib/i18n/infrastructure';
import { cn } from '@/lib/utils';

export interface ServiceCardProps {
  /** Service name (e.g., 'postgres', 'redis') */
  serviceName: string;
  /** Health state from backend */
  status: HealthState;
  /** Optional error message for degraded/unhealthy states */
  errorMessage?: string | null;
  /** Response time in milliseconds */
  responseTimeMs?: number;
  /** Last check timestamp */
  lastCheck?: Date;
  /** Loading state */
  loading?: boolean;
  /** Current locale for i18n */
  locale?: Locale;
  /** Additional CSS classes */
  className?: string;
}

const stateConfig = {
  Healthy: {
    icon: CheckCircleIcon,
    bgColor: 'bg-green-50/50 dark:bg-green-950/30',
    borderColor: 'border-green-200 dark:border-green-800',
    badgeBg: 'bg-green-100 dark:bg-green-900/50',
    badgeText: 'text-green-700 dark:text-green-300',
    iconColor: 'text-green-600 dark:text-green-400',
    dotColor: 'bg-green-500',
  },
  Degraded: {
    icon: AlertTriangleIcon,
    bgColor: 'bg-yellow-50/50 dark:bg-amber-950/30',
    borderColor: 'border-yellow-200 dark:border-amber-800',
    badgeBg: 'bg-yellow-100 dark:bg-amber-900/50',
    badgeText: 'text-yellow-700 dark:text-amber-300',
    iconColor: 'text-yellow-600 dark:text-amber-400',
    dotColor: 'bg-amber-500',
  },
  Unhealthy: {
    icon: XCircleIcon,
    bgColor: 'bg-red-50/50 dark:bg-red-950/30',
    borderColor: 'border-red-200 dark:border-red-800',
    badgeBg: 'bg-red-100 dark:bg-red-900/50',
    badgeText: 'text-red-700 dark:text-red-300',
    iconColor: 'text-red-600 dark:text-red-400',
    dotColor: 'bg-red-500',
  },
} as const;

function formatResponseTime(ms: number | undefined, _locale: Locale): string {
  if (ms === undefined) return '-';
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
}

function formatTimestamp(date: Date | undefined, locale: Locale): string {
  if (!date) return '-';
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMinutes = Math.floor(diffMs / 60000);

  if (diffMinutes < 1) return locale === 'it' ? 'Ora' : 'Now';
  if (diffMinutes < 60) return `${diffMinutes} ${locale === 'it' ? 'min fa' : 'min ago'}`;
  const diffHours = Math.floor(diffMinutes / 60);
  return `${diffHours} ${locale === 'it' ? 'h fa' : 'h ago'}`;
}

export function ServiceCard({
  serviceName,
  status,
  errorMessage,
  responseTimeMs,
  lastCheck,
  loading = false,
  locale = 'it',
  className,
}: ServiceCardProps) {
  const i18n = getInfrastructureI18n(locale);
  const config = stateConfig[status] || stateConfig.Unhealthy;
  const Icon = config.icon;
  // Safe access with known keys
  const serviceKey = serviceName as keyof typeof i18n.services;

  const displayName = (
    serviceKey in i18n.services ? i18n.services[serviceKey] : serviceName
  ) as string;
  const stateLabel = i18n.states[status];

  if (loading) {
    return (
      <Card
        className={cn('border-gray-200 dark:border-gray-700', className)}
        data-testid="service-card-loading"
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
      data-testid={`service-card-${serviceName}`}
    >
      <CardContent className="p-6">
        <div className="flex items-start gap-4">
          {/* Service Icon */}
          <div className={cn('p-2 rounded-lg shrink-0', config.badgeBg)}>
            <Icon className={cn('h-6 w-6', config.iconColor)} aria-hidden="true" />
          </div>

          {/* Service Info */}
          <div className="flex-1 min-w-0">
            {/* Service Name + Pulsating Dot + Badge */}
            <div className="flex items-center gap-2 mb-2">
              <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100 truncate">
                {displayName}
              </h3>
              {/* Pulsating Status Dot - Issue #2786 */}
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
                aria-label={`${i18n.labels.status}: ${stateLabel}`}
              >
                {stateLabel}
              </span>
            </div>

            {/* Metrics */}
            <div className="space-y-1 text-xs text-gray-600 dark:text-gray-400">
              {responseTimeMs !== undefined && (
                <div className="flex items-center gap-1.5">
                  <ActivityIcon className="h-3 w-3 text-gray-400 dark:text-gray-500" aria-hidden="true" />
                  <span>
                    {i18n.labels.responseTime}: {formatResponseTime(responseTimeMs, locale)}
                  </span>
                </div>
              )}

              {lastCheck && (
                <div className="text-gray-500 dark:text-gray-400">
                  {i18n.labels.lastCheck}: {formatTimestamp(lastCheck, locale)}
                </div>
              )}

              {/* Error Message */}
              {errorMessage && status !== 'Healthy' && (
                <div className="mt-2 p-2 bg-white/60 dark:bg-gray-800/60 rounded border border-gray-200 dark:border-gray-700">
                  <p className="text-xs text-gray-700 dark:text-gray-300 break-words">
                    <span className="font-medium">{i18n.labels.errorMessage}:</span> {errorMessage}
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
