/* eslint-disable local/no-hardcoded-color-utility -- admin routing chain: amber/cyan/teal/zinc priority palette (admin convention DS-13c, scope deferred to DS-16) */
'use client';

/**
 * #1834 SP5 F4-C3 — RoutingChainViz
 *
 * Visualizzazione catena di fallback LLM (mockup `sp5-admin-providers.html` §3).
 *
 * PR2: parsing migrato a Zod schema condiviso (`parseFallbackChain`).
 * Arrow tra nodi mostra le `failoverConditions` ("on 429 / 5xx / timeout").
 *
 * Schema tollerante a varianti BE (vedi `FallbackChainEntrySchema`); follow-up
 * BE per schema rigido + persistenza tipata.
 */

import { useMemo } from 'react';

import { useLlmSystemConfig } from '@/hooks/queries/useProviders';
import {
  parseFallbackChain,
  type FallbackChainEntry,
} from '@/lib/api/schemas/llm-system-config.schemas';

function priorityChipClass(priority: FallbackChainEntry['priority']): string {
  switch (priority) {
    case 'primary':
      return 'bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/40';
    case 'secondary':
      return 'bg-cyan-500/15 text-cyan-700 dark:text-cyan-300 border-cyan-500/40';
    case 'tertiary':
      return 'bg-teal-500/15 text-teal-700 dark:text-teal-300 border-teal-500/40';
    case 'standby':
      return 'bg-zinc-500/15 text-zinc-700 dark:text-zinc-300 border-zinc-500/40';
    default:
      return 'bg-zinc-500/10 text-zinc-500 border-zinc-500/30';
  }
}

function formatConditions(conditions: readonly string[]): string {
  if (conditions.length === 0) return 'on any failure';
  return `on ${conditions.join(' / ')}`;
}

function ChainArrow({ conditions }: { readonly conditions: readonly string[] }) {
  return (
    <div
      className="hidden sm:flex flex-col items-center justify-center gap-1 px-2"
      aria-hidden="true"
      data-testid="routing-chain-arrow"
    >
      <span className="font-mono text-lg text-muted-foreground">→</span>
      <span
        className="font-mono text-[9.5px] uppercase tracking-wider text-amber-700 dark:text-amber-300 bg-amber-500/10 border border-amber-500/30 rounded-full px-2 py-px whitespace-nowrap"
        data-testid="routing-chain-condition"
      >
        {formatConditions(conditions)}
      </span>
    </div>
  );
}

export function RoutingChainViz() {
  const configQuery = useLlmSystemConfig();
  const chain = useMemo(
    () => parseFallbackChain(configQuery.data?.fallbackChainJson),
    [configQuery.data?.fallbackChainJson]
  );

  return (
    <section
      className="rounded-lg border border-border/60 dark:border-zinc-700/60 bg-card/80 dark:bg-zinc-900/80 p-4"
      aria-labelledby="routing-chain-heading"
      data-testid="routing-chain"
    >
      <header className="flex items-center gap-2 mb-3">
        <h3
          id="routing-chain-heading"
          className="font-quicksand text-base font-bold text-foreground"
        >
          Routing chain
        </h3>
        <span className="font-mono text-[10.5px] text-muted-foreground">
          {chain.length > 0
            ? chain.length === 1
              ? '1 hop'
              : `${chain.length - 1 === 0 ? '' : `${chain.length - 1} fallback · `}${chain.length} hops`
            : 'fallback'}
        </span>
        {configQuery.data && (
          <span className="ml-auto font-mono text-[10.5px] text-muted-foreground">
            source: {configQuery.data.source}
          </span>
        )}
      </header>

      {configQuery.isLoading && (
        <div data-testid="routing-chain-loading" className="text-sm text-muted-foreground">
          Caricamento configurazione…
        </div>
      )}

      {configQuery.isError && (
        <div data-testid="routing-chain-error" className="text-sm text-rose-700 dark:text-rose-300">
          Errore nel caricamento della configurazione LLM.
        </div>
      )}

      {!configQuery.isLoading && chain.length === 0 && (
        <div data-testid="routing-chain-empty" className="text-sm text-muted-foreground">
          Nessuna catena di fallback configurata.{' '}
          <span className="font-mono text-[10.5px]">
            (fallbackChainJson: {configQuery.data?.fallbackChainJson ? 'invalid' : 'empty'})
          </span>
        </div>
      )}

      {chain.length > 0 && (
        <div className="flex flex-col sm:flex-row gap-2 sm:gap-3 items-stretch sm:items-center flex-wrap">
          {chain.map((node, idx) => {
            const nextNode = chain[idx + 1];
            // Arrow conditions are the conditions that trigger transition to NEXT node.
            // They come from the *next* node's `failoverConditions` (what makes us fall there).
            const arrowConditions = nextNode?.failoverConditions ?? [];
            return (
              <div key={`${node.provider}-${idx}`} className="flex items-center gap-2 sm:gap-3">
                <div
                  className="rounded-lg border border-border/60 dark:border-zinc-700/60 bg-card/60 px-3 py-2 min-w-[180px]"
                  data-testid={`routing-chain-node-${idx}`}
                >
                  <span
                    className={`inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border ${priorityChipClass(node.priority)}`}
                    data-testid={`routing-chain-priority-${idx}`}
                  >
                    {node.priority}
                  </span>
                  <div className="mt-1.5 font-quicksand text-sm font-bold text-foreground">
                    {node.provider}
                  </div>
                  <div className="font-mono text-[10.5px] text-muted-foreground truncate">
                    {node.model}
                  </div>
                </div>
                {nextNode && <ChainArrow conditions={arrowConditions} />}
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}
