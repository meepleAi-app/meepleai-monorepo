import { CircuitBreakerGrid } from '@/components/admin/providers/CircuitBreakerGrid';
import { ProvidersHero } from '@/components/admin/providers/ProvidersHero';
import { ProviderTable } from '@/components/admin/providers/ProviderTable';
import { RoutingChainViz } from '@/components/admin/providers/RoutingChainViz';

import type { Metadata } from 'next';

export const metadata: Metadata = { title: 'Providers — Admin' };

/**
 * #1834 SP5 F4-C3 — `/admin/providers` re-skin
 *
 * Layout SP5 (mockup `sp5-admin-providers.html`):
 *   1. ProvidersHero        — KPI strip (4 metriche)
 *   2. ProviderTable        — lista tabellare con actions
 *   3. RoutingChainViz      — fallback chain visualization
 *   4. CircuitBreakerGrid   — stato Polly per servizio
 *
 * Drill-down `/admin/providers/[name]` resta separato (probe + quota dettaglio).
 */
export default function ProvidersPage() {
  return (
    <div className="space-y-5">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          LLM Providers &amp; Circuit Breakers
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Stato token, quota residua, catena di fallback e circuit breaker per ogni provider
          configurato.
        </p>
      </div>

      <ProvidersHero />
      <ProviderTable />
      <RoutingChainViz />
      <CircuitBreakerGrid />
    </div>
  );
}
