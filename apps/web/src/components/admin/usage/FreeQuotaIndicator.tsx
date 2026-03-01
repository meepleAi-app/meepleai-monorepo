'use client';

/**
 * Free-tier quota indicator.
 * Issue #5082: Admin usage page — free-tier quota indicator.
 *
 * Shows per-model usage bars for today vs. the 50 req/day OpenRouter free limit.
 */

import { Zap } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import type { FreeQuotaDto } from '@/lib/api/schemas/admin-knowledge-base.schemas';

// ─── Types ───────────────────────────────────────────────────────────────────

interface FreeQuotaIndicatorProps {
  data: FreeQuotaDto | null | undefined;
  isLoading?: boolean;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function barColorClass(pct: number, exhausted: boolean): string {
  if (exhausted) return 'bg-destructive';
  if (pct >= 0.8) return 'bg-amber-500';
  return 'bg-emerald-500';
}

function formatModelId(modelId: string): string {
  // Shorten long OpenRouter model IDs for display
  const parts = modelId.split('/');
  return parts.length > 1 ? parts[parts.length - 1] : modelId;
}

// ─── Component ───────────────────────────────────────────────────────────────

export function FreeQuotaIndicator({ data, isLoading }: FreeQuotaIndicatorProps) {
  if (isLoading) {
    return (
      <Card>
        <CardHeader className="pb-2">
          <Skeleton className="h-4 w-40" />
        </CardHeader>
        <CardContent className="space-y-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="space-y-1">
              <Skeleton className="h-3 w-32" />
              <Skeleton className="h-2 w-full" />
            </div>
          ))}
        </CardContent>
      </Card>
    );
  }

  const noData = !data || data.models.length === 0;

  return (
    <Card>
      <CardHeader className="pb-2 flex flex-row items-center justify-between">
        <CardTitle className="text-sm font-medium">Free Tier Quota</CardTitle>
        <Zap className="h-4 w-4 text-muted-foreground" />
      </CardHeader>
      <CardContent>
        {noData ? (
          <p className="text-sm text-muted-foreground text-center py-4">
            No free-model requests today
          </p>
        ) : (
          <div className="space-y-4">
            {/* Summary row */}
            <div className="flex items-center justify-between rounded-md bg-muted/50 px-3 py-2 text-xs">
              <span className="text-muted-foreground">
                Total free requests today
              </span>
              <span className="font-semibold">{data.totalFreeRequestsToday}</span>
            </div>

            {/* Per-model bars */}
            <div className="space-y-3">
              {data.models.map((model) => {
                const pct = Math.min(model.percentUsed, 1);
                return (
                  <div key={model.modelId}>
                    <div className="flex items-center justify-between mb-1">
                      <span
                        className="text-xs font-medium truncate max-w-[60%]"
                        title={model.modelId}
                      >
                        {formatModelId(model.modelId)}
                      </span>
                      <div className="flex items-center gap-1.5">
                        <span className="text-xs text-muted-foreground">
                          {model.requestsToday} / {model.dailyLimit}
                        </span>
                        {model.isExhausted && (
                          <Badge variant="destructive" className="text-[10px] px-1 py-0 h-4">
                            Exhausted
                          </Badge>
                        )}
                      </div>
                    </div>
                    <div className="relative h-2 rounded-full bg-muted overflow-hidden">
                      <div
                        className={`absolute inset-y-0 left-0 rounded-full transition-all duration-500 ${barColorClass(pct, model.isExhausted)}`}
                        style={{ width: `${pct * 100}%` }}
                        aria-label={`${model.modelId}: ${Math.round(pct * 100)}% used`}
                      />
                    </div>
                    {model.nextResetUtc && (
                      <p className="text-[10px] text-muted-foreground mt-0.5">
                        Resets {new Date(model.nextResetUtc).toLocaleTimeString()} UTC
                      </p>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
