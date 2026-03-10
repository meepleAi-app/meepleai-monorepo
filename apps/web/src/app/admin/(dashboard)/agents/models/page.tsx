'use client';

/**
 * Admin — Model Health & Availability page
 * Issue #5503: Health badges, check-now button, change history.
 * Part of Epic #5490 - Model Versioning & Availability Monitoring.
 */

import { useEffect, useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, CheckCircle2, Clock, RefreshCw, XCircle } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/primitives/button';
import { useSetNavConfig } from '@/hooks/useSetNavConfig';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type {
  ModelHealthDto,
  ModelChangeHistoryDto,
} from '@/lib/api/schemas/tier-strategy.schemas';

// ─── Module-level client ─────────────────────────────────────────────────────

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

// ─── Health Badge ────────────────────────────────────────────────────────────

function HealthBadge({
  isAvailable,
  isDeprecated,
}: {
  isAvailable: boolean;
  isDeprecated: boolean;
}) {
  if (isDeprecated) {
    return (
      <Badge
        variant="outline"
        className="border-amber-500 text-amber-600 dark:text-amber-400 gap-1"
      >
        <AlertTriangle className="h-3 w-3" aria-label="deprecated" />
        Deprecated
      </Badge>
    );
  }
  if (!isAvailable) {
    return (
      <Badge variant="outline" className="border-red-500 text-red-600 dark:text-red-400 gap-1">
        <XCircle className="h-3 w-3" aria-label="unavailable" />
        Unavailable
      </Badge>
    );
  }
  return (
    <Badge
      variant="outline"
      className="border-emerald-500 text-emerald-600 dark:text-emerald-400 gap-1"
    >
      <CheckCircle2 className="h-3 w-3" aria-label="available" />
      Available
    </Badge>
  );
}

// ─── Change Type Badge ───────────────────────────────────────────────────────

function ChangeTypeBadge({ type }: { type: string }) {
  const styles: Record<string, string> = {
    deprecated: 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300',
    unavailable: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300',
    restored: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/30 dark:text-emerald-300',
    replaced: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300',
    fallback_activated: 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300',
    swapped: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-900/30 dark:text-indigo-300',
  };
  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${styles[type] || 'bg-slate-100 text-slate-800 dark:bg-slate-800 dark:text-slate-300'}`}
    >
      {type.replace(/_/g, ' ')}
    </span>
  );
}

// ─── Time Ago ────────────────────────────────────────────────────────────────

function timeAgo(dateStr: string | null): string {
  if (!dateStr) return 'Never';
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'Just now';
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

// ─── Models Table ────────────────────────────────────────────────────────────

function ModelHealthTable({ models }: { models: ModelHealthDto[] }) {
  if (models.length === 0) {
    return (
      <div className="text-center py-12 text-muted-foreground">
        No models tracked yet. Models will appear after the first availability check runs.
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200 dark:border-zinc-700">
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Model</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Provider</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Status</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Context</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Last Verified</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Alternatives</th>
          </tr>
        </thead>
        <tbody>
          {models.map(model => (
            <tr
              key={model.modelId}
              className="border-b border-slate-100 dark:border-zinc-800 hover:bg-slate-50/50 dark:hover:bg-zinc-800/50 transition-colors"
            >
              <td className="py-3 px-4">
                <div className="font-medium text-foreground">
                  {model.displayName || model.modelId}
                </div>
                <div className="text-xs text-muted-foreground font-mono mt-0.5">
                  {model.modelId}
                </div>
              </td>
              <td className="py-3 px-4 text-muted-foreground capitalize">{model.provider}</td>
              <td className="py-3 px-4">
                <HealthBadge
                  isAvailable={model.isCurrentlyAvailable}
                  isDeprecated={model.isDeprecated}
                />
              </td>
              <td className="py-3 px-4 text-muted-foreground">
                {model.contextWindow > 0 ? `${(model.contextWindow / 1000).toFixed(0)}K` : '—'}
              </td>
              <td className="py-3 px-4">
                <div className="flex items-center gap-1.5 text-muted-foreground">
                  <Clock className="h-3.5 w-3.5" />
                  <span className="text-xs">{timeAgo(model.lastVerifiedAt)}</span>
                </div>
              </td>
              <td className="py-3 px-4 text-xs text-muted-foreground">
                {model.alternatives.length > 0 ? model.alternatives.slice(0, 2).join(', ') : '—'}
                {model.alternatives.length > 2 && ` +${model.alternatives.length - 2}`}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── Change History Table ────────────────────────────────────────────────────

function ChangeHistoryTable({ changes }: { changes: ModelChangeHistoryDto[] }) {
  if (changes.length === 0) {
    return (
      <div className="text-center py-8 text-muted-foreground text-sm">
        No model changes recorded yet.
      </div>
    );
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200 dark:border-zinc-700">
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">When</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Type</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Model</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Strategy</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Change</th>
            <th className="text-left py-3 px-4 font-medium text-muted-foreground">Source</th>
          </tr>
        </thead>
        <tbody>
          {changes.map(change => (
            <tr
              key={change.id}
              className="border-b border-slate-100 dark:border-zinc-800 hover:bg-slate-50/50 dark:hover:bg-zinc-800/50 transition-colors"
            >
              <td className="py-3 px-4 text-xs text-muted-foreground whitespace-nowrap">
                {timeAgo(change.occurredAt)}
              </td>
              <td className="py-3 px-4">
                <ChangeTypeBadge type={change.changeType} />
              </td>
              <td className="py-3 px-4 font-mono text-xs text-foreground">{change.modelId}</td>
              <td className="py-3 px-4 text-muted-foreground">{change.affectedStrategy || '—'}</td>
              <td className="py-3 px-4 text-xs text-muted-foreground max-w-[300px] truncate">
                {change.previousModelId && change.newModelId
                  ? `${change.previousModelId} → ${change.newModelId}`
                  : change.reason}
              </td>
              <td className="py-3 px-4">
                <Badge variant="outline" className="text-xs">
                  {change.isAutomatic ? 'Auto' : 'Admin'}
                </Badge>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── Page ────────────────────────────────────────────────────────────────────

export default function ModelHealthPage() {
  const setNavConfig = useSetNavConfig();
  const queryClient = useQueryClient();
  const [historyLimit] = useState(50);

  useEffect(() => {
    setNavConfig({
      miniNav: [{ id: 'models', label: 'Model Health', href: '/admin/agents/models' }],
      actionBar: [],
    });
  }, [setNavConfig]);

  // ── Queries ──
  const {
    data: healthData,
    isLoading: healthLoading,
    isError: healthError,
  } = useQuery({
    queryKey: ['admin', 'model-health'],
    queryFn: () => adminClient.getModelHealth(),
    refetchInterval: 60_000,
    retry: 1,
  });

  const {
    data: historyData,
    isLoading: historyLoading,
    isError: historyError,
  } = useQuery({
    queryKey: ['admin', 'model-change-history', historyLimit],
    queryFn: () => adminClient.getModelChangeHistory(undefined, historyLimit),
    refetchInterval: 60_000,
    retry: 1,
  });

  // ── Check Now mutation ──
  const [showCheckSuccess, setShowCheckSuccess] = useState(false);
  const checkNow = useMutation({
    mutationFn: () => adminClient.triggerModelAvailabilityCheck(),
    onSuccess: data => {
      if (data?.triggered) {
        setShowCheckSuccess(true);
        // Auto-dismiss after 8 seconds and refetch
        setTimeout(() => {
          setShowCheckSuccess(false);
          queryClient.invalidateQueries({ queryKey: ['admin', 'model-health'] });
          queryClient.invalidateQueries({ queryKey: ['admin', 'model-change-history'] });
        }, 5000);
      }
    },
  });

  const models = healthData?.models ?? [];
  const changes = historyData?.changes ?? [];

  const availableCount = models.filter(m => m.isCurrentlyAvailable && !m.isDeprecated).length;
  const deprecatedCount = models.filter(m => m.isDeprecated).length;
  const unavailableCount = models.filter(m => !m.isCurrentlyAvailable && !m.isDeprecated).length;

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Model Health
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Monitor LLM model availability and change history
          </p>
        </div>
        <Button
          className="bg-amber-500 hover:bg-amber-600 text-white gap-2"
          onClick={() => checkNow.mutate()}
          disabled={checkNow.isPending}
        >
          <RefreshCw className={`h-4 w-4 ${checkNow.isPending ? 'animate-spin' : ''}`} />
          {checkNow.isPending ? 'Checking...' : 'Check Now'}
        </Button>
      </div>

      {/* Status Summary */}
      {!healthLoading && !healthError && models.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-sm rounded-xl border border-slate-200/60 dark:border-zinc-700/40 p-4">
            <div className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400">
              <CheckCircle2 className="h-5 w-5" />
              <span className="text-2xl font-bold">{availableCount}</span>
            </div>
            <p className="text-xs text-muted-foreground mt-1">Available Models</p>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-sm rounded-xl border border-slate-200/60 dark:border-zinc-700/40 p-4">
            <div className="flex items-center gap-2 text-amber-600 dark:text-amber-400">
              <AlertTriangle className="h-5 w-5" />
              <span className="text-2xl font-bold">{deprecatedCount}</span>
            </div>
            <p className="text-xs text-muted-foreground mt-1">Deprecated Models</p>
          </div>
          <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-sm rounded-xl border border-slate-200/60 dark:border-zinc-700/40 p-4">
            <div className="flex items-center gap-2 text-red-600 dark:text-red-400">
              <XCircle className="h-5 w-5" />
              <span className="text-2xl font-bold">{unavailableCount}</span>
            </div>
            <p className="text-xs text-muted-foreground mt-1">Unavailable Models</p>
          </div>
        </div>
      )}

      {/* Check Now Feedback */}
      {showCheckSuccess && (
        <div className="bg-emerald-50 dark:bg-emerald-900/20 border border-emerald-200 dark:border-emerald-800 rounded-lg p-3 text-sm text-emerald-800 dark:text-emerald-300">
          Availability check triggered. Results will update in a few seconds.
        </div>
      )}
      {checkNow.isError && (
        <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-3 text-sm text-red-800 dark:text-red-300">
          Failed to trigger availability check. Please try again.
        </div>
      )}

      {/* Models Table */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40">
        <div className="px-6 py-4 border-b border-slate-200/60 dark:border-zinc-700/40">
          <h2 className="font-quicksand text-lg font-semibold text-foreground">Tracked Models</h2>
        </div>
        {healthLoading ? (
          <div className="h-[200px] animate-pulse bg-slate-100/50 dark:bg-zinc-700/30 rounded-b-2xl" />
        ) : healthError ? (
          <div className="flex items-center gap-2 p-6 text-red-500">
            <AlertTriangle className="h-5 w-5" />
            Failed to load model health data.
          </div>
        ) : (
          <ModelHealthTable models={models} />
        )}
      </div>

      {/* Change History */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40">
        <div className="px-6 py-4 border-b border-slate-200/60 dark:border-zinc-700/40">
          <h2 className="font-quicksand text-lg font-semibold text-foreground">Change History</h2>
          <p className="text-xs text-muted-foreground mt-0.5">
            Audit trail of model swaps, deprecations, and automatic fallbacks
          </p>
        </div>
        {historyLoading ? (
          <div className="h-[200px] animate-pulse bg-slate-100/50 dark:bg-zinc-700/30 rounded-b-2xl" />
        ) : historyError ? (
          <div className="flex items-center gap-2 p-6 text-red-500">
            <AlertTriangle className="h-5 w-5" />
            Failed to load change history.
          </div>
        ) : (
          <ChangeHistoryTable changes={changes} />
        )}
      </div>
    </div>
  );
}
