'use client';

/**
 * Log Viewer Page — Application + Container logs
 * Issue #141 / #1.8
 */

import { useState } from 'react';

import { AppLogViewer } from './AppLogViewer';
import { LogViewer } from './LogViewer';

type LogTab = 'app' | 'container';

export default function LogViewerPage() {
  const [activeTab, setActiveTab] = useState<LogTab>('app');

  return (
    <div data-testid="logs-page" className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Log Viewer</h1>
        <p className="text-muted-foreground">Application and container log monitoring</p>
      </div>

      <div className="flex gap-1 border-b">
        <button
          onClick={() => setActiveTab('app')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            activeTab === 'app'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          Application Logs
        </button>
        <button
          onClick={() => setActiveTab('container')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            activeTab === 'container'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          Container Logs
        </button>
      </div>

      {activeTab === 'app' && <AppLogViewer />}
      {activeTab === 'container' && <LogViewer />}
    </div>
  );
}
