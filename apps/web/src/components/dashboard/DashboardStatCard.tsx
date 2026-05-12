'use client';

import Link from 'next/link';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

export interface DashboardStatCardProps {
  entity: MeepleEntityType;
  value: number;
  label: string;
  href: string;
  isLoading?: boolean;
  isError?: boolean;
  isFetching?: boolean;
  onRetry?: () => void;
}

export function DashboardStatCard({
  entity,
  value,
  label,
  href,
  isLoading,
  isError,
  isFetching,
  onRetry,
}: DashboardStatCardProps) {
  if (isError) {
    return (
      <button
        type="button"
        data-entity={entity}
        onClick={onRetry}
        disabled={isFetching || !onRetry}
        aria-label={`Errore caricamento ${label}. Premi Invio per riprovare`}
        className={`e-${entity} relative w-full overflow-hidden rounded-xl border border-border bg-card p-5
                    text-left transition-all duration-300 ease-out
                    motion-safe:hover:-translate-y-[3px] motion-safe:hover:scale-[1.02]
                    hover:border-[hsl(var(--e)/0.4)] hover:shadow-md
                    focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[hsl(var(--e))]
                    disabled:cursor-not-allowed disabled:opacity-60`}
      >
        <div className="font-quicksand text-[32px] font-extrabold leading-none tracking-tight tabular-nums text-[hsl(var(--e))]">
          —
        </div>
        <div className="mt-1 text-xs font-bold uppercase tracking-wider text-muted-foreground">
          {label}
        </div>
      </button>
    );
  }

  return (
    <Link
      href={href}
      data-entity={entity}
      aria-label={`${label}: ${value} elementi. Vai a ${href}`}
      className={`e-${entity} relative block overflow-hidden rounded-xl border border-border bg-card p-5
                  transition-all duration-300
                  motion-safe:hover:-translate-y-[3px] motion-safe:hover:scale-[1.02]
                  hover:border-[hsl(var(--e)/0.4)] hover:shadow-md
                  focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[hsl(var(--e))]`}
    >
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 opacity-70 transition-opacity duration-300
                   bg-[radial-gradient(circle_at_top_right,hsl(var(--e)/0.10),transparent_60%)]
                   dark:bg-[radial-gradient(circle_at_top_right,hsl(var(--e)/0.18),transparent_60%)]
                   hover:opacity-100"
      />
      {isLoading ? (
        <div
          data-testid="stat-card-skeleton"
          className="h-8 w-12 animate-pulse rounded bg-muted"
        />
      ) : (
        <div className="font-quicksand text-[32px] font-extrabold leading-none tracking-tight tabular-nums text-[hsl(var(--e))]">
          {value}
        </div>
      )}
      <div className="mt-1 text-xs font-bold uppercase tracking-wider text-muted-foreground">
        {label}
      </div>
    </Link>
  );
}
