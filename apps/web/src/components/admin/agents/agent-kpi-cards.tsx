'use client';

import { TrendingUpIcon, TrendingDownIcon } from 'lucide-react';

interface KPI {
  label: string;
  value: string;
  trend: {
    direction: 'up' | 'down';
    value: string;
    label: string;
  };
}

const MOCK_KPIS: KPI[] = [
  {
    label: 'Total Queries',
    value: '14,832',
    trend: { direction: 'up', value: '12.3%', label: 'vs last week' },
  },
  {
    label: 'Avg. Response Time',
    value: '1.8s',
    trend: { direction: 'down', value: '0.3s', label: 'improvement' },
  },
  {
    label: 'Total Cost',
    value: '$247.50',
    trend: { direction: 'up', value: '8.1%', label: 'vs last week' },
  },
  {
    label: 'Satisfaction',
    value: '4.6/5.0',
    trend: { direction: 'up', value: '0.2', label: 'improvement' },
  },
];

export function AgentKPICards() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
      {MOCK_KPIS.map((kpi) => (
        <div
          key={kpi.label}
          className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40"
        >
          <div className="text-sm text-gray-600 dark:text-zinc-400 mb-1">{kpi.label}</div>
          <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">{kpi.value}</div>
          <div
            className={`text-xs mt-1 flex items-center gap-1 ${
              kpi.trend.direction === 'up'
                ? kpi.label === 'Total Cost'
                  ? 'text-amber-600 dark:text-amber-400'
                  : 'text-emerald-600 dark:text-emerald-400'
                : 'text-emerald-600 dark:text-emerald-400'
            }`}
          >
            {kpi.trend.direction === 'up' ? (
              <TrendingUpIcon className="w-3 h-3" />
            ) : (
              <TrendingDownIcon className="w-3 h-3" />
            )}
            {kpi.trend.value} {kpi.trend.label}
          </div>
        </div>
      ))}
    </div>
  );
}
