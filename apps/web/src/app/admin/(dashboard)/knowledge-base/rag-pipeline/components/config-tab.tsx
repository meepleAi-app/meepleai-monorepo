'use client';

import { Settings2Icon, Loader2Icon } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';

import { useQueueConfig } from '../../queue/lib/queue-api';

// ── Main Component ────────────────────────────────────────────────────

export function ConfigTab() {
  const { data: config, isLoading } = useQueueConfig();

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2Icon className="h-5 w-5 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <h2 className="text-base font-semibold flex items-center gap-2">
        <Settings2Icon className="h-4 w-4" />
        Pipeline Configuration
      </h2>

      {/* Queue Config */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Queue Settings</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            <ConfigField
              label="Queue Status"
              value={config?.isPaused ? 'Paused' : 'Active'}
              accent={
                config?.isPaused
                  ? 'text-amber-600 dark:text-amber-400'
                  : 'text-emerald-600 dark:text-emerald-400'
              }
            />
            <ConfigField
              label="Max Concurrent Workers"
              value={config?.maxConcurrentWorkers?.toString() ?? '\u2014'}
            />
            <ConfigField
              label="Last Updated"
              value={config?.updatedAt ? new Date(config.updatedAt).toLocaleString() : '\u2014'}
            />
            {config?.updatedBy && <ConfigField label="Updated By" value={config.updatedBy} />}
          </div>
        </CardContent>
      </Card>

      {/* Pipeline Info */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Pipeline Info</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            The RAG pipeline processes uploaded PDFs through extraction, chunking, embedding, and
            vector indexing. Configuration changes can be made via the API or the queue management
            panel.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}

// ── Sub-component ─────────────────────────────────────────────────────

function ConfigField({ label, value, accent }: { label: string; value: string; accent?: string }) {
  return (
    <div className="rounded-lg border bg-white/50 dark:bg-zinc-800/50 p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className={`text-sm font-medium ${accent ?? 'text-foreground'}`}>{value}</p>
    </div>
  );
}
