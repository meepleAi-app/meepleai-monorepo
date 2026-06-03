'use client';

/**
 * #1837 SP5 F4-C1 — `/admin/monitor/containers` re-skin
 *
 * Layout SP5 (mockup `sp5-admin-infra.html`):
 *   1. Hero header (titolo + descrizione + crumbs)
 *   2. ContainerDashboard — grid container con auto-refresh + StatusSummary KPI
 *   3. RestartAllPanel — restart dependency-ordered (typed-confirm)
 *
 * Originale (Issue #143): page admin Phase 4. Re-skin SP5 introduce:
 *   - Header SP5-style con breadcrumb-like crumbs
 *   - StatusSummary KPI con token semantici emerald/rose (era green/red hardcoded)
 *   - ContainerStatusBadge dot color token-aligned
 *
 * BE pending (documentato, follow-up issue da aprire):
 *   - KPISparkline per CPU/Memory/Network — richiede endpoint metrics aggregato
 *   - LiveEventLog filtrato `type=infra` — richiede event types specifici per
 *     container start/stop/restart (oggi LiveEventLog supporta solo eventi domain
 *     come `agent.created`, `kb.doc.indexed`, etc.)
 */

import { ContainerDashboard } from './ContainerDashboard';
import { RestartAllPanel } from './RestartAllPanel';

export default function ContainerDashboardPage() {
  return (
    <div data-testid="containers-page" className="space-y-6">
      {/* SP5 hero header */}
      <header>
        <nav
          aria-label="breadcrumb"
          className="font-mono text-[10.5px] uppercase tracking-wider text-muted-foreground mb-1"
        >
          Monitor &middot; Containers
        </nav>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Infrastructure &amp; Containers
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Docker container status, metrics, e azioni di restart dependency-ordered.
        </p>
      </header>

      <ContainerDashboard />

      {/* Issue #145: Restart All with dependency-ordered restart */}
      <RestartAllPanel />
    </div>
  );
}
