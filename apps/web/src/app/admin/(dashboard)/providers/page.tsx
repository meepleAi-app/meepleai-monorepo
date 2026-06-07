import { CircuitBreakerGrid } from '@/components/admin/providers/CircuitBreakerGrid';
import { ProvidersHero } from '@/components/admin/providers/ProvidersHero';
import { ProvidersToolbar } from '@/components/admin/providers/ProvidersToolbar';
import { ProviderTable } from '@/components/admin/providers/ProviderTable';
import { RoutingChainViz } from '@/components/admin/providers/RoutingChainViz';

import type { Metadata } from 'next';

export const metadata: Metadata = { title: 'Providers — Admin' };

/**
 * #1834 SP5 F4-C3 — `/admin/providers` re-skin
 *
 * Layout SP5 (mockup `sp5-admin-providers.html`):
 *   1. ProvidersToolbar     — header con refresh
 *   2. ProvidersHero        — KPI strip (2 metriche reali, mockup §1)
 *   3. ProviderTable        — lista tabellare con entity colors + primary tag
 *   4. RoutingChainViz      — fallback chain visualization
 *   5. CircuitBreakerGrid   — stato Polly per servizio
 *
 * Drill-down `/admin/providers/[name]` resta separato (probe + quota dettaglio).
 */
export default function ProvidersPage() {
  return (
    <div className="space-y-5">
      <ProvidersToolbar />
      <ProvidersHero />
      <ProviderTable />
      <RoutingChainViz />
      <CircuitBreakerGrid />
    </div>
  );
}
