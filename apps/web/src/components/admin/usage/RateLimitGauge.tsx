'use client';

/**
 * Rate-limit and balance gauge panel.
 * Issue #5081: Admin usage page — rate-limit gauge.
 *
 * Renders radial-style progress rings for RPM utilization, using the
 * OpenRouter status data already fetched by the parent page (no extra request).
 */

import { AlertTriangle, CheckCircle, Zap } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import type { OpenRouterStatusDto } from '@/lib/api/schemas/admin-knowledge-base.schemas';

// ─── Types ───────────────────────────────────────────────────────────────────

interface RateLimitGaugeProps {
  status: OpenRouterStatusDto | null | undefined;
  isLoading?: boolean;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function getUtilizationColor(percent: number): string {
  if (percent >= 0.9) return 'text-destructive';
  if (percent >= 0.7) return 'text-amber-500';
  return 'text-emerald-500';
}

function ProgressRing({
  value,
  max,
  label,
  sublabel,
  isThrottled,
}: {
  value: number;
  max: number;
  label: string;
  sublabel: string;
  isThrottled?: boolean;
}) {
  const pct = max > 0 ? Math.min(value / max, 1) : 0;
  const radius = 36;
  const circumference = 2 * Math.PI * radius;
  const dashOffset = circumference * (1 - pct);
  const colorClass = isThrottled ? 'stroke-destructive' : pct >= 0.9 ? 'stroke-amber-500' : 'stroke-emerald-500';

  return (
    <div className="flex flex-col items-center gap-2">
      <div className="relative h-24 w-24">
        <svg className="h-full w-full -rotate-90" viewBox="0 0 88 88">
          {/* Track */}
          <circle
            cx="44"
            cy="44"
            r={radius}
            fill="none"
            stroke="currentColor"
            strokeWidth="8"
            className="text-muted-foreground/20"
          />
          {/* Progress */}
          <circle
            cx="44"
            cy="44"
            r={radius}
            fill="none"
            strokeWidth="8"
            strokeLinecap="round"
            strokeDasharray={circumference}
            strokeDashoffset={dashOffset}
            className={colorClass}
            style={{ transition: 'stroke-dashoffset 0.5s ease' }}
          />
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className={`text-base font-bold ${getUtilizationColor(pct)}`}>
            {Math.round(pct * 100)}%
          </span>
        </div>
      </div>
      <div className="text-center">
        <p className="text-sm font-medium">{label}</p>
        <p className="text-xs text-muted-foreground">{sublabel}</p>
        {isThrottled && (
          <Badge variant="destructive" className="mt-1 text-xs px-1.5 py-0">
            Throttled
          </Badge>
        )}
      </div>
    </div>
  );
}

// ─── Component ───────────────────────────────────────────────────────────────

export function RateLimitGauge({ status, isLoading }: RateLimitGaugeProps) {
  if (isLoading) {
    return (
      <Card>
        <CardHeader className="pb-2">
          <Skeleton className="h-4 w-32" />
        </CardHeader>
        <CardContent>
          <div className="flex justify-around">
            <Skeleton className="h-24 w-24 rounded-full" />
            <Skeleton className="h-24 w-24 rounded-full" />
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader className="pb-2 flex flex-row items-center justify-between">
        <CardTitle className="text-sm font-medium">Rate-Limit Utilization</CardTitle>
        <Zap className="h-4 w-4 text-muted-foreground" />
      </CardHeader>
      <CardContent>
        {!status ? (
          <p className="text-sm text-muted-foreground text-center py-4">No data available</p>
        ) : (
          <>
            <div className="flex justify-around py-2">
              <ProgressRing
                value={status.currentRpm}
                max={status.limitRpm || 1}
                label={`${status.currentRpm} / ${status.limitRpm}`}
                sublabel="req / min"
                isThrottled={status.isThrottled}
              />
            </div>

            {/* Status row */}
            <div className="mt-3 flex items-center justify-between rounded-md bg-muted/50 px-3 py-2 text-xs">
              <span className="text-muted-foreground">
                Interval: {status.rateLimitInterval || '—'}
              </span>
              <span className="flex items-center gap-1">
                {status.isThrottled ? (
                  <>
                    <AlertTriangle className="h-3 w-3 text-destructive" />
                    <span className="text-destructive font-medium">Throttled</span>
                  </>
                ) : (
                  <>
                    <CheckCircle className="h-3 w-3 text-emerald-500" />
                    <span className="text-emerald-600 font-medium">OK</span>
                  </>
                )}
              </span>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}
