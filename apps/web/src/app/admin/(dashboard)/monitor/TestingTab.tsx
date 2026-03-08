'use client';

import { useState, useEffect } from 'react';

import { api } from '@/lib/api';
import type { AccessibilityMetrics } from '@/lib/api/schemas/testing.schemas';
import type { E2EMetrics } from '@/lib/api/schemas/testing.schemas';
import type { PerformanceMetrics } from '@/lib/api/schemas/testing.schemas';
import { cn } from '@/lib/utils';

interface TestingMetricsState {
  accessibility: AccessibilityMetrics | null;
  performance: PerformanceMetrics | null;
  e2e: E2EMetrics | null;
}

function MetricValue({
  label,
  value,
  unit,
}: {
  label: string;
  value: string | number;
  unit?: string;
}) {
  return (
    <div className="flex items-baseline justify-between py-1">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className="font-quicksand text-sm font-semibold text-foreground">
        {value}
        {unit && <span className="ml-0.5 text-xs font-normal text-muted-foreground">{unit}</span>}
      </span>
    </div>
  );
}

function AccessibilitySection({ data }: { data: AccessibilityMetrics | null }) {
  if (!data) {
    return (
      <div
        className={cn(
          'rounded-xl border border-border/60 p-5',
          'bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md'
        )}
      >
        <h3 className="font-quicksand text-sm font-semibold text-foreground">Accessibility</h3>
        <p className="mt-2 text-sm text-muted-foreground">No data available.</p>
      </div>
    );
  }

  return (
    <div
      className={cn(
        'rounded-xl border border-border/60 p-5',
        'bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md'
      )}
    >
      <h3 className="font-quicksand text-sm font-semibold text-foreground">Accessibility</h3>
      <div className="mt-3 space-y-1">
        <MetricValue label="Lighthouse Score" value={data.lighthouseScore} />
        <MetricValue label="Axe Violations" value={data.axeViolations} />
        <MetricValue label="WCAG A" value={`${data.wcagCompliance.levelA}%`} />
        <MetricValue label="WCAG AA" value={`${data.wcagCompliance.levelAA}%`} />
        <MetricValue label="Pages Tested" value={data.testedPages} />
      </div>
    </div>
  );
}

function PerformanceSection({ data }: { data: PerformanceMetrics | null }) {
  if (!data) {
    return (
      <div
        className={cn(
          'rounded-xl border border-border/60 p-5',
          'bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md'
        )}
      >
        <h3 className="font-quicksand text-sm font-semibold text-foreground">Performance</h3>
        <p className="mt-2 text-sm text-muted-foreground">No data available.</p>
      </div>
    );
  }

  return (
    <div
      className={cn(
        'rounded-xl border border-border/60 p-5',
        'bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md'
      )}
    >
      <h3 className="font-quicksand text-sm font-semibold text-foreground">Performance</h3>
      <div className="mt-3 space-y-1">
        <MetricValue label="Lighthouse Score" value={data.lighthouseScore} />
        <MetricValue label="LCP" value={data.coreWebVitals.lcp.toFixed(2)} unit="s" />
        <MetricValue label="FID" value={data.coreWebVitals.fid} unit="ms" />
        <MetricValue label="CLS" value={data.coreWebVitals.cls.toFixed(3)} />
        <MetricValue
          label="JS Budget"
          value={`${data.budgetStatus.js.current}/${data.budgetStatus.js.budget}`}
          unit={data.budgetStatus.js.unit}
        />
      </div>
    </div>
  );
}

function E2ESection({ data }: { data: E2EMetrics | null }) {
  if (!data) {
    return (
      <div
        className={cn(
          'rounded-xl border border-border/60 p-5',
          'bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md'
        )}
      >
        <h3 className="font-quicksand text-sm font-semibold text-foreground">E2E</h3>
        <p className="mt-2 text-sm text-muted-foreground">No data available.</p>
      </div>
    );
  }

  return (
    <div
      className={cn(
        'rounded-xl border border-border/60 p-5',
        'bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md'
      )}
    >
      <h3 className="font-quicksand text-sm font-semibold text-foreground">E2E</h3>
      <div className="mt-3 space-y-1">
        <MetricValue label="Coverage" value={`${data.coverage}%`} />
        <MetricValue label="Pass Rate" value={`${data.passRate}%`} />
        <MetricValue label="Flaky Rate" value={`${data.flakyRate}%`} />
        <MetricValue label="Total Tests" value={data.totalTests} />
        <MetricValue label="Execution Time" value={data.executionTime} unit="min" />
      </div>
    </div>
  );
}

export function TestingTab() {
  const [metrics, setMetrics] = useState<TestingMetricsState>({
    accessibility: null,
    performance: null,
    e2e: null,
  });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      api.admin.getAccessibilityMetrics().catch(() => null),
      api.admin.getPerformanceMetrics().catch(() => null),
      api.admin.getE2EMetrics().catch(() => null),
    ]).then(([accessibility, performance, e2e]) => {
      setMetrics({
        accessibility: accessibility as AccessibilityMetrics | null,
        performance: performance as PerformanceMetrics | null,
        e2e: e2e as E2EMetrics | null,
      });
      setLoading(false);
    });
  }, []);

  if (loading) {
    return (
      <div className="space-y-6 animate-pulse">
        <div className="h-8 w-48 rounded bg-muted/50" />
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-48 rounded-xl bg-muted/50" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Testing Metrics
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Accessibility, performance, and E2E test results.
        </p>
      </div>
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <AccessibilitySection data={metrics.accessibility} />
        <PerformanceSection data={metrics.performance} />
        <E2ESection data={metrics.e2e} />
      </div>
    </div>
  );
}
