'use client';

import { EmptyFeatureState } from '@/components/admin/EmptyFeatureState';

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

      <EmptyFeatureState
        title="Generazione Report"
        description="La generazione di report non è ancora disponibile."
        issueNumber={920}
      />
    </div>
  );
}
