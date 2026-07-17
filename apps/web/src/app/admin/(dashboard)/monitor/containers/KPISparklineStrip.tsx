'use client';

/**
 * KPISparklineStrip (#1837 SP5 F4-C1) — 4 KPI con mini-sparkline SVG
 * sopra il ContainerDashboard.
 *
 * KPI rendered (match mockup `sp5-admin-infra.html` riga 275-316):
 *   1. Container attivi (active/total) — no sparkline (no time-series)
 *   2. CPU media (last value + sparkline da getMetricsTimeSeries.cpu)
 *   3. RAM usata (last value/total + sparkline da getMetricsTimeSeries.memory)
 *   4. Batch job in coda (queued · running) — no sparkline
 *
 * Entity color mapping:
 *   - containers → entity-toolkit (green)
 *   - cpu → entity-chat (blue)
 *   - memory → entity-kb (teal)
 *   - batchJobs → entity-agent (amber)
 */

import { cn } from '@/lib/utils';

import {
  useInfrastructureKpis,
  type TimeSeriesPoint,
  type Trend,
} from './hooks/use-infrastructure-kpis';

const SPARKLINE_WIDTH = 70;
const SPARKLINE_HEIGHT = 28;

function buildSparklinePath(series: TimeSeriesPoint[]): { line: string; fill: string } | null {
  if (series.length < 2) return null;
  const values = series.map(p => p.value);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min || 1;
  const stepX = SPARKLINE_WIDTH / (series.length - 1);
  const points = series.map((p, i) => {
    const x = i * stepX;
    const y = SPARKLINE_HEIGHT - ((p.value - min) / range) * (SPARKLINE_HEIGHT - 4) - 2;
    return `${x.toFixed(2)},${y.toFixed(2)}`;
  });
  const line = `M${points.join(' L')}`;
  const fill = `${line} L${SPARKLINE_WIDTH},${SPARKLINE_HEIGHT} L0,${SPARKLINE_HEIGHT} Z`;
  return { line, fill };
}

function TrendLabel({ trend, pct, suffix }: { trend: Trend; pct: number; suffix?: string }) {
  if (trend === 'flat') {
    return <span className="text-muted-foreground">▬ stabile {suffix ?? ''}</span>;
  }
  const arrow = trend === 'up' ? '▲' : '▼';
  const colorClass =
    trend === 'up' ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400';
  return (
    <span className={colorClass}>
      {arrow} {Math.abs(pct)}% {suffix ?? ''}
    </span>
  );
}

function Sparkline({
  series,
  testId,
  colorClass,
}: {
  series: TimeSeriesPoint[];
  testId: string;
  colorClass: string;
}) {
  const path = buildSparklinePath(series);
  if (!path) return null;
  return (
    <svg
      data-testid={testId}
      aria-hidden="true"
      width={SPARKLINE_WIDTH}
      height={SPARKLINE_HEIGHT}
      viewBox={`0 0 ${SPARKLINE_WIDTH} ${SPARKLINE_HEIGHT}`}
      className={cn('absolute right-3 top-3 opacity-85', colorClass)}
    >
      <path d={path.fill} fill="currentColor" opacity={0.15} />
      <path
        d={path.line}
        fill="none"
        stroke="currentColor"
        strokeWidth={1.5}
        strokeLinejoin="round"
      />
    </svg>
  );
}

function KpiCardSkeleton({ testId }: { testId: string }) {
  return (
    <div
      data-testid={testId}
      className="relative rounded-xl border border-border bg-card/60 p-4 min-h-[88px] animate-pulse"
    >
      <div className="mb-2 h-3 w-24 rounded bg-muted" />
      <div className="h-7 w-16 rounded bg-muted" />
    </div>
  );
}

// #3045: placeholder condiviso per lo stato "sorgente non disponibile" (CPU + RAM).
const SOURCE_UNAVAILABLE_LABEL = 'Sorgente non disponibile';

function MetricUnavailable({ testId }: { testId: string }) {
  return (
    <p data-testid={testId} className="mt-1 font-mono text-[13px] text-muted-foreground">
      {SOURCE_UNAVAILABLE_LABEL}
    </p>
  );
}

export function KPISparklineStrip() {
  const { containers, cpu, memory, batchJobs } = useInfrastructureKpis();

  return (
    <div
      className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4"
      data-testid="kpi-sparkline-strip"
    >
      {containers.loading ? (
        <KpiCardSkeleton testId="kpi-skeleton-containers" />
      ) : (
        <article
          data-testid="kpi-card"
          aria-label={`Container attivi ${containers.active} su ${containers.total}, ${containers.stopped} stopped`}
          className="relative rounded-xl border border-border bg-card/60 p-4 min-h-[88px] border-l-4 border-l-entity-toolkit"
        >
          <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
            Container attivi
          </p>
          <p className="mt-1 font-quicksand text-2xl font-bold text-foreground tabular-nums">
            {containers.active}
            <span className="text-sm text-muted-foreground font-semibold">/{containers.total}</span>
          </p>
          <p className="mt-1 font-mono text-[11px] text-muted-foreground">
            ▬ {containers.stopped} stopped
          </p>
        </article>
      )}

      {cpu.loading ? (
        <KpiCardSkeleton testId="kpi-skeleton-cpu" />
      ) : (
        <article
          data-testid="kpi-card"
          aria-label={
            cpu.sourceAvailable
              ? `CPU media ${cpu.value}%, trend ${cpu.trend} ${cpu.trendPct}%`
              : 'CPU media: sorgente non disponibile'
          }
          className="relative rounded-xl border border-border bg-card/60 p-4 min-h-[88px] border-l-4 border-l-entity-chat"
        >
          <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
            CPU media
          </p>
          {cpu.sourceAvailable ? (
            <>
              <p className="mt-1 font-quicksand text-2xl font-bold text-foreground tabular-nums">
                {cpu.value}
                <span className="text-sm text-muted-foreground font-semibold">%</span>
              </p>
              <p className="mt-1 font-mono text-[11px]">
                <TrendLabel trend={cpu.trend} pct={cpu.trendPct} suffix="vs 1h fa" />
              </p>
              <Sparkline
                series={cpu.series}
                testId="kpi-sparkline-cpu"
                colorClass="text-entity-chat"
              />
            </>
          ) : (
            <MetricUnavailable testId="kpi-cpu-unavailable" />
          )}
        </article>
      )}

      {memory.loading ? (
        <KpiCardSkeleton testId="kpi-skeleton-memory" />
      ) : (
        <article
          data-testid="kpi-card"
          aria-label={
            !memory.sourceAvailable
              ? 'RAM usata: sorgente non disponibile'
              : memory.total > 0
                ? `RAM usata ${memory.value} di ${memory.total} GB`
                : `RAM usata ${memory.value} GB`
          }
          className="relative rounded-xl border border-border bg-card/60 p-4 min-h-[88px] border-l-4 border-l-entity-kb"
        >
          <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
            RAM usata
          </p>
          {memory.sourceAvailable ? (
            <>
              <p className="mt-1 font-quicksand text-2xl font-bold text-foreground tabular-nums">
                {memory.value}
                <span className="text-sm text-muted-foreground font-semibold">
                  {memory.total > 0 ? `/${memory.total} GB` : ' GB'}
                </span>
              </p>
              <p className="mt-1 font-mono text-[11px]">
                <TrendLabel trend={memory.trend} pct={memory.trendPct} suffix="ultimi 60m" />
              </p>
              <Sparkline
                series={memory.series}
                testId="kpi-sparkline-memory"
                colorClass="text-entity-kb"
              />
            </>
          ) : (
            <MetricUnavailable testId="kpi-memory-unavailable" />
          )}
        </article>
      )}

      {batchJobs.loading ? (
        <KpiCardSkeleton testId="kpi-skeleton-batchjobs" />
      ) : (
        <article
          data-testid="kpi-card"
          aria-label={`Batch job: ${batchJobs.queued} in coda, ${batchJobs.running} in esecuzione`}
          className="relative rounded-xl border border-border bg-card/60 p-4 min-h-[88px] border-l-4 border-l-entity-agent"
        >
          <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
            Batch job in coda
          </p>
          <p className="mt-1 font-quicksand text-2xl font-bold text-foreground tabular-nums">
            {batchJobs.queued}
            <span className="text-sm text-muted-foreground font-semibold">
              {' '}
              · {batchJobs.running} run
            </span>
          </p>
          <p className="mt-1 font-mono text-[11px] text-muted-foreground">▬ live</p>
        </article>
      )}
    </div>
  );
}
