'use client';

/**
 * #1834 SP5 F4-C3 — ProvidersToolbar
 *
 * Header bar per `/admin/providers` (mockup `sp5-admin-providers.html` top bar).
 *
 * PR1 scope minimale: titolo + sottotitolo + refresh button che invalida le query.
 * Search (⌘K) + config globale + theme toggle restano out-of-scope per PR1.
 */

import { useState } from 'react';

import { useQueryClient } from '@tanstack/react-query';

import { providerKeys } from '@/hooks/queries/useProviders';

export function ProvidersToolbar() {
  const qc = useQueryClient();
  const [refreshing, setRefreshing] = useState(false);

  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      await Promise.all([
        qc.invalidateQueries({ queryKey: providerKeys.circuitBreakers }),
        qc.invalidateQueries({ queryKey: providerKeys.llmConfig }),
        qc.invalidateQueries({ queryKey: providerKeys.all }),
      ]);
    } finally {
      setRefreshing(false);
    }
  };

  return (
    <header className="flex items-start justify-between gap-3" data-testid="providers-toolbar">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          LLM Providers &amp; Circuit Breakers
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Stato token, quota residua, catena di fallback e circuit breaker per ogni provider
          configurato.
        </p>
      </div>
      <button
        type="button"
        onClick={handleRefresh}
        disabled={refreshing}
        aria-label="Aggiorna stato provider"
        data-testid="providers-refresh"
        className="inline-flex items-center gap-1.5 rounded-md border border-border/60 bg-card/60 px-3 py-1.5 text-xs font-medium text-foreground hover:bg-muted/70 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        <span aria-hidden="true">{refreshing ? '⟳' : '⟳'}</span>
        {refreshing ? 'Aggiornamento…' : 'Refresh'}
      </button>
    </header>
  );
}
