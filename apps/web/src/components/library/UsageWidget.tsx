/**
 * UsageWidget — User usage summary card
 * Game Night Improvvisata
 *
 * Displays current tier and quota usage with progress bars.
 * Shows an upgrade CTA when on free tier and approaching limits.
 */

'use client';

import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';

import { Progress } from '@/components/ui/feedback/progress';
import { TierBadge } from '@/components/ui/feedback/tier-badge';
import { api } from '@/lib/api';
import type { UsageSnapshot } from '@/lib/api/schemas/tier.schemas';
import { cn } from '@/lib/utils';
import type { UserTier } from '@/types/permissions';

// ── Helpers ────────────────────────────────────────────────────────────────────

function pct(current: number, max: number): number {
  if (max <= 0 || max >= 2_147_483_647) return 0;
  return Math.min(100, Math.round((current / max) * 100));
}

function formatMax(max: number): string {
  if (max >= 2_147_483_647) return '∞';
  return String(max);
}

function labelColor(percentage: number): string {
  if (percentage >= 95) return 'text-red-600 dark:text-red-400';
  if (percentage >= 80) return 'text-amber-600 dark:text-amber-400';
  return 'text-muted-foreground';
}

// ── Sub-components ─────────────────────────────────────────────────────────────

interface QuotaRowProps {
  label: string;
  current: number;
  max: number;
}

function QuotaRow({ label, current, max }: QuotaRowProps) {
  const percentage = pct(current, max);
  const isUnlimited = max >= 2_147_483_647;

  if (isUnlimited) {
    return (
      <div className="flex items-center justify-between text-xs" data-testid={`quota-${label}`}>
        <span className="text-muted-foreground">{label}</span>
        <span className="font-medium tabular-nums text-muted-foreground">∞</span>
      </div>
    );
  }

  return (
    <div className="space-y-1" data-testid={`quota-${label}`}>
      <div className="flex items-center justify-between text-xs">
        <span className="text-muted-foreground">{label}</span>
        <span className={cn('font-medium tabular-nums', labelColor(percentage))}>
          {current}/{formatMax(max)}
        </span>
      </div>
      <Progress
        value={percentage}
        className={cn(
          'h-1.5',
          percentage >= 95 && 'bg-red-100 dark:bg-red-950',
          percentage >= 80 && percentage < 95 && 'bg-amber-100 dark:bg-amber-950'
        )}
        aria-label={`${label}: ${current} di ${formatMax(max)}`}
      />
    </div>
  );
}

// ── Skeleton ───────────────────────────────────────────────────────────────────

function UsageWidgetSkeleton() {
  return (
    <div className="rounded-xl border bg-white/70 backdrop-blur-md p-4 space-y-3 animate-pulse">
      <div className="h-5 w-24 rounded bg-muted" />
      <div className="space-y-2">
        {[1, 2, 3, 4].map(i => (
          <div key={i} className="space-y-1">
            <div className="flex justify-between">
              <div className="h-3 w-28 rounded bg-muted" />
              <div className="h-3 w-10 rounded bg-muted" />
            </div>
            <div className="h-1.5 w-full rounded bg-muted" />
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Main component ─────────────────────────────────────────────────────────────

interface UsageWidgetProps {
  /** Inferred tier from the user's profile (passed in to avoid extra fetch) */
  tier?: UserTier;
  className?: string;
}

/**
 * UsageWidget shows the user's current quota usage.
 *
 * @example
 * ```tsx
 * <UsageWidget tier="free" />
 * ```
 */
export function UsageWidget({ tier = 'free', className }: UsageWidgetProps) {
  const {
    data: usage,
    isLoading,
    isError,
  } = useQuery<UsageSnapshot>({
    queryKey: ['users', 'me', 'usage'],
    queryFn: () => api.tiers.getMyUsage(),
    staleTime: 60_000,
  });

  if (isLoading) return <UsageWidgetSkeleton />;

  if (isError || !usage) {
    return (
      <div
        className={cn(
          'rounded-xl border bg-white/70 backdrop-blur-md p-4 text-xs text-muted-foreground',
          className
        )}
      >
        Impossibile caricare i dati di utilizzo.
      </div>
    );
  }

  const isFree = tier === 'free';

  const quotas: Array<{ label: string; current: number; max: number }> = [
    { label: 'Giochi privati', current: usage.privateGames, max: usage.privateGamesMax },
    { label: 'PDF questo mese', current: usage.pdfThisMonth, max: usage.pdfThisMonthMax },
    { label: 'Query oggi', current: usage.agentQueriesToday, max: usage.agentQueriesTodayMax },
    { label: 'Agent', current: usage.agents, max: usage.agentsMax },
    { label: 'Query sessione', current: usage.sessionQueries, max: usage.sessionQueriesMax },
  ];

  return (
    <div
      className={cn(
        'rounded-xl border bg-white/70 backdrop-blur-md p-4 space-y-3',
        'dark:bg-zinc-900/70',
        className
      )}
      data-testid="usage-widget"
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <span className="font-quicksand text-sm font-semibold">Il tuo piano</span>
        <TierBadge tier={tier} />
      </div>

      {/* Quota rows */}
      <div className="space-y-2.5">
        {quotas.map(q => (
          <QuotaRow key={q.label} label={q.label} current={q.current} max={q.max} />
        ))}
      </div>

      {/* Session save indicator */}
      <div className="flex items-center justify-between text-xs border-t pt-2">
        <span className="text-muted-foreground">Salvataggio sessione</span>
        <span
          className={cn(
            'font-medium',
            usage.sessionSaveEnabled ? 'text-green-600' : 'text-muted-foreground'
          )}
        >
          {usage.sessionSaveEnabled ? '✓ Abilitato' : '✗ Non disponibile'}
        </span>
      </div>

      {/* Upgrade CTA */}
      {isFree && (
        <Link
          href="/pricing"
          className={cn(
            'block w-full rounded-lg px-3 py-2 text-center text-xs font-semibold',
            'bg-amber-100 text-amber-900 hover:bg-amber-200 transition-colors',
            'dark:bg-amber-900/30 dark:text-amber-300 dark:hover:bg-amber-900/50'
          )}
          data-testid="upgrade-cta"
        >
          Passa a Premium →
        </Link>
      )}
    </div>
  );
}
