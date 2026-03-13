/**
 * Admin Grafana Dashboard Embed
 * Issue #134 — Embedded Grafana dashboards for infrastructure and application monitoring.
 *
 * Route: /admin/monitor/grafana
 */

import { GrafanaDashboard } from './GrafanaDashboard';

export default function GrafanaPage() {
  return (
    <div className="space-y-5" data-testid="grafana-page">
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Grafana Dashboards
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Embedded Grafana dashboards for infrastructure and application monitoring.
        </p>
      </div>
      <GrafanaDashboard />
    </div>
  );
}
