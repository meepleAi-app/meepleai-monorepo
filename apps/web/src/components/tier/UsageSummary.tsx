/**
 * UsageSummary - Multi-Meter Usage Overview (Game Night Improvvisata - E2-4)
 *
 * Compact layout showing key usage metrics for sidebar placement.
 * Uses the useUsage hook for data fetching.
 */

'use client';

import React from 'react';

import { Loader2 } from 'lucide-react';

import { useUsage } from '@/hooks/useUsage';
import type { UsageSnapshot } from '@/lib/api/schemas/tier.schemas';
import { cn } from '@/lib/utils';

import { UpgradeCta } from './UpgradeCta';
import { UsageMeter } from './UsageMeter';

// ============================================================================
// Types
// ============================================================================

export interface UsageSummaryProps {
  /** Optional extra CSS class */
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

interface MeterConfig {
  label: string;
  currentKey: keyof UsageSnapshot;
  maxKey: keyof UsageSnapshot;
  limitType: string;
}

const METERS: MeterConfig[] = [
  {
    label: 'Giochi privati',
    currentKey: 'privateGames',
    maxKey: 'privateGamesMax',
    limitType: 'giochi privati',
  },
  {
    label: 'PDF questo mese',
    currentKey: 'pdfThisMonth',
    maxKey: 'pdfThisMonthMax',
    limitType: 'PDF',
  },
  { label: 'Agenti AI', currentKey: 'agents', maxKey: 'agentsMax', limitType: 'agenti' },
  {
    label: 'Query AI oggi',
    currentKey: 'agentQueriesToday',
    maxKey: 'agentQueriesTodayMax',
    limitType: 'query AI',
  },
];

// ============================================================================
// Component
// ============================================================================

export const UsageSummary = React.memo(function UsageSummary({ className }: UsageSummaryProps) {
  const { data, isLoading, error } = useUsage();

  if (isLoading) {
    return (
      <div
        className={cn('flex items-center justify-center py-4', className)}
        data-testid="usage-summary-loading"
      >
        <Loader2
          className="h-4 w-4 animate-spin text-muted-foreground"
          aria-label="Caricamento utilizzo..."
        />
      </div>
    );
  }

  if (error || !data) {
    return null;
  }

  // Find the first metric that is at limit (100%+)
  const atLimit = METERS.find(m => {
    const current = data[m.currentKey] as number;
    const max = data[m.maxKey] as number;
    return max > 0 && max <= 999_999 && current >= max;
  });

  return (
    <div className={cn('space-y-3', className)} data-testid="usage-summary">
      <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider font-quicksand">
        Utilizzo
      </h4>

      <div className="space-y-2">
        {METERS.map(meter => (
          <UsageMeter
            key={meter.currentKey}
            label={meter.label}
            current={data[meter.currentKey] as number}
            max={data[meter.maxKey] as number}
          />
        ))}
      </div>

      {atLimit && (
        <UpgradeCta
          limitType={atLimit.limitType}
          current={data[atLimit.currentKey] as number}
          max={data[atLimit.maxKey] as number}
        />
      )}
    </div>
  );
});

UsageSummary.displayName = 'UsageSummary';
