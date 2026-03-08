'use client';

import { ChartsSection } from '@/components/admin/charts/ChartsSection';

export function OverviewTab() {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Analytics Overview
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          High-level usage charts and platform metrics.
        </p>
      </div>
      <ChartsSection />
    </div>
  );
}
