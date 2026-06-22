'use client';

/**
 * #1837 SP5 F4-C1 — `/admin/monitor/containers` re-skin (composition layer).
 *
 * Layout SP5 (mockup `sp5-admin-infra.html`):
 *   1. Hero header (titolo + crumbs)
 *   2. KPISparklineStrip — 4 KPI con sparkline (CPU/Memory inline)
 *   3. ContainerDashboard — grid container + auto-refresh + StatusSummary KPI
 *   4. LiveEventLog — feed SSE Container/Infrastructure con fade-out 30s
 *   5. RestartAllPanel — restart dependency-ordered (typed-confirm)
 *
 * SSE subscription è ora "lifted" a questa page: ContainerDashboard +
 * LiveEventLog ricevono `events` / `isStreaming` come prop e condividono
 * una singola EventSource (no doppia connessione SSE).
 *
 * Resolved follow-ups: #1853 (SSE wiring) · #1854 (typed-confirm dialog).
 * Open gap (out of scope, follow-up issue tracciato in spec):
 *   - Per-container CPU/Memory metrics (richiede estensione ContainerInfo BE)
 *   - Resources section (disco + memoria host breakdown)
 */

import { useLiveEvents } from '@/components/admin/monitor/use-live-events';

import { ContainerDashboard } from './ContainerDashboard';
import { KPISparklineStrip } from './KPISparklineStrip';
import { LiveEventLog } from './LiveEventLog';
import { RestartAllPanel } from './RestartAllPanel';

export default function ContainerDashboardPage() {
  const { events, isStreaming, isLoading } = useLiveEvents({
    aggregateTypes: ['Container', 'Infrastructure'],
    initialLimit: 50,
  });

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

      <KPISparklineStrip />

      <ContainerDashboard liveEvents={events} sseConnected={isStreaming} />

      <section aria-labelledby="live-events-heading" className="space-y-2">
        <h2
          id="live-events-heading"
          className="font-quicksand text-sm font-semibold text-foreground"
        >
          Eventi live
        </h2>
        <LiveEventLog events={events} isStreaming={isStreaming} isLoading={isLoading} />
      </section>

      {/* Issue #145: Restart All with dependency-ordered restart */}
      <RestartAllPanel />
    </div>
  );
}
