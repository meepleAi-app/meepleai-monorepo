'use client';

import { KpiCards } from '@/components/admin/usage/KpiCards';

// UsageChart and CostBreakdownChart require recharts with real API data.
// We render KpiCards with mock data and show placeholders for charts.

const MOCK_STATUS = {
  balanceUsd: 18.64,
  dailySpendUsd: 0.0842,
  todayRequestCount: 127,
  currentRpm: 23,
  limitRpm: 60,
  utilizationPercent: 0.383,
  isThrottled: false,
  isFreeTier: false,
  rateLimitInterval: '1m',
  lastUpdated: new Date().toISOString(),
};

function ChartPlaceholder({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex h-48 flex-col items-center justify-center gap-2 rounded-xl border border-dashed border-border/50 bg-muted/20">
      <p className="font-quicksand text-sm font-semibold text-foreground">{title}</p>
      <p className="font-nunito text-xs text-muted-foreground">{description}</p>
      <span className="rounded-full border border-border/50 px-2.5 py-0.5 font-nunito text-[10px] text-muted-foreground">
        requires live API data
      </span>
    </div>
  );
}

export default function UsageMonitoringScene() {
  return (
    <div className="space-y-8">
      {/* KPI cards — fully rendered with mock data */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">KPI Cards</h3>
        <KpiCards status={MOCK_STATUS} isLoading={false} />
      </div>

      {/* Loading state */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          KPI Cards — Loading State
        </h3>
        <KpiCards status={null} isLoading={true} />
      </div>

      {/* Charts — require recharts with real data */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Charts (require live data)
        </h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <ChartPlaceholder
            title="UsageChart"
            description="Recharts line chart of requests, tokens, and spend over time with time range selector."
          />
          <ChartPlaceholder
            title="CostBreakdownChart"
            description="Pie/bar chart breaking down costs by model, strategy tier, and request source."
          />
        </div>
      </div>
    </div>
  );
}
