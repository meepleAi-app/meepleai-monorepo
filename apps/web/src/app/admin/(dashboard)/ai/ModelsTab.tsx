'use client';

/**
 * #4169 (ADR-095 D5/D8, fetta 6b): «Models & prices».
 *
 * The model catalog is the single price list of the LLM gateway: the price of each model is what the
 * gateway uses for cost, budget cap and degrade. The page shows and edits what the runtime reads, and
 * only that:
 * - the price (USD per million tokens), with where it comes from;
 * - whether the model is in the fallback order (`isActive`): it does not block calls to the model;
 * - the default model of each provider (`isPrimary`), used when the provider enters the fallback.
 * Generation settings (temperature, max tokens) belong to the agent profile, not to the catalog: the
 * gateway never read them from here.
 */
import { useCallback, useEffect, useId, useState } from 'react';

import { Cpu, Loader2, Pencil, Star } from 'lucide-react';

import { AdminHubEmptyState } from '@/components/admin/layout/AdminHubEmptyState';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { AiModelDto } from '@/lib/api/schemas';

/** Providers that cost nothing: models served in house. */
const FREE_PROVIDERS = new Set(['ollama']);

type PriceStatus = 'priced' | 'free' | 'unpriced' | 'foreign-currency';

function priceStatus(model: AiModelDto): PriceStatus {
  const { pricing } = model.settings;
  if (pricing.currency.toUpperCase() !== 'USD') return 'foreign-currency';
  if (pricing.inputPricePerMillion > 0 || pricing.outputPricePerMillion > 0) return 'priced';
  return FREE_PROVIDERS.has(model.provider.toLowerCase()) ? 'free' : 'unpriced';
}

const STATUS_TEXT: Record<PriceStatus, string> = {
  priced: 'Priced',
  free: 'Free (local)',
  unpriced: 'No price: counted as free, so it escapes the budget cap',
  'foreign-currency': 'Not in USD: the gateway refuses it',
};

// Fino a quattro decimali: i modelli economici costano frazioni di centesimo per milione di token.
const usd = (value: number) =>
  `$${value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 4 })}`;

function sortModels(models: AiModelDto[]): AiModelDto[] {
  return [...models].sort(
    (a, b) =>
      a.provider.localeCompare(b.provider) ||
      Number(b.isPrimary) - Number(a.isPrimary) ||
      a.priority - b.priority ||
      a.modelId.localeCompare(b.modelId)
  );
}

export function ModelsTab() {
  const [models, setModels] = useState<AiModelDto[]>([]);
  const [loading, setLoading] = useState(true);
  // 🔴 #4059 — l'errore deve arrivare a schermo, non solo in console: un errore di schema e un
  // catalogo legittimamente vuoto non devono essere lo stesso schermo.
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const data = await api.admin.getAiModels({ pageSize: 100 });
      setModels(sortModels(data.models));
      setError(null);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Could not load the AI models');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const run = async (id: string, action: () => Promise<unknown>, failure: string) => {
    setBusyId(id);
    setActionError(null);
    try {
      await action();
      await load();
      return true;
    } catch (err: unknown) {
      setActionError(err instanceof Error ? `${failure}: ${err.message}` : failure);
      return false;
    } finally {
      setBusyId(null);
    }
  };

  const savePrice = async (model: AiModelDto, input: number, output: number) => {
    const saved = await run(
      model.id,
      () =>
        api.admin.updateModelPricing(model.id, {
          inputPricePerMillion: input,
          outputPricePerMillion: output,
        }),
      `Could not save the price of ${model.modelId}`
    );
    if (saved) setEditingId(null);
  };

  return (
    <div className="space-y-5">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Models &amp; prices
        </h2>
        <p className="text-sm text-muted-foreground mt-0.5">
          The catalog is the price list of the LLM gateway: cost, budget cap and degrade use these
          prices. A price change applies within a minute.
        </p>
      </div>

      {actionError && (
        <div
          role="alert"
          className="rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive"
        >
          {actionError}
        </div>
      )}

      {loading ? (
        <div className="space-y-2" aria-busy="true">
          {[1, 2, 3].map(i => (
            <div key={i} className="h-12 rounded-lg bg-card/40 animate-pulse" />
          ))}
        </div>
      ) : error ? (
        <div
          role="alert"
          className="rounded-xl border border-destructive/30 bg-destructive/10 p-4"
          data-testid="models-tab-error"
        >
          <p className="text-sm font-medium text-destructive">Could not load the AI models.</p>
          <p className="mt-1 text-xs text-muted-foreground">{error}</p>
        </div>
      ) : models.length > 0 ? (
        <div className="overflow-x-auto rounded-xl border border-border/60 bg-card/70">
          <table className="w-full text-sm">
            <caption className="sr-only">AI model catalog with prices</caption>
            <thead>
              <tr className="border-b border-border/60 text-left text-xs text-muted-foreground">
                <th scope="col" className="px-3 py-2 font-medium">
                  Model
                </th>
                <th scope="col" className="px-3 py-2 font-medium">
                  Price per 1M tokens (in / out)
                </th>
                <th scope="col" className="px-3 py-2 font-medium">
                  Price source
                </th>
                <th scope="col" className="px-3 py-2 font-medium">
                  In fallback order
                </th>
                <th scope="col" className="px-3 py-2 font-medium">
                  Default of provider
                </th>
              </tr>
            </thead>
            <tbody>
              {models.map(m => (
                <ModelRow
                  key={m.id}
                  model={m}
                  busy={busyId === m.id}
                  editing={editingId === m.id}
                  onEdit={() => {
                    setEditingId(m.id);
                    setActionError(null);
                  }}
                  onCancel={() => setEditingId(null)}
                  onSavePrice={(input, output) => void savePrice(m, input, output)}
                  onToggle={() =>
                    void run(
                      m.id,
                      () => api.admin.toggleModelActive(m.id),
                      `Could not change ${m.modelId}`
                    )
                  }
                  onMakeDefault={() =>
                    void run(
                      m.id,
                      () => api.admin.setPrimaryModel({ modelId: m.id }),
                      `Could not make ${m.modelId} the default`
                    )
                  }
                />
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <AdminHubEmptyState
          icon={<Cpu />}
          title="No AI models configured"
          description="The catalog is seeded at startup; an empty catalog means every paid call is refused."
        />
      )}

      <p className="text-xs text-muted-foreground">
        «In fallback order» only decides which providers the gateway tries when the chosen one
        fails: it does not block calls to the model. The default of a provider is the model used
        when that provider enters the fallback.
      </p>
    </div>
  );
}

function ModelRow({
  model,
  busy,
  editing,
  onEdit,
  onCancel,
  onSavePrice,
  onToggle,
  onMakeDefault,
}: {
  model: AiModelDto;
  busy: boolean;
  editing: boolean;
  onEdit: () => void;
  onCancel: () => void;
  onSavePrice: (input: number, output: number) => void;
  onToggle: () => void;
  onMakeDefault: () => void;
}) {
  const { pricing } = model.settings;
  const status = priceStatus(model);
  const cannotDeactivate = model.isPrimary && model.isActive;

  return (
    <tr
      aria-label={`${model.modelId} (${model.provider})`}
      className="border-b border-border/40 align-top"
    >
      <td className="px-3 py-2">
        <p className="font-medium text-foreground">{model.modelId}</p>
        <p className="text-xs text-muted-foreground">
          {model.provider}
          {model.displayName !== model.modelId ? ` · ${model.displayName}` : ''}
        </p>
      </td>
      <td className="px-3 py-2">
        {editing ? (
          <PriceEditor model={model} busy={busy} onSave={onSavePrice} onCancel={onCancel} />
        ) : (
          <div className="space-y-1">
            <p className="font-mono text-foreground">
              {usd(pricing.inputPricePerMillion)} / {usd(pricing.outputPricePerMillion)}
            </p>
            <p
              className={
                status === 'unpriced' || status === 'foreign-currency'
                  ? 'text-xs text-[hsl(var(--c-warning-ink))]'
                  : 'text-xs text-muted-foreground'
              }
            >
              {STATUS_TEXT[status]}
            </p>
            <Button
              variant="ghost"
              size="sm"
              className="h-7 px-2 text-xs"
              onClick={onEdit}
              aria-label={`Edit price of ${model.modelId}`}
            >
              <Pencil className="mr-1 h-3 w-3" aria-hidden="true" /> Edit price
            </Button>
          </div>
        )}
      </td>
      <td className="px-3 py-2 text-xs text-muted-foreground">
        {pricing.verifiedOn ? (
          <>
            <p>Verified {pricing.verifiedOn}</p>
            <p className="break-all">{pricing.source ?? 'unknown source'}</p>
          </>
        ) : (
          <p>Never verified</p>
        )}
      </td>
      <td className="px-3 py-2">
        <button
          type="button"
          role="switch"
          aria-checked={model.isActive}
          aria-label={`In fallback order: ${model.modelId}`}
          disabled={busy || cannotDeactivate}
          title={
            cannotDeactivate ? 'Make another model the default of this provider first' : undefined
          }
          onClick={onToggle}
          className="inline-flex items-center gap-2 rounded-full border border-border px-2 py-1 text-xs text-foreground disabled:opacity-50"
        >
          <span
            aria-hidden="true"
            className={`inline-block h-2 w-2 rounded-full ${
              model.isActive ? 'bg-[hsl(var(--c-success))]' : 'bg-muted-foreground/40'
            }`}
          />
          {model.isActive ? 'Yes' : 'No'}
        </button>
      </td>
      <td className="px-3 py-2">
        {model.isPrimary ? (
          <span className="inline-flex items-center gap-1 rounded-full bg-[hsl(var(--c-warning)/0.15)] px-2 py-0.5 text-xs font-medium text-[hsl(var(--c-warning-ink))]">
            <Star className="h-3 w-3" aria-hidden="true" /> Default
          </span>
        ) : (
          <Button
            variant="outline"
            size="sm"
            className="text-xs"
            disabled={busy || !model.isActive}
            title={!model.isActive ? 'Put the model in the fallback order first' : undefined}
            onClick={onMakeDefault}
            aria-label={`Make default: ${model.modelId}`}
          >
            {busy ? <Loader2 className="mr-1 h-3 w-3 animate-spin" aria-hidden="true" /> : null}
            Make default
          </Button>
        )}
      </td>
    </tr>
  );
}

function PriceEditor({
  model,
  busy,
  onSave,
  onCancel,
}: {
  model: AiModelDto;
  busy: boolean;
  onSave: (input: number, output: number) => void;
  onCancel: () => void;
}) {
  const id = useId();
  const [input, setInput] = useState(String(model.settings.pricing.inputPricePerMillion));
  const [output, setOutput] = useState(String(model.settings.pricing.outputPricePerMillion));
  const [invalid, setInvalid] = useState(false);

  const submit = () => {
    const inputValue = Number(input);
    const outputValue = Number(output);
    const valid =
      input.trim() !== '' &&
      output.trim() !== '' &&
      [inputValue, outputValue].every(v => Number.isFinite(v) && v >= 0 && v <= 1000);
    setInvalid(!valid);
    if (valid) onSave(inputValue, outputValue);
  };

  return (
    <div className="space-y-2">
      <div className="flex gap-2">
        <div>
          <label htmlFor={`${id}-in`} className="block text-[11px] text-muted-foreground">
            Input price (USD / 1M)
          </label>
          <input
            id={`${id}-in`}
            type="number"
            inputMode="decimal"
            min={0}
            max={1000}
            step={0.01}
            value={input}
            onChange={e => setInput(e.target.value)}
            aria-invalid={invalid}
            className="w-24 rounded-md border border-border bg-background px-2 py-1 text-xs text-foreground focus:outline-none focus:ring-2 focus:ring-primary/50"
          />
        </div>
        <div>
          <label htmlFor={`${id}-out`} className="block text-[11px] text-muted-foreground">
            Output price (USD / 1M)
          </label>
          <input
            id={`${id}-out`}
            type="number"
            inputMode="decimal"
            min={0}
            max={1000}
            step={0.01}
            value={output}
            onChange={e => setOutput(e.target.value)}
            aria-invalid={invalid}
            className="w-24 rounded-md border border-border bg-background px-2 py-1 text-xs text-foreground focus:outline-none focus:ring-2 focus:ring-primary/50"
          />
        </div>
      </div>
      {invalid && (
        <p role="alert" className="text-xs text-destructive">
          Prices are numbers between 0 and 1000 USD per million tokens.
        </p>
      )}
      <div className="flex gap-2">
        <Button size="sm" className="h-7 text-xs" disabled={busy} onClick={submit}>
          Save price
        </Button>
        <Button variant="ghost" size="sm" className="h-7 text-xs" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </div>
  );
}
