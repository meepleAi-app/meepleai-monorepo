'use client';

import { FileBarChart, DollarSign, Activity } from 'lucide-react';

const REPORT_TYPES = [
  {
    title: 'Usage Report',
    description:
      'Platform usage statistics including active users, sessions, and feature adoption metrics.',
    icon: FileBarChart,
  },
  {
    title: 'Cost Report',
    description:
      'AI model costs, token consumption, and infrastructure expenses broken down by service.',
    icon: DollarSign,
  },
  {
    title: 'Performance Report',
    description:
      'API response times, error rates, cache hit ratios, and system throughput analysis.',
    icon: Activity,
  },
] as const;

export function ReportsTab() {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Reports
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Generate and download platform reports.
        </p>
      </div>

      <div className="grid gap-6 md:grid-cols-3">
        {REPORT_TYPES.map(report => {
          const Icon = report.icon;
          return (
            <div
              key={report.title}
              className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-6"
            >
              <div className="flex items-center gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-amber-100 dark:bg-amber-900/30">
                  <Icon className="h-5 w-5 text-amber-700 dark:text-amber-400" aria-hidden="true" />
                </div>
                <h3 className="font-quicksand text-base font-semibold text-foreground">
                  {report.title}
                </h3>
              </div>
              <p className="mt-3 text-sm text-muted-foreground">{report.description}</p>
              <p className="mt-4 text-xs text-muted-foreground italic">
                Report generation coming soon.
              </p>
            </div>
          );
        })}
      </div>
    </div>
  );
}
