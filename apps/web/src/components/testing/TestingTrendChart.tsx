/**
 * TestingTrendChart - Historical trends for testing metrics
 * Issue #2139: Testing Dashboard Enhancements
 *
 * Displays time series data for:
 * - Accessibility scores over time
 * - Performance metrics (LCP, FID, CLS)
 * - E2E coverage trends
 *
 * Pattern: Reuses MetricsChart component with testing-specific configuration
 */
'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import dynamic from 'next/dynamic';
import { useState } from 'react';

// Dynamic import to avoid SSR issues
const MetricsChart = dynamic(
  () => import('@/components/metrics/MetricsChart').then(m => m.MetricsChart),
  {
    ssr: false,
    loading: () => <div className="h-[300px] w-full animate-pulse rounded-lg bg-slate-100" />,
  }
);

type TimeRange = '7d' | '14d' | '30d';

export type TrendDataPoint = {
  date: string;
  value: number;
  target?: number;
};

export interface TestingTrendChartProps {
  title: string;
  description?: string;
  data: TrendDataPoint[];
  dataKey: string;
  valueLabel: string;
  targetLabel?: string;
  color?: string;
  showTarget?: boolean;
  unit?: string;
}

export function TestingTrendChart({
  title,
  description,
  data,
  dataKey,
  valueLabel,
  targetLabel = 'Target',
  color = '#1a73e8',
  showTarget = true,
  unit: _unit = '',
}: TestingTrendChartProps) {
  const [timeRange, setTimeRange] = useState<TimeRange>('7d');

  // Filter data by time range
  const filteredData = data.slice(-(timeRange === '7d' ? 7 : timeRange === '14d' ? 14 : 30));

  const chartSeries = [
    {
      key: dataKey,
      name: valueLabel,
      color,
      strokeWidth: 2,
    },
    ...(showTarget
      ? [
          {
            key: 'target',
            name: targetLabel,
            color: '#9ca3af',
            strokeWidth: 1,
            strokeDasharray: '5 5',
          },
        ]
      : []),
  ];

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle>{title}</CardTitle>
            {description && <CardDescription>{description}</CardDescription>}
          </div>
          <Select value={timeRange} onValueChange={(v: TimeRange) => setTimeRange(v)}>
            <SelectTrigger className="w-32">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="7d">Last 7 days</SelectItem>
              <SelectItem value="14d">Last 14 days</SelectItem>
              <SelectItem value="30d">Last 30 days</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </CardHeader>
      <CardContent>
        <MetricsChart
          type="line"
          data={filteredData}
          series={chartSeries}
          xAxisKey="date"
          height={300}
          showGrid
          showLegend
          enableBrush={filteredData.length > 14}
        />
      </CardContent>
    </Card>
  );
}
