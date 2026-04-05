'use client';

/**
 * Log Viewer Page — Application + Container logs + Container Errors (Loki)
 * Issue #141 / #1.8 + #3367 (Hybrid Solution C)
 */

import { useState } from 'react';

import { ExternalLink } from 'lucide-react';

import { AppLogViewer } from './AppLogViewer';
import { LogViewer } from './LogViewer';
import { LokiErrorViewer } from './LokiErrorViewer';

type LogTab = 'app' | 'container' | 'loki';

const GRAFANA_URL = process.env.NEXT_PUBLIC_GRAFANA_URL;

// Build Grafana Explore URL with correctly URL-encoded JSON
function buildGrafanaExploreUrl(baseUrl: string): string {
  const state = JSON.stringify({
    datasource: 'loki',
    queries: [{ refId: 'A', expr: '{container_name=~"meepleai-.*"}' }],
  });
  return `${baseUrl}/explore?left=${encodeURIComponent(state)}`;
}

export default function LogViewerPage() {
  const [activeTab, setActiveTab] = useState<LogTab>('app');

  return (
    <div data-testid="logs-page" className="space-y-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Log Viewer</h1>
          <p className="text-muted-foreground">Application and container log monitoring</p>
        </div>
        {GRAFANA_URL && (
          <a
            href={buildGrafanaExploreUrl(GRAFANA_URL)}
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-1.5 rounded-md border px-3 py-1.5 text-xs text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
            data-testid="grafana-link"
          >
            <ExternalLink className="h-3.5 w-3.5" />
            Open in Grafana
          </a>
        )}
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
        <button
          onClick={() => setActiveTab('loki')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            activeTab === 'loki'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          Container Errors
        </button>
      </div>

      {activeTab === 'app' && <AppLogViewer />}
      {activeTab === 'container' && <LogViewer />}
      {activeTab === 'loki' && <LokiErrorViewer />}
    </div>
  );
}
