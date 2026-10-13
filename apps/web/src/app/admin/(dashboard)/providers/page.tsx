import { CircuitBreakerGrid } from '@/components/admin/providers/CircuitBreakerGrid';
import { ProviderQuotaSummary } from '@/components/admin/providers/ProviderQuotaSummary';
import { ProvidersHero } from '@/components/admin/providers/ProvidersHero';
import { ProvidersToolbar } from '@/components/admin/providers/ProvidersToolbar';
import { ProviderTable } from '@/components/admin/providers/ProviderTable';

import type { Metadata } from 'next';

export const metadata: Metadata = { title: 'Providers — Admin' };

/**
 * #1834 SP5 F4-C3 — `/admin/providers` re-skin
 *
 * Layout SP5 (mockup `sp5-admin-providers.html`):
 *   1. ProvidersToolbar     — header con refresh
 *   2. ProvidersHero        — KPI strip (2 metriche reali, mockup §1)
 *   3. ProviderTable        — lista tabellare con entity colors + primary tag
 *   4. CircuitBreakerGrid   — stato Polly per servizio
 *
 * #4169: RoutingChainViz è uscita. Disegnava `fallbackChainJson`, che il runtime non legge: l'ordine
 * di fallback vero viene dal catalogo modelli (LlmProviderSelector.BuildFallbackOrderAsync).
 *
 * Drill-down `/admin/providers/[name]` resta separato (probe + quota dettaglio).
 */
export default function ProvidersPage() {
  return (
    <div className="space-y-5">
      <ProvidersToolbar />
      <ProvidersHero />
      <ProviderQuotaSummary />
      <ProviderTable />
      <CircuitBreakerGrid />
    </div>
  );
}
