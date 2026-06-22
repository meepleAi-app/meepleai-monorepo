/* eslint-disable local/no-hardcoded-color-utility -- admin CRUD chrome: text-white / button color on style-prop colored bg or admin-decorative inline gradient. DS-13c admin scope (--admin-* decision deferred to DS-15). */
'use client';

import { Badge } from '@/components/ui/badge';
import { useAdminAiModels, useToggleAdminAiModel } from '@/hooks/queries/useAdminAiModels';
import { AdminAiModelsApiError, type AiModelDto } from '@/lib/api/admin-ai-models';

interface ModelRow {
  id: string;
  provider: string;
  name: string;
  enabled: boolean;
  /** Input price per 1,000 tokens, derived from `Settings.Pricing.InputPricePerMillion / 1000`. */
  costPer1k: number;
  /** Server doesn't track per-model avg latency — placeholder "—". Phase 2 BE extension tracked in #1442. */
  avgLatency: string;
  /** Total requests from server-side UsageStats. */
  usage: number;
}

function mapDtoToRow(dto: AiModelDto): ModelRow {
  return {
    id: dto.id,
    provider: dto.provider,
    name: dto.displayName,
    enabled: dto.isActive,
    costPer1k: dto.settings.pricing.inputPricePerMillion / 1000,
    avgLatency: '—',
    usage: dto.usage.totalRequests,
  };
}

function describeError(err: unknown): string {
  if (err instanceof AdminAiModelsApiError) {
    return err.serverMessage;
  }
  if (err instanceof Error) {
    return err.message;
  }
  return 'Unknown error';
}

export function ModelsTable() {
  const modelsQuery = useAdminAiModels();
  const toggleMutation = useToggleAdminAiModel();
  const rows = (modelsQuery.data ?? []).map(mapDtoToRow);

  const handleToggle = async (id: string) => {
    try {
      await toggleMutation.mutateAsync(id);
    } catch (err) {
      // Surface to console + (transient) UI via mutation state.
      // The BE returns 409 when toggling a primary model off — that text
      // is preserved on `toggleMutation.error.message` for callers that
      // want to render it.

      console.warn('Failed to toggle AI model:', describeError(err));
    }
  };

  return (
    <div className="bg-card/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-amber-200/50 dark:border-zinc-700/50 overflow-hidden">
      <div className="p-6 border-b border-border dark:border-zinc-700">
        <h2 className="font-quicksand text-xl font-bold text-foreground dark:text-zinc-100">
          AI Models
        </h2>
      </div>

      {toggleMutation.isError && (
        <div
          role="alert"
          className="px-6 py-3 bg-red-50/80 dark:bg-red-900/40 text-sm text-red-700 dark:text-red-200 border-b border-red-200 dark:border-red-800"
        >
          {describeError(toggleMutation.error)}
        </div>
      )}

      <div className="overflow-x-auto">
        <table className="w-full">
          <thead className="bg-amber-100/50 dark:bg-zinc-900/50 border-b border-amber-200/50 dark:border-zinc-700/50">
            <tr>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Provider
              </th>
              <th className="text-left py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Model
              </th>
              <th className="text-center py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Status
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Cost/1k
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Latency
              </th>
              <th className="text-right py-3 px-4 text-sm font-bold text-amber-900 dark:text-amber-400 uppercase">
                Usage
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-zinc-700">
            {modelsQuery.isLoading && (
              <tr>
                <td
                  colSpan={6}
                  className="px-6 py-8 text-center text-sm text-muted-foreground dark:text-muted-foreground"
                >
                  Loading AI models…
                </td>
              </tr>
            )}
            {modelsQuery.isError && (
              <tr>
                <td
                  colSpan={6}
                  className="px-6 py-8 text-center text-sm text-red-600 dark:text-red-400"
                  role="alert"
                >
                  Failed to load AI models: {describeError(modelsQuery.error)}
                </td>
              </tr>
            )}
            {!modelsQuery.isLoading && !modelsQuery.isError && rows.length === 0 && (
              <tr>
                <td
                  colSpan={6}
                  className="px-6 py-8 text-center text-sm text-muted-foreground dark:text-muted-foreground"
                >
                  No AI models configured yet.
                </td>
              </tr>
            )}
            {rows.map(model => (
              <tr key={model.id} className="hover:bg-muted/50 dark:hover:bg-zinc-900/50">
                <td className="py-3 px-4">
                  <Badge
                    variant="outline"
                    className="bg-muted text-foreground dark:bg-card dark:text-foreground"
                  >
                    {model.provider}
                  </Badge>
                </td>
                <td className="py-3 px-4 font-medium text-foreground dark:text-zinc-100">
                  {model.name}
                </td>
                <td className="py-3 px-4 text-center">
                  <button
                    onClick={() => {
                      void handleToggle(model.id);
                    }}
                    disabled={toggleMutation.isPending}
                    aria-label={`Toggle ${model.name}`}
                    className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors disabled:opacity-50 ${
                      model.enabled ? 'bg-green-500 dark:bg-green-600' : 'bg-muted dark:bg-zinc-700'
                    }`}
                  >
                    <span
                      className={`inline-block h-4 w-4 transform rounded-full bg-card transition-transform ${
                        model.enabled ? 'translate-x-6' : 'translate-x-1'
                      }`}
                    />
                    <span className="sr-only">Toggle {model.name}</span>
                  </button>
                </td>
                <td className="py-3 px-4 text-right font-mono text-sm text-muted-foreground dark:text-muted-foreground">
                  ${model.costPer1k.toFixed(4)}
                </td>
                <td className="py-3 px-4 text-right font-mono text-sm text-muted-foreground dark:text-muted-foreground">
                  {model.avgLatency}
                </td>
                <td className="py-3 px-4 text-right">
                  <Badge
                    variant="outline"
                    className="bg-blue-100 text-blue-900 dark:bg-blue-900/30 dark:text-blue-300"
                  >
                    {model.usage.toLocaleString()}
                  </Badge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
