type EndpointChartProps = {
  endpointCounts: Record<string, number>;
};

type LatencyChartProps = {
  requests: Array<{ latencyMs: number; endpoint: string; createdAt: string }>;
};

type TimeSeriesChartProps = {
  requests: Array<{ createdAt: string; status: string }>;
};

type FeedbackChartProps = {
  feedbackCounts: Record<string, number>;
};

const ENDPOINT_COLORS: Record<string, string> = {
  qa: '#1a73e8',
  explain: '#f9ab00',
  setup: '#a142f4',
  chess: '#34a853',
};

const COLORS = ['#1a73e8', '#f9ab00', '#a142f4', '#34a853', '#ea4335'];

const ChartSkeleton = () => (
  <div className="h-[300px] w-full animate-pulse rounded-lg bg-slate-100 dark:bg-slate-800" />
);

export function buildEndpointData(endpointCounts: Record<string, number>) {
  return Object.entries(endpointCounts).map(([name, value]) => {
    let color = '#64748b';
    switch (name) {
      case 'qa':
        color = ENDPOINT_COLORS.qa;
        break;
      case 'explain':
        color = ENDPOINT_COLORS.explain;
        break;
      case 'setup':
        color = ENDPOINT_COLORS.setup;
        break;
      case 'chess':
        color = ENDPOINT_COLORS.chess;
        break;
      default:
        color = '#64748b';
    }
    return { name, value, color };
  });
}

export function buildLatencyBins(requests: Array<{ latencyMs: number }>) {
  const bins = [0, 100, 200, 300, 400, 500, 1000];
  return bins.slice(0, -1).map((bin, i) => {
    const nextBin = bins[i + 1];
    const count = requests.filter(r => r.latencyMs >= bin && r.latencyMs < nextBin).length;
    return {
      range: `${bin}-${nextBin}ms`,
      count,
    };
  });
}

export function buildTimeSeries(requests: Array<{ createdAt: string; status: string }>) {
  const grouped = requests.reduce((acc, req) => {
    const date = new Date(req.createdAt);
    const hour = new Date(
      date.getFullYear(),
      date.getMonth(),
      date.getDate(),
      date.getHours()
    ).toISOString();

    if (!acc.has(hour)) {
      acc.set(hour, { time: hour, success: 0, error: 0, total: 0 });
    }

    const entry = acc.get(hour)!;
    entry.total++;
    if (req.status === 'Success') {
      entry.success++;
    } else {
      entry.error++;
    }

    return acc;
  }, new Map<string, { time: string; success: number; error: number; total: number }>());

  return Array.from(grouped.values())
    .sort((a, b) => new Date(a.time).getTime() - new Date(b.time).getTime())
    .map(item => ({
      ...item,
      time: new Date(item.time).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    }));
}

export function buildFeedbackData(feedbackCounts: Record<string, number>) {
  return Object.entries(feedbackCounts).map(([name, value]) => ({
    name: name === 'helpful' ? 'Helpful' : 'Not Helpful',
    value,
    color: name === 'helpful' ? '#34a853' : '#ea4335',
  }));
}

export function EndpointDistributionChart({ endpointCounts }: EndpointChartProps) {
  const data = buildEndpointData(endpointCounts);

  if (data.length === 0) {
    return <div className="p-12 text-center text-gray-500">No endpoint data available</div>;
  }

  return (
    <Suspense fallback={<ChartSkeleton />}>
      <ResponsiveContainer width="100%" height={300}>
        <PieChart>
          <Pie
            data={data}
            dataKey="value"
            nameKey="name"
            cx="50%"
            cy="50%"
            outerRadius={100}
            label={({ name, value }: { name: string; value: number }) => `${name}: ${value}`}
          >
            {data.map((entry, index) => (
              <Cell key={`cell-${index}`} fill={entry.color} />
            ))}
          </Pie>
          <Tooltip />
          <Legend />
        </PieChart>
      </ResponsiveContainer>
    </Suspense>
  );
}

export function LatencyDistributionChart({ requests }: LatencyChartProps) {
  if (requests.length === 0) {
    return <div className="p-12 text-center text-gray-500">No latency data available</div>;
  }

  const binCounts = buildLatencyBins(requests);

  return (
    <Suspense fallback={<ChartSkeleton />}>
      <ResponsiveContainer width="100%" height={300}>
        <BarChart data={binCounts}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="range" />
          <YAxis />
          <Tooltip />
          <Legend />
          <Bar dataKey="count" fill="#1a73e8" name="Requests" />
        </BarChart>
      </ResponsiveContainer>
    </Suspense>
  );
}

export function RequestsTimeSeriesChart({ requests }: TimeSeriesChartProps) {
  if (requests.length === 0) {
    return <div className="p-12 text-center text-gray-500">No time series data available</div>;
  }

  const data = buildTimeSeries(requests);

  return (
    <Suspense fallback={<ChartSkeleton />}>
      <ResponsiveContainer width="100%" height={300}>
        <LineChart data={data}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="time" />
          <YAxis />
          <Tooltip />
          <Legend />
          <Line
            type="monotone"
            dataKey="total"
            stroke="#1a73e8"
            name="Total Requests"
            strokeWidth={2}
          />
          <Line
            type="monotone"
            dataKey="success"
            stroke="#0f9d58"
            name="Successful"
            strokeWidth={2}
          />
          <Line type="monotone" dataKey="error" stroke="#d93025" name="Errors" strokeWidth={2} />
        </LineChart>
      </ResponsiveContainer>
    </Suspense>
  );
}

export function FeedbackChart({ feedbackCounts }: FeedbackChartProps) {
  const data = buildFeedbackData(feedbackCounts);

  if (data.length === 0 || data.every(d => d.value === 0)) {
    return <div className="p-12 text-center text-gray-500">No feedback data available</div>;
  }

  return (
    <Suspense fallback={<ChartSkeleton />}>
      <ResponsiveContainer width="100%" height={300}>
        <BarChart data={data}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="name" />
          <YAxis />
          <Tooltip />
          <Legend />
          <Bar dataKey="value" name="Feedback Count">
            {data.map((entry, index) => (
              <Cell key={`cell-${index}`} fill={entry.color} />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </Suspense>
  );
}

/**
 * Admin charts (Recharts) - dynamically loaded to avoid SSR/bundle bloat.
 */

import dynamic from 'next/dynamic';
import { Suspense } from 'react';

const isTest = process.env.NODE_ENV === 'test';
const recharts = isTest ? require('recharts') : null;

const ResponsiveContainer = isTest
  ? recharts.ResponsiveContainer
  : dynamic(() => import('recharts').then(m => m.ResponsiveContainer), { ssr: false });
const PieChart = isTest
  ? recharts.PieChart
  : dynamic(() => import('recharts').then(m => m.PieChart), { ssr: false });
const Pie = isTest
  ? recharts.Pie
  : dynamic(() => import('recharts').then(m => m.Pie), { ssr: false });
const Cell = isTest
  ? recharts.Cell
  : dynamic(() => import('recharts').then(m => m.Cell), { ssr: false });
const Tooltip = isTest
  ? recharts.Tooltip
  : dynamic(() => import('recharts').then(m => m.Tooltip), { ssr: false });
const Legend = isTest
  ? recharts.Legend
  : dynamic(() => import('recharts').then(m => m.Legend), { ssr: false });

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
const Bar = isTest
  ? recharts.Bar
  : dynamic(() => import('recharts').then(m => m.Bar), { ssr: false });

const LineChart = isTest
  ? recharts.LineChart
  : dynamic(() => import('recharts').then(m => m.LineChart), { ssr: false });
const Line = isTest
  ? recharts.Line
  : dynamic(() => import('recharts').then(m => m.Line), { ssr: false });
