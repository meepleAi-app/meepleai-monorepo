/**
 * Overall Status Badge Component
 *
 * Issue #2516: Displays aggregated service health status
 *
 * Features:
 * - Large badge with overall state (Healthy/Degraded/Unhealthy)
 * - Service counts breakdown (healthy/degraded/unhealthy)
 * - Color-coded based on worst state
 * - Tooltip with detailed breakdown
 */

import { CheckCircleIcon, AlertTriangleIcon, XCircleIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import type { HealthState } from '@/lib/api';

interface OverallStatusBadgeProps {
  overallState: HealthState;
  healthyCount: number;
  degradedCount: number;
  unhealthyCount: number;
  totalCount: number;
}

const stateConfig = {
  Healthy: {
    icon: CheckCircleIcon,
    bgColor: 'bg-green-100 dark:bg-green-950',
    textColor: 'text-green-800 dark:text-green-200',
    iconColor: 'text-green-600 dark:text-green-400',
    borderColor: 'border-green-500',
  },
  Degraded: {
    icon: AlertTriangleIcon,
    bgColor: 'bg-yellow-100 dark:bg-yellow-950',
    textColor: 'text-yellow-800 dark:text-yellow-200',
    iconColor: 'text-yellow-600 dark:text-yellow-400',
    borderColor: 'border-yellow-500',
  },
  Unhealthy: {
    icon: XCircleIcon,
    bgColor: 'bg-red-100 dark:bg-red-950',
    textColor: 'text-red-800 dark:text-red-200',
    iconColor: 'text-red-600 dark:text-red-400',
    borderColor: 'border-red-500',
  },
};

export function OverallStatusBadge({
  overallState,
  healthyCount,
  degradedCount,
  unhealthyCount,
  totalCount,
}: OverallStatusBadgeProps) {
  const config = stateConfig[overallState];
  const Icon = config.icon;

  return (
    <Card className={`${config.borderColor} border-2`}>
      <CardContent className={`flex items-center gap-4 p-4 ${config.bgColor}`}>
        {/* Icon */}
        <Icon className={`h-8 w-8 ${config.iconColor}`} />

        {/* Status Text */}
        <div className="flex-1">
          <p className={`text-sm font-medium ${config.textColor}`}>Overall Status</p>
          <p className={`text-2xl font-bold ${config.textColor}`}>{overallState}</p>
        </div>

        {/* Service Counts */}
        <div className="flex flex-col gap-1 text-right">
          <div className="flex items-center gap-2">
            <span className="text-xs text-muted-foreground">Total:</span>
            <Badge variant="outline" className="font-mono">
              {totalCount}
            </Badge>
          </div>

          <div className="flex items-center gap-2 text-xs">
            {healthyCount > 0 && (
              <span className="text-green-600 dark:text-green-400">
                ✅ {healthyCount}
              </span>
            )}
            {degradedCount > 0 && (
              <span className="text-yellow-600 dark:text-yellow-400">
                ⚠️ {degradedCount}
              </span>
            )}
            {unhealthyCount > 0 && (
              <span className="text-red-600 dark:text-red-400">
                ❌ {unhealthyCount}
              </span>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
