'use client';

/**
 * Container Dashboard Page — Admin Infrastructure Panel Phase 4
 * Issue #143
 */

import { ContainerDashboard } from './ContainerDashboard';
import { RestartAllPanel } from './RestartAllPanel';

export default function ContainerDashboardPage() {
  return (
    <div data-testid="containers-page" className="space-y-6">
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold tracking-tight text-foreground">
          Container Dashboard
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Docker container status, metrics, and management controls.
        </p>
      </div>

      <ContainerDashboard />

      {/* Issue #145: Restart All with dependency-ordered restart */}
      <RestartAllPanel />
    </div>
  );
}
