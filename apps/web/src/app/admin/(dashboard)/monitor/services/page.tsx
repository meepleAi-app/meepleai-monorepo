/**
 * Admin Service Dashboard
 * Issue #132 — Phase 2: Service Dashboard with health, DB stats, restart controls
 *
 * Route: /admin/monitor/services
 * Enhanced service health view with auto-refresh, uptime badges,
 * response time trends, database stats, and service restart controls.
 */

import { SecretsPanel } from '@/components/admin/secrets/SecretsPanel';

import { CircuitBreakerPanel } from './CircuitBreakerPanel';
import { DbStatsPanel } from './DbStatsPanel';
import { RestartServicePanel } from './RestartServicePanel';
import { ServicesDashboard } from './ServicesDashboard';

export default function ServiceDashboardPage() {
  return (
    <div className="space-y-5" data-testid="services-page">
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Service Dashboard
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Service health monitoring with auto-refresh, uptime tracking, and response time trends.
        </p>
      </div>

      <ServicesDashboard />

      {/* Issue #135: Database Stats Overview */}
      <DbStatsPanel />

      {/* Issue #133: Service Restart Controls (SuperAdmin only) */}
      <RestartServicePanel />

      {/* Secrets Management (staging/dev) */}
      <SecretsPanel />

      {/* Circuit Breaker States (Issue #3.4) */}
      <CircuitBreakerPanel />
    </div>
  );
}
