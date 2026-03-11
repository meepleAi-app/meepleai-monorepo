/**
 * Admin Service Dashboard
 * Issue #132 — Phase 2A: Enhanced ServiceHealthMatrix
 *
 * Route: /admin/monitor/services
 * Enhanced service health view with auto-refresh, uptime badges,
 * response time trends, and service category grouping.
 */

import { ServicesNavConfig } from './NavConfig';
import { ServicesDashboard } from './ServicesDashboard';

export default function ServiceDashboardPage() {
  return (
    <div className="space-y-5" data-testid="services-page">
      <ServicesNavConfig />
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Service Dashboard
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Service health monitoring with auto-refresh, uptime tracking, and response time trends.
        </p>
      </div>

      <ServicesDashboard />
    </div>
  );
}
