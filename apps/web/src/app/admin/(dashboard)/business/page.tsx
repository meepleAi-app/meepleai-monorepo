import { BudgetKpiStrip } from '@/components/admin/business/BudgetKpiStrip';
import { BudgetPlaceholderPanel } from '@/components/admin/business/BudgetPlaceholderPanel';

import type { Metadata } from 'next';

export const metadata: Metadata = { title: 'Budget & Cost — Admin' };

/**
 * #1838 SP5 F4-C5 — `/admin/business` (NEW page)
 *
 * Layout SP5 (mockup `sp5-admin-budget.html`):
 *   1. Hero header (titolo + crumbs + descrizione)
 *   2. BudgetKpiStrip — 4 KPI (spesa oggi/mese, budget residuo, proiezione)
 *   3. CostStackedArea placeholder — stacked area cost per provider 30gg
 *   4. FeatureCostTable placeholder — costi per feature con drill provider
 *   5. CostSimulator placeholder — what-if calculator
 *   6. BudgetGauge placeholder — gauge spent vs budget mensile
 *
 * **Stato attuale**: page nuova creata + nav config aggiornata. Tutti i 4
 * pannelli sono placeholder "BE pending" perché gli endpoint cost aggregation
 * non sono ancora implementati nel backend.
 *
 * Endpoint BE attesi (follow-up issue da aprire):
 *   GET /api/v1/admin/business               → KPI aggregati
 *   GET /api/v1/admin/budget                 → budget mensile + spent
 *   GET /api/v1/admin/cost-calculator        → simulator input/output
 *   POST /api/v1/admin/budget/limit          → update budget limit
 *   GET /api/v1/admin/business/breakdown?range=30d → CostStackedArea data
 *   GET /api/v1/admin/business/per-feature   → FeatureCostTable
 */
export default function BudgetPage() {
  return (
    <div className="space-y-5" data-testid="business-page">
      {/* SP5 hero header */}
      <header>
        <nav
          aria-label="breadcrumb"
          className="font-mono text-[10.5px] uppercase tracking-wider text-muted-foreground mb-1"
        >
          Admin &middot; Platform &amp; Operations &middot; Budget
        </nav>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Budget &amp; Cost
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          KPI di spesa per provider/feature, simulator di costo, e configurazione budget mensile.
        </p>
      </header>

      <BudgetKpiStrip />

      <BudgetPlaceholderPanel
        id="cost-stacked-area"
        title="Costi per provider · 30 giorni"
        description="Stacked area chart con breakdown costo giornaliero per provider (DeepSeek, OpenRouter, OpenAI, Anthropic, ...) — last 30 days con linea budget cap."
        endpoint="GET /api/v1/admin/business/breakdown?range=30d"
      />

      <BudgetPlaceholderPanel
        id="feature-cost-table"
        title="Costi per feature"
        description="Tabella per-feature (rag-query, embedding, image-gen, ...) con cost totale + breakdown per provider drillabile."
        endpoint="GET /api/v1/admin/business/per-feature"
      />

      <BudgetPlaceholderPanel
        id="cost-simulator"
        title="What-if calculator"
        description="Simulator: 'se aumento RPM del X% e cambio modello a Y, costo previsto = Z'. Output con warning quando supera budget."
        endpoint="GET /api/v1/admin/cost-calculator"
      />

      <BudgetPlaceholderPanel
        id="budget-gauge"
        title="Budget mensile"
        description="Gauge spent vs budget mensile + ETA exhaust + threshold alert (80% / 95% / 100%). Configurazione via POST /admin/budget/limit."
        endpoint="GET /api/v1/admin/budget"
      />
    </div>
  );
}
