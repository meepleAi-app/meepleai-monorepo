/**
 * Admin Config Hub — Page
 * Issue #5040 — Admin Route Consolidation
 */

'use client';

import { useSearchParams } from 'next/navigation';

import { Settings } from 'lucide-react';

export default function AdminConfigPage() {
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab') ?? 'general';

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Settings className="h-6 w-6 text-primary" />
        <div>
          <h1 className="text-2xl font-bold font-quicksand text-foreground">
            Configurazione
          </h1>
          <p className="text-sm text-muted-foreground">
            Configurazione sistema, limiti, feature flags e integrazioni
          </p>
        </div>
      </div>

      <div className="rounded-xl border border-border bg-card p-6 text-muted-foreground text-sm">
        <p>
          Tab: <strong>{tab}</strong>
          {searchParams.get('section') && <> · Sezione: <strong>{searchParams.get('section')}</strong></>}
        </p>
        <p className="mt-2 text-xs">Contenuto in migrazione — Phase 3 (issue #5050)</p>
      </div>
    </div>
  );
}
