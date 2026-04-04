'use client';

/**
 * LogViewer Component
 * Issue #141 — Container log viewer for Admin Infrastructure Panel Phase 3
 */

import { useEffect, useState, useCallback } from 'react';

import { api } from '@/lib/api';
import type { ContainerInfo, ContainerLogs } from '@/lib/api/schemas';

export function LogViewer() {
  const [containers, setContainers] = useState<ContainerInfo[]>([]);
  const [selectedContainer, setSelectedContainer] = useState<string | null>(null);
  const [logs, setLogs] = useState<ContainerLogs | null>(null);
  const [loading, setLoading] = useState(true);
  const [logsLoading, setLogsLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    async function fetchContainers(): Promise<void> {
      try {
        const data = await api.admin.getDockerContainers();
        if (!cancelled) {
          setContainers(data);
          setLoading(false);
        }
      } catch {
        if (!cancelled) {
          setContainers([]);
          setLoading(false);
        }
      }
    }
    void fetchContainers();
    return () => {
      cancelled = true;
    };
  }, []);

  const fetchLogs = useCallback(async (containerId: string): Promise<void> => {
    setLogsLoading(true);
    try {
      const data = await api.admin.getContainerLogs(containerId);
      setLogs(data);
    } catch {
      setLogs(null);
    } finally {
      setLogsLoading(false);
    }
  }, []);

  const handleContainerClick = useCallback(
    (containerId: string): void => {
      setSelectedContainer(containerId);
      void fetchLogs(containerId);
    },
    [fetchLogs]
  );

  const handleRefresh = useCallback((): void => {
    if (selectedContainer) {
      void fetchLogs(selectedContainer);
    }
  }, [selectedContainer, fetchLogs]);

  if (loading) {
    return (
      <div data-testid="log-viewer-loading" className="flex items-center justify-center py-12">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      </div>
    );
  }

  if (containers.length === 0) {
    return (
      <div
        data-testid="log-viewer-empty"
        className="rounded-xl border bg-white/70 p-8 text-center backdrop-blur-md dark:bg-zinc-900/70"
      >
        <p className="text-muted-foreground">
          No containers found. Make sure Docker Socket Proxy is running.
        </p>
      </div>
    );
  }

  return (
    <div className="flex gap-4" data-testid="log-viewer">
      {/* Container sidebar */}
      <div
        data-testid="container-list"
        className="w-64 shrink-0 space-y-2 rounded-xl border bg-white/70 p-4 backdrop-blur-md dark:bg-zinc-900/70"
      >
        <h3 className="mb-3 text-sm font-semibold text-muted-foreground">Containers</h3>
        {containers.map(container => (
          <button
            key={container.id}
            data-testid={`container-item-${container.id}`}
            type="button"
            onClick={() => handleContainerClick(container.id)}
            className={`w-full rounded-lg px-3 py-2 text-left text-sm transition-colors ${
              selectedContainer === container.id ? 'bg-primary/10 text-primary' : 'hover:bg-muted'
            }`}
          >
            <div className="font-medium">{container.name}</div>
            <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <span
                className={`inline-block h-2 w-2 rounded-full ${
                  container.state === 'running' ? 'bg-green-500' : 'bg-red-500'
                }`}
              />
              {container.status}
            </div>
          </button>
        ))}
      </div>

      {/* Log output panel */}
      <div className="min-w-0 flex-1 rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70">
        {logs ? (
          <div className="flex h-full flex-col">
            <div className="flex items-center justify-between border-b px-4 py-3">
              <h3 className="text-sm font-semibold">{logs.containerName} — Logs</h3>
              <button
                data-testid="log-refresh-btn"
                type="button"
                onClick={handleRefresh}
                disabled={logsLoading}
                className="rounded-md px-3 py-1.5 text-xs font-medium text-primary hover:bg-primary/10 disabled:opacity-50"
              >
                {logsLoading ? 'Loading...' : 'Refresh'}
              </button>
            </div>
            <pre
              data-testid="log-output"
              className="flex-1 overflow-auto p-4 font-mono text-xs leading-relaxed text-foreground"
            >
              {logs.lines.join('\n')}
            </pre>
          </div>
        ) : (
          <div className="flex h-64 items-center justify-center text-muted-foreground">
            Select a container to view logs
          </div>
        )}
      </div>
    </div>
  );
}
