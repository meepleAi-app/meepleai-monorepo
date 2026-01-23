/* eslint-disable security/detect-object-injection -- Safe status config Record access */
/**
 * SystemStatus Component - Issue #885
 *
 * Displays overall system health status with service indicators.
 * Shows uptime, health checks, and critical service states.
 *
 * Features:
 * - Overall health indicator (healthy/degraded/unhealthy)
 * - Service status list
 * - Last update timestamp
 * - Refresh button
 */

import {
  CheckCircleIcon,
  XCircleIcon,
  AlertTriangleIcon,
  RefreshCwIcon,
  ActivityIcon,
} from 'lucide-react';

import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export type ServiceHealthStatus = 'healthy' | 'degraded' | 'unhealthy' | 'unknown';

export interface ServiceStatus {
  name: string;
  status: ServiceHealthStatus;
  latency?: number;
  message?: string;
}

export interface SystemStatusProps {
  services?: ServiceStatus[];
  overallStatus?: ServiceHealthStatus;
  lastUpdate?: Date;
  loading?: boolean;
  onRefresh?: () => void;
  refreshing?: boolean;
  className?: string;
}

const statusConfig = {
  healthy: {
    icon: CheckCircleIcon,
    label: 'All Systems Operational',
    bgColor: 'bg-green-50',
    borderColor: 'border-green-200',
    textColor: 'text-green-700',
    iconColor: 'text-green-500',
    dotColor: 'bg-green-500',
  },
  degraded: {
    icon: AlertTriangleIcon,
    label: 'Degraded Performance',
    bgColor: 'bg-yellow-50',
    borderColor: 'border-yellow-200',
    textColor: 'text-yellow-700',
    iconColor: 'text-yellow-500',
    dotColor: 'bg-yellow-500',
  },
  unhealthy: {
    icon: XCircleIcon,
    label: 'System Issues Detected',
    bgColor: 'bg-red-50',
    borderColor: 'border-red-200',
    textColor: 'text-red-700',
    iconColor: 'text-red-500',
    dotColor: 'bg-red-500',
  },
  unknown: {
    icon: ActivityIcon,
    label: 'Status Unknown',
    bgColor: 'bg-muted/50',
    borderColor: 'border-border/50',
    textColor: 'text-foreground',
    iconColor: 'text-muted-foreground',
    dotColor: 'bg-muted-foreground',
  },
};

const serviceStatusColors = {
  healthy: 'bg-green-500',
  degraded: 'bg-yellow-500',
  unhealthy: 'bg-red-500',
  unknown: 'bg-muted-foreground',
};

/**
 * Default services to display
 */
export const defaultServices: ServiceStatus[] = [
  { name: 'Database', status: 'healthy' },
  { name: 'Redis Cache', status: 'healthy' },
  { name: 'Vector Store', status: 'healthy' },
  { name: 'AI Services', status: 'healthy' },
];

function SystemStatusSkeleton({ className }: { className?: string }) {
  return (
    <Card className={className}>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <Skeleton className="h-6 w-32" />
          <Skeleton className="h-8 w-8 rounded-md" />
        </div>
      </CardHeader>
      <CardContent>
        <Skeleton className="h-16 w-full mb-4" />
        <div className="space-y-2" data-testid="system-status-skeleton">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={`skeleton-${i}`} className="flex items-center justify-between">
              <Skeleton className="h-4 w-24" />
              <Skeleton className="h-3 w-3 rounded-full" />
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

export function SystemStatus({
  services = defaultServices,
  overallStatus = 'healthy',
  lastUpdate,
  loading = false,
  onRefresh,
  refreshing = false,
  className,
}: SystemStatusProps) {
  if (loading) {
    return <SystemStatusSkeleton className={className} />;
  }

  const config = statusConfig[overallStatus];
  const StatusIcon = config.icon;

  return (
    <Card className={className}>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-lg font-semibold flex items-center gap-2">
            <ActivityIcon className="h-5 w-5 text-blue-500" aria-hidden="true" />
            System Status
          </CardTitle>
          {onRefresh && (
            <Button
              variant="ghost"
              size="icon"
              onClick={onRefresh}
              disabled={refreshing}
              aria-label="Refresh system status"
              data-testid="system-status-refresh"
            >
              <RefreshCwIcon
                className={cn('h-4 w-4', refreshing && 'animate-spin')}
                aria-hidden="true"
              />
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent>
        {/* Overall Status Banner */}
        <div
          className={cn(
            'flex items-center gap-3 p-4 rounded-lg border mb-4',
            config.bgColor,
            config.borderColor
          )}
          data-testid="system-status-banner"
        >
          <StatusIcon className={cn('h-6 w-6', config.iconColor)} aria-hidden="true" />
          <div className="flex-1">
            <p className={cn('font-medium', config.textColor)}>{config.label}</p>
            {lastUpdate && (
              <p className="text-xs text-muted-foreground mt-0.5">
                Last checked: {lastUpdate.toLocaleTimeString('it-IT')}
              </p>
            )}
          </div>
        </div>

        {/* Service List - Now as individual cards with border-left indicators (Issue #2849) */}
        <div className="space-y-3" role="list" aria-label="Service status list">
          {services.map(service => {
            const borderLeftClass = service.status === 'healthy' 
              ? 'border-l-green-500'
              : service.status === 'degraded'
              ? 'border-l-yellow-500'
              : service.status === 'unhealthy'
              ? 'border-l-red-500'
              : 'border-l-border';

            const bgClass = service.status === 'degraded'
              ? 'bg-yellow-50 dark:bg-yellow-500/10'
              : service.status === 'unhealthy'
              ? 'bg-red-50 dark:bg-red-500/10'
              : 'bg-card';

            return (
              <div
                key={service.name}
                className={cn(
                  'flex items-center justify-between p-4 rounded-xl border-l-4 border border-meeple-border',
                  'hover-card hover-shadow-meeple cursor-pointer',
                  borderLeftClass,
                  bgClass
                )}
                role="listitem"
                data-testid={`service-status-${service.name.toLowerCase().replace(/\s+/g, '-')}`}
              >
                <div className="flex items-center gap-3">
                  <div
                    className={cn(
                      'h-3 w-3 rounded-full animate-pulse-meeple',
                      serviceStatusColors[service.status]
                    )}
                    title={`${service.name}: ${service.status}`}
                    aria-label={`${service.name} is ${service.status}`}
                  />
                  <div>
                    <div className="text-sm font-bold font-heading text-foreground">
                      {service.name}
                    </div>
                    {service.latency !== undefined && (
                      <div className="text-xs text-muted-foreground font-semibold">
                        {service.latency}ms response time
                      </div>
                    )}
                  </div>
                </div>
                <div className="text-xs text-muted-foreground/70">
                  {service.message || '10s ago'}
                </div>
              </div>
            );
          })}
        </div>
      </CardContent>
    </Card>
  );
}