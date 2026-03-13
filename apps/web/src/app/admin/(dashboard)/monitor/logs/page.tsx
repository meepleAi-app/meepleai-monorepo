'use client';

/**
 * Log Viewer Page — Admin Infrastructure Panel Phase 3
 * Issue #140
 */

import { LogViewer } from './LogViewer';
import { LogsNavConfig } from './NavConfig';

export default function LogViewerPage() {
  return (
    <div data-testid="logs-page" className="space-y-6">
      <LogsNavConfig />
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Log Viewer</h1>
        <p className="text-muted-foreground">Container logs and application monitoring</p>
      </div>
      <LogViewer />
    </div>
  );
}
