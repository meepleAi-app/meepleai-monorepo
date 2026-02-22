/**
 * Admin AI Hub — Page
 * Issue #5040 — Admin Route Consolidation
 *
 * Tab content rendered based on ?tab= search param.
 * Existing page components are imported as tab panels.
 * Phase 3 (issues #5048-#5053) will complete the tab content.
 */

'use client';

import { useSearchParams } from 'next/navigation';

import { Cpu } from 'lucide-react';

// ─── Admin AI Hub ─────────────────────────────────────────────────────────────

export default function AdminAiPage() {
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab') ?? 'agents';

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Cpu className="h-6 w-6 text-primary" />
        <div>
          <h1 className="text-2xl font-bold font-quicksand text-foreground">
            AI & Agenti
          </h1>
          <p className="text-sm text-muted-foreground">
            Lab AI, modelli, prompt e configurazione agenti
          </p>
        </div>
      </div>

      {/* Tab content placeholder — will be completed in Phase 3 issues #5048-#5053 */}
      <AdminAiTabContent tab={tab} section={searchParams.get('section')} />
    </div>
  );
}

function AdminAiTabContent({ tab, section }: { tab: string; section: string | null }) {
  // Each tab will be replaced with the actual migrated page component in Phase 3
  return (
    <div className="rounded-xl border border-border bg-card p-6 text-muted-foreground text-sm">
      <p>
        Tab: <strong>{tab}</strong>
        {section && <> · Sezione: <strong>{section}</strong></>}
      </p>
      <p className="mt-2 text-xs">
        Contenuto in migrazione — Phase 3 (issue #5048)
      </p>
    </div>
  );
}
