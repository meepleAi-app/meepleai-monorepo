'use client';

import { cn } from '@/lib/utils';

export interface TrendDatapoint {
  /** YYYY-MM-DD or ISO timestamp. Used as React key + sr-only table cell. */
  date: string;
  avgLatencyMs: number;
  requestCount: number;
  /** #1729: optional percentile series (populated by /admin/ai/metrics) */
  p50LatencyMs?: number;
  p95LatencyMs?: number;
  /** #1729: optional error fraction in [0, 1]. Rendered as % on the right axis. */
  errorRate?: number;
}

interface AiTrendChartProps {
  data: TrendDatapoint[];
  range: string;
  rangeOptions: readonly string[];
  onRangeChange: (range: string) => void;
}

const CHART_WIDTH = 600;
const CHART_HEIGHT = 140;
const PADDING = 24;

/**
 * AI trend chart — inline SVG.
 *
 * Renders avgLatency + volume by default; when the new
 * `/api/v1/admin/ai/metrics` endpoint provides p50/p95/error series
 * (#1729), they layer on top and the legacy "approx" badge drops.
 */
export function AiTrendChart({ data, range, rangeOptions, onRangeChange }: AiTrendChartProps) {
  const hasData = data.length > 0;
  // #1729: detect full-series payload — when all 3 percentile/error fields
  // are populated on every datapoint, we can drop the "approx" badge.
  const hasPercentileSeries =
    hasData &&
    data.every(
      d => d.p50LatencyMs !== undefined && d.p95LatencyMs !== undefined && d.errorRate !== undefined
    );

  const latencyPath = hasData ? buildSvgPath(data, d => d.avgLatencyMs) : '';
  const volumePath = hasData ? buildSvgPath(data, d => d.requestCount) : '';
  const p50Path = hasPercentileSeries ? buildSvgPath(data, d => d.p50LatencyMs ?? 0) : '';
  const p95Path = hasPercentileSeries ? buildSvgPath(data, d => d.p95LatencyMs ?? 0) : '';
  // #1735 B6: errorRate ∈ [0, 1] now uses an explicit fixed axis (0..1) instead of
  // the prior cosmetic `* 1000` magic number. Each polyline still has its own Y-axis
  // (auto-scaled by buildSvgPath), but a fixed [0, 1] range for error guarantees the
  // polyline stays within the viewBox and reads as a stable percentage scale.
  const errorPath = hasPercentileSeries
    ? buildSvgPath(data, d => d.errorRate ?? 0, { fixedMin: 0, fixedMax: 1 })
    : '';

  return (
    <section className="rounded-xl border border-border/60 bg-card/70 backdrop-blur-md p-4 space-y-3">
      <header className="flex items-center gap-2 flex-wrap">
        <div>
          <h3 className="font-quicksand text-sm font-semibold text-foreground">
            Latency &amp; volume trend
          </h3>
          <p className="font-mono text-[10px] text-muted-foreground mt-0.5">
            {hasPercentileSeries
              ? 'p50/p95 latency (sx) · requestCount (dx) · errorRate (dashed)'
              : 'avgLatencyMs (sx) · requestCount (dx)'}
          </p>
        </div>
        {!hasPercentileSeries && (
          <span className="inline-flex items-center rounded-full bg-[hsl(var(--c-warning)/0.15)] px-2 py-0.5 text-[10px] font-medium text-[hsl(var(--c-warning))]">
            approx — p50/p95/error pending
          </span>
        )}
        <div
          role="group"
          aria-label="Time range"
          className="ml-auto inline-flex items-center gap-1"
        >
          {rangeOptions.map(opt => (
            <button
              key={opt}
              type="button"
              onClick={() => onRangeChange(opt)}
              aria-pressed={opt === range}
              className={cn(
                'rounded-md border px-2 py-1 font-mono text-[11px] font-medium transition-colors',
                opt === range
                  ? 'border-primary/60 bg-primary/10 text-primary'
                  : 'border-border/60 bg-card text-muted-foreground hover:text-foreground'
              )}
            >
              {opt}
            </button>
          ))}
        </div>
      </header>

      {hasData ? (
        <>
          <svg
            role="img"
            aria-label={
              hasPercentileSeries
                ? 'Latency p50/p95, request volume and error rate trend'
                : 'Latency and request volume trend'
            }
            viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`}
            preserveAspectRatio="none"
            className="block w-full h-32"
          >
            {/* Axis baseline */}
            <line
              x1={PADDING}
              x2={CHART_WIDTH - PADDING}
              y1={CHART_HEIGHT - PADDING}
              y2={CHART_HEIGHT - PADDING}
              className="stroke-border"
              strokeWidth={1}
            />
            {!hasPercentileSeries && (
              <polyline
                data-series="latency"
                fill="none"
                className="stroke-entity-session"
                strokeWidth={2}
                points={latencyPath}
              />
            )}
            <polyline
              data-series="volume"
              fill="none"
              className="stroke-entity-toolkit"
              strokeWidth={2}
              strokeDasharray="4 2"
              points={volumePath}
            />
            {hasPercentileSeries && (
              <>
                <polyline
                  data-series="p50"
                  fill="none"
                  className="stroke-entity-session"
                  strokeWidth={2}
                  points={p50Path}
                />
                <polyline
                  data-series="p95"
                  fill="none"
                  className="stroke-entity-player"
                  strokeWidth={2}
                  points={p95Path}
                />
                <polyline
                  data-series="error"
                  fill="none"
                  className="stroke-destructive"
                  strokeWidth={1.5}
                  strokeDasharray="2 3"
                  points={errorPath}
                />
              </>
            )}
          </svg>
          {/* sr-only data table */}
          <table className="sr-only">
            <caption>AI latency and volume trend</caption>
            <thead>
              <tr>
                <th scope="col">Date</th>
                <th scope="col">avgLatencyMs</th>
                <th scope="col">requestCount</th>
                {hasPercentileSeries && (
                  <>
                    <th scope="col">p50LatencyMs</th>
                    <th scope="col">p95LatencyMs</th>
                    <th scope="col">errorRate</th>
                  </>
                )}
              </tr>
            </thead>
            <tbody>
              {data.map(d => (
                <tr key={d.date}>
                  <td>{d.date}</td>
                  <td>{Math.round(d.avgLatencyMs)}</td>
                  <td>{d.requestCount}</td>
                  {hasPercentileSeries && (
                    <>
                      <td>{d.p50LatencyMs ?? 0}</td>
                      <td>{d.p95LatencyMs ?? 0}</td>
                      <td>{((d.errorRate ?? 0) * 100).toFixed(1)}%</td>
                    </>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </>
      ) : (
        <div className="py-8 text-center font-mono text-[11px] text-muted-foreground">
          No data for the selected range
        </div>
      )}
    </section>
  );
}

function buildSvgPath(
  data: TrendDatapoint[],
  pick: (d: TrendDatapoint) => number,
  options?: { fixedMin?: number; fixedMax?: number }
): string {
  if (data.length === 0) return '';
  const innerWidth = CHART_WIDTH - PADDING * 2;
  const innerHeight = CHART_HEIGHT - PADDING * 2;
  const xs = data.map((_, i) =>
    data.length === 1 ? PADDING + innerWidth / 2 : PADDING + (i / (data.length - 1)) * innerWidth
  );
  const ys = data.map(d => pick(d));
  // #1735 B6: callers may pass fixed bounds (e.g. {0, 1} for errorRate) to anchor
  // the polyline to a stable axis. Values outside the range are clamped so the
  // polyline never escapes the viewBox.
  const maxY = options?.fixedMax ?? Math.max(...ys, 1);
  const minY = options?.fixedMin ?? Math.min(...ys, 0);
  const range = Math.max(maxY - minY, 1);
  const mapped = ys.map(y => {
    const clamped = Math.min(Math.max(y, minY), maxY);
    return CHART_HEIGHT - PADDING - ((clamped - minY) / range) * innerHeight;
  });
  return data.map((_, i) => `${xs[i].toFixed(1)},${mapped[i].toFixed(1)}`).join(' ');
}
