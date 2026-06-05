'use client';

/**
 * CostSimulator (#1838 SP5 F4-C5) — what-if calculator (Scenario E nello spec doc).
 *
 * Mockup `sp5-admin-budget.html` riga 480-560.
 *
 * Riusa l'endpoint esistente `POST /admin/cost-calculator/estimate` (issue #3725
 * BC BusinessSimulations) per stimare il costo agent in base a strategia +
 * modello + traffic profile. La modalità "what-if" inversa (delta vs baseline)
 * è derivata in `useMemo` confrontando la stima corrente con un baseline opzionale.
 *
 * Confronto vs budget: se `useBudget` ritorna un AppBudget configurato,
 * mostriamo warning quando `monthlyProjection > monthlyLimit`.
 */

import { useEffect, useMemo, useState } from 'react';

import { useMutation } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';

import { Label } from '@/components/ui/primitives/label';
import { useBudget } from '@/hooks/useBudget';
import { costCalculatorApi } from '@/lib/api/cost-calculator.api';
import type {
  AgentCostEstimationResult,
  EstimateAgentCostRequest,
} from '@/lib/api/schemas/cost-calculator.schemas';
import { cn } from '@/lib/utils';

const STRATEGY_OPTIONS = [
  'Fast',
  'Balanced',
  'Precise',
  'Expert',
  'Consensus',
  'SentenceWindow',
  'Iterative',
  'Custom',
  'MultiAgent',
  'StepBack',
  'QueryExpansion',
  'RagFusion',
] as const;

const MODEL_OPTIONS = [
  { value: 'deepseek/deepseek-chat', label: 'DeepSeek Chat (V3)' },
  { value: 'openrouter/anthropic/claude-sonnet-4', label: 'Claude Sonnet 4 (via OpenRouter)' },
  { value: 'openai/gpt-4o', label: 'GPT-4o' },
  { value: 'openai/gpt-4o-mini', label: 'GPT-4o Mini' },
  { value: 'anthropic/claude-3-haiku', label: 'Claude 3 Haiku' },
] as const;

interface CostSimulatorProps {
  className?: string;
}

function formatCurrency(value: number): string {
  return `$${value.toFixed(2)}`;
}

export function CostSimulator({ className }: CostSimulatorProps) {
  const { budget } = useBudget();

  const [strategy, setStrategy] = useState<string>('Balanced');
  const [modelId, setModelId] = useState<string>('deepseek/deepseek-chat');
  const [messagesPerDay, setMessagesPerDay] = useState<string>('1000');
  const [activeUsers, setActiveUsers] = useState<string>('100');
  const [avgTokens, setAvgTokens] = useState<string>('500');
  const [baseline, setBaseline] = useState<AgentCostEstimationResult | null>(null);

  const estimateMutation = useMutation<AgentCostEstimationResult, Error, EstimateAgentCostRequest>({
    mutationFn: body => costCalculatorApi.estimate(body),
  });

  // Debounced auto-estimate on input change.
  useEffect(() => {
    const msgs = parseInt(messagesPerDay, 10);
    const users = parseInt(activeUsers, 10);
    const tokens = parseInt(avgTokens, 10);
    if (
      Number.isNaN(msgs) ||
      Number.isNaN(users) ||
      Number.isNaN(tokens) ||
      msgs <= 0 ||
      tokens <= 0
    ) {
      return;
    }
    const handle = setTimeout(() => {
      estimateMutation.mutate({
        strategy,
        modelId,
        messagesPerDay: msgs,
        activeUsers: users,
        avgTokensPerRequest: tokens,
      });
    }, 500);
    return () => clearTimeout(handle);
    // estimateMutation reference identity changes per render; intentionally
    // depend only on user input to drive a single debounce per change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [strategy, modelId, messagesPerDay, activeUsers, avgTokens]);

  const result = estimateMutation.data;

  // Baseline anchor: capture first successful estimate as baseline so subsequent
  // adjustments show a delta. Admin can re-anchor by clicking "Save as baseline".
  useEffect(() => {
    if (result && !baseline) {
      setBaseline(result);
    }
  }, [result, baseline]);

  const projection = result?.monthlyProjection ?? 0;
  const baselineProjection = baseline?.monthlyProjection ?? 0;
  const delta = projection - baselineProjection;
  const overBudget = budget != null && projection > budget.monthlyLimit;

  const budgetPctLabel = useMemo(() => {
    if (!budget) return null;
    const pct = (projection / budget.monthlyLimit) * 100;
    return `${pct.toFixed(0)}% del budget`;
  }, [budget, projection]);

  return (
    <section
      className={cn('overflow-hidden rounded-xl border border-border bg-card/60', className)}
      data-testid="cost-simulator"
    >
      <header className="border-b border-border/60 bg-muted/30 px-4 py-2.5">
        <h3 className="font-quicksand text-sm font-bold text-foreground">What-if calculator</h3>
        <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
          stima costo agent · debounced 500ms
        </p>
      </header>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-0">
        {/* Form */}
        <form
          onSubmit={e => e.preventDefault()}
          className="space-y-3 border-b border-border/40 p-4 sm:border-b-0 sm:border-r"
        >
          <div className="space-y-1">
            <Label htmlFor="sim-strategy">Strategia RAG</Label>
            <select
              id="sim-strategy"
              value={strategy}
              onChange={e => setStrategy(e.target.value)}
              className="w-full rounded border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            >
              {STRATEGY_OPTIONS.map(s => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <Label htmlFor="sim-model">Modello</Label>
            <select
              id="sim-model"
              value={modelId}
              onChange={e => setModelId(e.target.value)}
              className="w-full rounded border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            >
              {MODEL_OPTIONS.map(m => (
                <option key={m.value} value={m.value}>
                  {m.label}
                </option>
              ))}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-2">
            <div className="space-y-1">
              <Label htmlFor="sim-msgs">Messaggi/giorno</Label>
              <input
                id="sim-msgs"
                type="number"
                value={messagesPerDay}
                onChange={e => setMessagesPerDay(e.target.value)}
                min={0}
                className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              />
            </div>
            <div className="space-y-1">
              <Label htmlFor="sim-users">Utenti attivi</Label>
              <input
                id="sim-users"
                type="number"
                value={activeUsers}
                onChange={e => setActiveUsers(e.target.value)}
                min={0}
                className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              />
            </div>
          </div>

          <div className="space-y-1">
            <Label htmlFor="sim-tokens">Token medi per richiesta</Label>
            <input
              id="sim-tokens"
              type="number"
              value={avgTokens}
              onChange={e => setAvgTokens(e.target.value)}
              min={1}
              className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>

          <div className="flex items-center justify-between gap-2 border-t border-border/40 pt-2">
            <button
              type="button"
              onClick={() => result && setBaseline(result)}
              disabled={!result}
              className="rounded border border-border bg-background px-3 py-1 font-mono text-[11px] hover:bg-muted disabled:opacity-50"
            >
              Aggancia baseline
            </button>
            {estimateMutation.isPending && (
              <span className="flex items-center gap-1 font-mono text-[10px] text-muted-foreground">
                <Loader2 className="h-3 w-3 animate-spin" aria-hidden /> stima…
              </span>
            )}
          </div>
        </form>

        {/* Readout */}
        <div
          className="flex flex-col justify-center gap-4 p-5"
          style={{ background: 'linear-gradient(180deg, hsl(var(--c-agent)/.06), transparent)' }}
        >
          <div>
            <p className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
              Stima mensile
            </p>
            <p
              className="mt-1 font-quicksand text-[44px] font-bold leading-none tabular-nums text-entity-agent"
              data-testid="cost-simulator-projection"
            >
              <span className="text-2xl text-muted-foreground">$</span>
              {projection.toFixed(2)}
              <span className="ml-1 text-sm text-muted-foreground">/mo</span>
            </p>
            {budgetPctLabel && (
              <p
                className={cn(
                  'mt-1 font-mono text-[11px]',
                  overBudget ? 'text-rose-600 dark:text-rose-400' : 'text-muted-foreground'
                )}
              >
                ▬ {budgetPctLabel}
              </p>
            )}
          </div>

          <div className="space-y-1 border-t border-dashed border-border/60 pt-3">
            <div className="flex items-baseline justify-between font-mono text-[12px]">
              <span className="text-[10px] uppercase tracking-wide text-muted-foreground">
                vs baseline
              </span>
              <span
                className={cn(
                  'font-bold tabular-nums',
                  delta < 0
                    ? 'text-emerald-600 dark:text-emerald-400'
                    : delta > 0
                      ? 'text-rose-600 dark:text-rose-400'
                      : 'text-muted-foreground'
                )}
              >
                {delta === 0 ? '—' : delta > 0 ? '+' : ''}
                {formatCurrency(delta)}/mo
              </span>
            </div>
            <div className="flex items-baseline justify-between font-mono text-[12px]">
              <span className="text-[10px] uppercase tracking-wide text-muted-foreground">
                costo/richiesta
              </span>
              <span className="font-bold tabular-nums text-foreground">
                {result ? formatCurrency(result.costPerRequest) : '—'}
              </span>
            </div>
            <div className="flex items-baseline justify-between font-mono text-[12px]">
              <span className="text-[10px] uppercase tracking-wide text-muted-foreground">
                richieste/giorno
              </span>
              <span className="font-bold tabular-nums text-foreground">
                {result ? result.totalDailyRequests.toLocaleString('en-US') : '—'}
              </span>
            </div>
          </div>

          {overBudget && (
            <p
              className="flex items-center gap-1.5 font-mono text-[10.5px] text-rose-600 dark:text-rose-400"
              role="status"
            >
              <span aria-hidden className="h-2 w-2 rounded-full bg-rose-500 dark:bg-rose-400" />
              Proiezione supera il budget mensile ({formatCurrency(budget?.monthlyLimit ?? 0)})
            </p>
          )}

          {result?.warnings && result.warnings.length > 0 && (
            <ul className="space-y-0.5 border-t border-dashed border-border/60 pt-2">
              {result.warnings.map(w => (
                <li key={w} className="font-mono text-[10.5px] text-amber-700 dark:text-amber-300">
                  ⚠ {w}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </section>
  );
}
