/**
 * Admin Monitor Hub — Page
 * Issue #5040 — Admin Route Consolidation
 */

'use client';

import { useSearchParams } from 'next/navigation';

import { Monitor } from 'lucide-react';

export default function AdminMonitorPage() {
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab') ?? 'alerts';

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Monitor className="h-6 w-6 text-primary" />
        <div>
          <h1 className="text-2xl font-bold font-quicksand text-foreground">
            Monitor
          </h1>
          <p className="text-sm text-muted-foreground">
            Infrastruttura, alert, cache e servizi
          </p>
        </div>
      </div>

      <div className="rounded-xl border border-border bg-card p-6 text-muted-foreground text-sm">
        <p>
          Tab: <strong>{tab}</strong>
          {searchParams.get('section') && <> · Sezione: <strong>{searchParams.get('section')}</strong></>}
        </p>
        <p className="mt-2 text-xs">Contenuto in migrazione — Phase 3 (issue #5051)</p>
      </div>
    </div>
  );
}
