'use client';

import { cn } from '@/lib/utils';

export interface TrendDatapoint {
  date: string; // YYYY-MM-DD
  avgLatencyMs: number;
  requestCount: number;
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
 * AI trend chart — inline SVG with two series:
 *   - avgLatencyMs (ms)  → entity-session line
 *   - requestCount (n)   → entity-toolkit line (scaled to right axis)
 *
 * The mockup A4 asks for p50/p95/error series, but
 * `/api/v1/admin/model-performance?days=` only exposes daily averages
 * and aggregate success rate. The dedicated `/ai/metrics?range=`
 * endpoint (#1722 sub-task BE) will unlock that — until then we surface
 * a visible "approx" badge so reviewers know the trend is daily-avg,
 * not p50/p95.
 */
export function AiTrendChart({ data, range, rangeOptions, onRangeChange }: AiTrendChartProps) {
  const hasData = data.length > 0;

  const latencyPath = hasData ? buildSvgPath(data, d => d.avgLatencyMs) : '';
  const volumePath = hasData ? buildSvgPath(data, d => d.requestCount) : '';

  return (
    <section className="rounded-xl border border-border/60 bg-card/70 backdrop-blur-md p-4 space-y-3">
      <header className="flex items-center gap-2 flex-wrap">
        <div>
          <h3 className="font-quicksand text-sm font-semibold text-foreground">
            Latency &amp; volume trend
          </h3>
          <p className="font-mono text-[10px] text-muted-foreground mt-0.5">
            avgLatencyMs (sx) · requestCount (dx)
          </p>
        </div>
        <span className="inline-flex items-center rounded-full bg-warning/15 px-2 py-0.5 text-[10px] font-medium text-warning">
          approx — p50/p95/error pending
        </span>
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
            aria-label="Latency and request volume trend"
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
            <polyline
              data-series="latency"
              fill="none"
              className="stroke-entity-session"
              strokeWidth={2}
              points={latencyPath}
            />
            <polyline
              data-series="volume"
              fill="none"
              className="stroke-entity-toolkit"
              strokeWidth={2}
              strokeDasharray="4 2"
              points={volumePath}
            />
          </svg>
          {/* sr-only data table */}
          <table className="sr-only">
            <caption>Daily AI latency and volume</caption>
            <thead>
              <tr>
                <th scope="col">Date</th>
                <th scope="col">avgLatencyMs</th>
                <th scope="col">requestCount</th>
              </tr>
            </thead>
            <tbody>
              {data.map(d => (
                <tr key={d.date}>
                  <td>{d.date}</td>
                  <td>{Math.round(d.avgLatencyMs)}</td>
                  <td>{d.requestCount}</td>
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

function buildSvgPath(data: TrendDatapoint[], pick: (d: TrendDatapoint) => number): string {
  if (data.length === 0) return '';
  const innerWidth = CHART_WIDTH - PADDING * 2;
  const innerHeight = CHART_HEIGHT - PADDING * 2;
  const xs = data.map((_, i) =>
    data.length === 1 ? PADDING + innerWidth / 2 : PADDING + (i / (data.length - 1)) * innerWidth
  );
  const ys = data.map(d => pick(d));
  const maxY = Math.max(...ys, 1);
  const minY = Math.min(...ys, 0);
  const range = Math.max(maxY - minY, 1);
  const mapped = ys.map(y => CHART_HEIGHT - PADDING - ((y - minY) / range) * innerHeight);
  return data.map((_, i) => `${xs[i].toFixed(1)},${mapped[i].toFixed(1)}`).join(' ');
}
