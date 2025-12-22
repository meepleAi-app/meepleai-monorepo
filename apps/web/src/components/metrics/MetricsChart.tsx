/**
 * MetricsChart - Generic time-series chart component for metrics visualization
 *
 * Features:
 * - Multiple chart types (line, area, bar)
 * - Responsive design
 * - Interactive tooltips and legend
 * - Zoom/pan support with Brush
 * - Loading skeleton
 * - Dark mode support
 * - Empty state handling
 *
 * @see Issue #898
 */
'use client';

import { Suspense } from 'react';

import dynamic from 'next/dynamic';

// Dynamic imports to avoid SSR/bundle bloat (follows AdminCharts pattern)
const isTest = process.env.NODE_ENV === 'test';
const recharts = isTest ? require('recharts') : null;

const ResponsiveContainer = isTest
  ? recharts.ResponsiveContainer
  : dynamic(() => import('recharts').then(m => m.ResponsiveContainer), { ssr: false });
const LineChart = isTest
  ? recharts.LineChart
  : dynamic(() => import('recharts').then(m => m.LineChart), { ssr: false });
const AreaChart = isTest
  ? recharts.AreaChart
  : dynamic(() => import('recharts').then(m => m.AreaChart), { ssr: false });
const BarChart = isTest
  ? recharts.BarChart
  : dynamic(() => import('recharts').then(m => m.BarChart), { ssr: false });
const CartesianGrid = isTest
  ? recharts.CartesianGrid
  : dynamic(() => import('recharts').then(m => m.CartesianGrid), { ssr: false });
const XAxis = isTest
  ? recharts.XAxis
  : dynamic(() => import('recharts').then(m => m.XAxis), { ssr: false });
const YAxis = isTest
  ? recharts.YAxis
  : dynamic(() => import('recharts').then(m => m.YAxis), { ssr: false });
const Tooltip = isTest
  ? recharts.Tooltip
  : dynamic(() => import('recharts').then(m => m.Tooltip), { ssr: false });
const Legend = isTest
  ? recharts.Legend
  : dynamic(() => import('recharts').then(m => m.Legend), { ssr: false });
const Line = isTest
  ? recharts.Line
  : dynamic(() => import('recharts').then(m => m.Line), { ssr: false });
const Area = isTest
  ? recharts.Area
  : dynamic(() => import('recharts').then(m => m.Area), { ssr: false });
const Bar = isTest
  ? recharts.Bar
  : dynamic(() => import('recharts').then(m => m.Bar), { ssr: false });
const Brush = isTest
  ? recharts.Brush
  : dynamic(() => import('recharts').then(m => m.Brush), { ssr: false });

// Types
export type ChartType = 'line' | 'area' | 'bar';

export type DataSeries = {
  key: string;
  name: string;
  color?: string;
  strokeWidth?: number;
  fillOpacity?: number;
};

export type DataPoint = {
  [key: string]: string | number;
};

export interface MetricsChartProps {
  /** Chart type */
  type?: ChartType;
  /** Data points array */
  data: DataPoint[];
  /** X-axis key in data */
  xAxisKey: string;
  /** Data series configuration */
  series: DataSeries[];
  /** Chart height in pixels */
  height?: number;
  /** Show grid */
  showGrid?: boolean;
  /** Show tooltip */
  showTooltip?: boolean;
  /** Show legend */
  showLegend?: boolean;
  /** Enable zoom/pan with brush */
  enableBrush?: boolean;
  /** Loading state */
  loading?: boolean;
  /** Empty state message */
  emptyMessage?: string;
  /** X-axis label */
  xAxisLabel?: string;
  /** Y-axis label */
  yAxisLabel?: string;
  /** Gradient fill for area charts */
  useGradient?: boolean;
}

// Loading skeleton
const ChartSkeleton = ({ height = 300 }: { height?: number }) => (
  <div
    className="w-full animate-pulse rounded-lg bg-slate-100 dark:bg-slate-800"
    style={{ height: `${height}px` }}
    role="status"
    aria-label="Loading chart"
  />
);

// Empty state
const EmptyChart = ({ message, height = 300 }: { message: string; height?: number }) => (
  <div
    className="flex w-full items-center justify-center rounded-lg border border-slate-200 bg-slate-50 dark:border-slate-700 dark:bg-slate-900"
    style={{ height: `${height}px` }}
  >
    <p className="text-slate-500 dark:text-slate-400">{message}</p>
  </div>
);

// Default colors (MeepleAI palette)
const DEFAULT_COLORS = [
  '#1a73e8', // Blue
  '#34a853', // Green
  '#f9ab00', // Orange
  '#a142f4', // Purple
  '#ea4335', // Red
];

export function MetricsChart({
  type = 'line',
  data,
  xAxisKey,
  series,
  height = 300,
  showGrid = true,
  showTooltip = true,
  showLegend = true,
  enableBrush = false,
  loading = false,
  emptyMessage = 'No data available',
  xAxisLabel,
  yAxisLabel,
  useGradient = false,
}: MetricsChartProps) {
  // Loading state
  if (loading) {
    return <ChartSkeleton height={height} />;
  }

  // Empty state
  if (!data || data.length === 0) {
    return <EmptyChart message={emptyMessage} height={height} />;
  }

  // Assign default colors to series if not provided
  const enrichedSeries = series.map((s, i) => ({
    ...s,
    color: s.color || DEFAULT_COLORS[i % DEFAULT_COLORS.length],
    strokeWidth: s.strokeWidth || 2,
    fillOpacity: s.fillOpacity || 0.6,
  }));

  // Common chart props
  const commonProps = {
    data,
    margin: { top: 10, right: 30, left: 0, bottom: 0 },
  };

  // Common axis props
  const xAxisProps = {
    dataKey: xAxisKey,
    label: xAxisLabel ? { value: xAxisLabel, position: 'insideBottom', offset: -5 } : undefined,
  };

  const yAxisProps = {
    label: yAxisLabel ? { value: yAxisLabel, angle: -90, position: 'insideLeft' } : undefined,
  };

  // Render chart based on type
  const renderChart = () => {
    switch (type) {
      case 'area':
        return (
          <AreaChart {...commonProps}>
            {showGrid && <CartesianGrid strokeDasharray="3 3" />}
            <XAxis {...xAxisProps} />
            <YAxis {...yAxisProps} />
            {showTooltip && <Tooltip />}
            {showLegend && <Legend />}
            {enrichedSeries.map(s => (
              <Area
                key={s.key}
                type="monotone"
                dataKey={s.key}
                name={s.name}
                stroke={s.color}
                fill={useGradient ? `url(#gradient-${s.key})` : s.color}
                fillOpacity={s.fillOpacity}
                strokeWidth={s.strokeWidth}
              />
            ))}
            {enableBrush && <Brush dataKey={xAxisKey} height={30} stroke="#1a73e8" />}
          </AreaChart>
        );

      case 'bar':
        return (
          <BarChart {...commonProps}>
            {showGrid && <CartesianGrid strokeDasharray="3 3" />}
            <XAxis {...xAxisProps} />
            <YAxis {...yAxisProps} />
            {showTooltip && <Tooltip />}
            {showLegend && <Legend />}
            {enrichedSeries.map(s => (
              <Bar key={s.key} dataKey={s.key} name={s.name} fill={s.color} />
            ))}
            {enableBrush && <Brush dataKey={xAxisKey} height={30} stroke="#1a73e8" />}
          </BarChart>
        );

      case 'line':
      default:
        return (
          <LineChart {...commonProps}>
            {showGrid && <CartesianGrid strokeDasharray="3 3" />}
            <XAxis {...xAxisProps} />
            <YAxis {...yAxisProps} />
            {showTooltip && <Tooltip />}
            {showLegend && <Legend />}
            {enrichedSeries.map(s => (
              <Line
                key={s.key}
                type="monotone"
                dataKey={s.key}
                name={s.name}
                stroke={s.color}
                strokeWidth={s.strokeWidth}
                dot={{ r: 4 }}
                activeDot={{ r: 6 }}
              />
            ))}
            {enableBrush && <Brush dataKey={xAxisKey} height={30} stroke="#1a73e8" />}
          </LineChart>
        );
    }
  };

  return (
    <Suspense fallback={<ChartSkeleton height={height} />}>
      <ResponsiveContainer width="100%" height={height}>
        {renderChart()}
      </ResponsiveContainer>
    </Suspense>
  );
}
