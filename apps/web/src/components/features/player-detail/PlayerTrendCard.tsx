/**
 * PlayerTrendCard — /players/[id] v2 component (issue #1549, #1485 follow-up F3).
 *
 * Mapped from `admin-mockups/design_files/sp4-player-detail.jsx:594-636`
 * (function `TrendCard`). Inline SVG line + gradient area chart showing the
 * player's win-rate trend over the last 6 ISO months.
 *
 * Pure component: all i18n strings injected via `labels`. No hooks, no chart
 * dep — handcrafted SVG keeps bundle weight under the Tier L budget.
 *
 * Renders:
 *   - Title row + delta badge (↗ +N% / ↘ -N% / hidden when < 2 points)
 *   - SVG line chart: gradient area + polyline + data-point circles
 *   - Footer axis with short month labels per data point
 *   - Empty state when points.length < 2
 *
 * Data source: `PlayerStatistics.WinRateTrend` (post-#1550 BE bundle); each
 * entry is `{month: YYYY-MM, winRate: 0..1}`, already ordered ascending.
 *
 * WCAG:
 *   - SVG is `aria-hidden="true"` (decorative). The trend datum is announced
 *     via a sr-only summary so AT users get the same information.
 *   - Delta arrow glyph is `aria-hidden`; the accessible name comes from the
 *     interpolated `delta{Up,Down,Flat}AriaLabel`.
 *   - Empty state uses `role="status"` for polite announcement.
 *
 * Refs #1485 gap report § 2.2, #1549, #1550.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

/**
 * Single monthly bucket — the public input contract of `PlayerTrendCard`.
 * Mirrors `PlayerStatistics.WinRateTrend` BE shape (#1550).
 */
export interface MonthlyWinRatePoint {
  /** ISO `YYYY-MM`. Lexicographic comparison ⇒ chronological for this format. */
  readonly month: string;
  /** Win rate in [0, 1]. `0` is a valid datum (played but never won). */
  readonly winRate: number;
}

export interface PlayerTrendCardLabels {
  readonly title: string;
  /** Template: "↗ +{percent}%" — `{percent}` substituted with integer ≥ 0. */
  readonly deltaUp: string;
  /** Template: "↘ {percent}%" — `{percent}` already includes the leading minus. */
  readonly deltaDown: string;
  /** Template: "→ 0%" — emitted when delta is exactly 0. */
  readonly deltaFlat: string;
  /** Template aria-label for positive delta. */
  readonly deltaUpAriaLabel: string;
  /** Template aria-label for negative delta. */
  readonly deltaDownAriaLabel: string;
  /** Aria-label when delta is exactly 0. */
  readonly deltaFlatAriaLabel: string;
  /** Shown when fewer than 2 points are available. */
  readonly empty: string;
  /**
   * Short month labels (3 chars typically), indexed by 1-based ISO month
   * (e.g. labels.monthsShort[0] = "Jan"/"Gen", labels.monthsShort[11] = "Dec"/"Dic").
   */
  readonly monthsShort: ReadonlyArray<string>;
  /** Aria summary template: e.g. "Win rate trend from {from}% to {to}% over {count} months". */
  readonly trendSummaryAriaLabel: string;
}

export interface PlayerTrendCardProps {
  readonly points: ReadonlyArray<MonthlyWinRatePoint>;
  readonly labels: PlayerTrendCardLabels;
  readonly className?: string;
}

// ─── SVG geometry constants ───────────────────────────────────────────────────

const SVG_VIEWBOX_WIDTH = 280;
const SVG_VIEWBOX_HEIGHT = 100;
const SVG_PADDING_Y = 8; // top/bottom padding so circles don't clip the viewbox

// ─── Helpers ──────────────────────────────────────────────────────────────────

function interpolate(template: string, values: Record<string, string | number>): string {
  return Object.entries(values).reduce(
    (acc, [key, val]) => acc.replace(`{${key}}`, String(val)),
    template
  );
}

/** Maps a winRate in [0, 1] to an SVG Y coordinate with top/bottom padding. */
function toSvgY(winRate: number): number {
  const usable = SVG_VIEWBOX_HEIGHT - 2 * SVG_PADDING_Y;
  return SVG_PADDING_Y + (1 - winRate) * usable;
}

/** Distributes N points evenly across the X axis (first at 0, last at width). */
function toSvgX(index: number, total: number): number {
  if (total <= 1) return SVG_VIEWBOX_WIDTH / 2;
  return (index / (total - 1)) * SVG_VIEWBOX_WIDTH;
}

/** Extracts the 1-based month number from an ISO YYYY-MM key. */
function parseMonthNumber(monthIso: string): number | null {
  const match = /^\d{4}-(\d{2})$/.exec(monthIso);
  if (!match) return null;
  const m = Number.parseInt(match[1] ?? '', 10);
  return Number.isFinite(m) && m >= 1 && m <= 12 ? m : null;
}

interface DeltaInfo {
  readonly direction: 'up' | 'down' | 'flat';
  /** Absolute integer percentage difference (rounded). */
  readonly absPercent: number;
  /** Signed integer percentage difference (negative when down). */
  readonly signedPercent: number;
}

/** Computes the rounded percentage delta from first to last point. */
function computeDelta(points: ReadonlyArray<MonthlyWinRatePoint>): DeltaInfo | null {
  if (points.length < 2) return null;
  const first = points[0];
  const last = points[points.length - 1];
  if (!first || !last) return null;
  const diff = last.winRate - first.winRate;
  const signed = Math.round(diff * 100);
  const abs = Math.abs(signed);
  if (signed > 0) return { direction: 'up', absPercent: abs, signedPercent: signed };
  if (signed < 0) return { direction: 'down', absPercent: abs, signedPercent: signed };
  return { direction: 'flat', absPercent: 0, signedPercent: 0 };
}

export function PlayerTrendCard({ points, labels, className }: PlayerTrendCardProps): ReactElement {
  const hasEnoughPoints = points.length >= 2;
  const delta = computeDelta(points);

  return (
    <div
      data-slot="player-detail-trend"
      className={clsx(
        'flex flex-col gap-3 rounded-2xl border border-border bg-card p-4 shadow-sm',
        className
      )}
    >
      {/* Header */}
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="font-display text-[15px] font-extrabold text-foreground">{labels.title}</h3>
        {delta != null ? <DeltaBadge delta={delta} labels={labels} /> : null}
      </div>

      {!hasEnoughPoints ? (
        <p
          data-slot="player-detail-trend-empty"
          role="status"
          className="text-sm text-muted-foreground"
        >
          {labels.empty}
        </p>
      ) : (
        <>
          <TrendChartSvg points={points} />
          <TrendAxis points={points} labels={labels} />
          {/* sr-only summary so AT users learn the trend without parsing the SVG */}
          <span data-slot="player-detail-trend-summary" className="sr-only">
            {interpolate(labels.trendSummaryAriaLabel, {
              from: Math.round((points[0]?.winRate ?? 0) * 100),
              to: Math.round((points[points.length - 1]?.winRate ?? 0) * 100),
              count: points.length,
            })}
          </span>
        </>
      )}
    </div>
  );
}

// ─── Delta badge ──────────────────────────────────────────────────────────────

function DeltaBadge({
  delta,
  labels,
}: {
  delta: DeltaInfo;
  labels: PlayerTrendCardLabels;
}): ReactElement {
  let template: string;
  let ariaTemplate: string;
  let colorClass: string;

  if (delta.direction === 'up') {
    template = labels.deltaUp;
    ariaTemplate = labels.deltaUpAriaLabel;
    colorClass = 'text-emerald-700 dark:text-emerald-400';
  } else if (delta.direction === 'down') {
    template = labels.deltaDown;
    ariaTemplate = labels.deltaDownAriaLabel;
    colorClass = 'text-rose-700 dark:text-rose-400';
  } else {
    template = labels.deltaFlat;
    ariaTemplate = labels.deltaFlatAriaLabel;
    colorClass = 'text-muted-foreground';
  }

  // For deltaUp the template wants the unsigned percent (it provides the "+");
  // for deltaDown the signed percent already carries the minus.
  const percentToken = delta.direction === 'down' ? delta.signedPercent : delta.absPercent;

  return (
    <span
      data-slot="player-detail-trend-delta"
      className={clsx('font-mono text-[10px] font-bold tabular-nums', colorClass)}
    >
      <span aria-hidden="true">{interpolate(template, { percent: percentToken })}</span>
      <span className="sr-only">{interpolate(ariaTemplate, { percent: delta.absPercent })}</span>
    </span>
  );
}

// ─── SVG line chart ───────────────────────────────────────────────────────────

function TrendChartSvg({ points }: { points: ReadonlyArray<MonthlyWinRatePoint> }): ReactElement {
  const coords = points.map((p, idx) => ({
    x: toSvgX(idx, points.length),
    y: toSvgY(p.winRate),
  }));

  const lineD = coords.map((c, idx) => `${idx === 0 ? 'M' : 'L'}${c.x},${c.y}`).join(' ');
  const areaD = `${lineD} L${SVG_VIEWBOX_WIDTH},${SVG_VIEWBOX_HEIGHT} L0,${SVG_VIEWBOX_HEIGHT} Z`;

  // Use a deterministic-but-unique gradient id so multiple cards on the page
  // don't collide (e.g. fixture playground or future "compare two players"
  // surfaces). React's `useId` would be cleaner; we keep this dependency-free.
  const gradientId = `player-trend-grad-${points.length}-${Math.round(coords[0]?.y ?? 0)}`;

  return (
    <div className="relative h-[110px] w-full">
      <svg
        viewBox={`0 0 ${SVG_VIEWBOX_WIDTH} ${SVG_VIEWBOX_HEIGHT}`}
        preserveAspectRatio="none"
        className="h-full w-full text-violet-700 dark:text-violet-400"
        aria-hidden="true"
        data-slot="player-detail-trend-svg"
      >
        <defs>
          <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="currentColor" stopOpacity="0.35" />
            <stop offset="100%" stopColor="currentColor" stopOpacity="0" />
          </linearGradient>
        </defs>
        <path d={areaD} fill={`url(#${gradientId})`} />
        <path
          d={lineD}
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        {coords.map((c, idx) => (
          <circle
            key={`${c.x.toFixed(2)}-${idx}`}
            cx={c.x}
            cy={c.y}
            r="3"
            fill="var(--card)"
            stroke="currentColor"
            strokeWidth="2"
          />
        ))}
      </svg>
    </div>
  );
}

// ─── Axis labels ──────────────────────────────────────────────────────────────

function TrendAxis({
  points,
  labels,
}: {
  points: ReadonlyArray<MonthlyWinRatePoint>;
  labels: PlayerTrendCardLabels;
}): ReactElement {
  return (
    <div
      aria-hidden="true"
      data-slot="player-detail-trend-axis"
      className="flex justify-between font-mono text-[9px] font-bold text-muted-foreground"
    >
      {points.map((p, idx) => {
        const monthNum = parseMonthNumber(p.month);
        const label = monthNum != null ? (labels.monthsShort[monthNum - 1] ?? p.month) : p.month;
        return <span key={`${p.month}-${idx}`}>{label}</span>;
      })}
    </div>
  );
}
