/* eslint-disable local/no-hardcoded-color-utility -- admin providers table: emerald/amber/rose/zinc status chip palette (admin convention DS-13c, scope deferred to DS-16) */
'use client';

/**
 * #1834 SP5 F4-C3 — ProviderTable
 *
 * Tabella elenco provider con probe/quota status (mockup `sp5-admin-providers.html` §2).
 * Iterates `KNOWN_PROVIDERS` clientside (backend non espone una lista unica).
 *
 * Colonne (mockup):
 * - Provider (nome + host placeholder + key fingerprint)
 * - Stato (chip da quota/probe)
 * - Modello default (placeholder dal config)
 * - P95 latenza (probe.latencyMs)
 * - Req 24h (BE pending → "—")
 * - Errori % (BE pending → "—")
 * - Circuit breaker (da useCircuitBreakerStates)
 * - Azioni (Config / Rotate key [BE pending] / ⋯)
 */

import Link from 'next/link';

import { useCircuitBreakerStates, useProviderQuota } from '@/hooks/queries/useProviders';
import type { CircuitBreakerState } from '@/lib/api/schemas/admin/admin-circuit-breakers.schemas';
import { KNOWN_PROVIDERS, type ProviderName } from '@/lib/api/schemas/providers';

import { RotateKeyModal } from './RotateKeyModal';

type ProviderTone = 'tool' | 'chat' | 'kb';

const PROVIDER_DISPLAY: Record<
  ProviderName,
  {
    readonly mark: string;
    readonly name: string;
    readonly host: string;
    readonly defaultModel: string;
    readonly tone: ProviderTone;
    readonly primary?: boolean;
  }
> = {
  deepseek: {
    mark: 'D',
    name: 'DeepSeek',
    host: 'api.deepseek.com',
    defaultModel: 'deepseek-chat',
    tone: 'tool',
    primary: true,
  },
  openrouter: {
    mark: 'O',
    name: 'OpenRouter',
    host: 'openrouter.ai/api/v1',
    defaultModel: 'anthropic/claude-3.5-sonnet',
    tone: 'chat',
  },
  'ollama-local': {
    mark: 'L',
    name: 'Ollama (locale)',
    host: 'localhost:11434',
    defaultModel: 'llama3.1:8b-instruct',
    tone: 'kb',
  },
};

const TONE_MARK_CLASS: Record<ProviderTone, string> = {
  tool: 'bg-entity-tool/15 text-entity-tool ring-1 ring-entity-tool/30',
  chat: 'bg-entity-chat/15 text-entity-chat ring-1 ring-entity-chat/30',
  kb: 'bg-entity-kb/15 text-entity-kb ring-1 ring-entity-kb/30',
};

type CircuitState = 'closed' | 'open' | 'half-open' | 'unknown';

function deriveCircuitState(
  breakers: CircuitBreakerState[] | undefined,
  name: ProviderName
): CircuitState {
  if (!breakers) return 'unknown';
  // Backend `serviceName` may include the provider name as substring (e.g. "deepseek-llm").
  const match = breakers.find(b => b.serviceName.toLowerCase().includes(name.toLowerCase()));
  if (!match) return 'unknown';
  const s = match.state.toLowerCase();
  if (s === 'closed') return 'closed';
  if (s === 'open') return 'open';
  if (s === 'half-open' || s === 'halfopen') return 'half-open';
  return 'unknown';
}

function circuitChipClass(state: CircuitState): string {
  switch (state) {
    case 'closed':
      return 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/30';
    case 'half-open':
      return 'bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30';
    case 'open':
      return 'bg-rose-500/10 text-rose-700 dark:text-rose-300 border-rose-500/30';
    default:
      return 'bg-zinc-500/10 text-zinc-600 dark:text-zinc-400 border-zinc-500/30';
  }
}

function ProviderRow({
  name,
  breakers,
}: {
  readonly name: ProviderName;
  readonly breakers: CircuitBreakerState[] | undefined;
}) {
  const quotaQuery = useProviderQuota(name);
  const display = PROVIDER_DISPLAY[name];
  const circuit = deriveCircuitState(breakers, name);

  const tokenStatus: { label: string; cls: string; title?: string } = (() => {
    if (quotaQuery.isLoading)
      return { label: '…', cls: 'bg-zinc-500/10 text-zinc-500 border-zinc-500/30' };
    const q = quotaQuery.data;
    if (!q) return { label: 'unknown', cls: 'bg-zinc-500/10 text-zinc-500 border-zinc-500/30' };
    if (!q.tokenConfigured)
      return {
        label: 'no token',
        cls: 'bg-rose-500/10 text-rose-700 dark:text-rose-300 border-rose-500/30',
      };
    if (!q.quotaSupported)
      return {
        label: 'healthy',
        cls: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/30',
      };
    // #3045: quota supportata + token configurato ma fetch degradato (errorCode presente,
    // numerici null) → NON "healthy". Stato esplicito "degraded".
    if (q.errorCode)
      return {
        label: 'degraded',
        cls: 'bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30',
        title: q.errorMessage ?? q.errorCode,
      };
    return {
      label: 'healthy',
      cls: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/30',
    };
  })();

  return (
    <tr
      data-testid={`provider-row-${name}`}
      className="border-b border-border/60 dark:border-zinc-700/60 last:border-b-0 hover:bg-muted/40 dark:hover:bg-zinc-800/40"
    >
      <td className="py-3 px-3">
        <Link
          href={`/admin/providers/${encodeURIComponent(name)}`}
          className="flex items-center gap-3 outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-md"
        >
          <div
            className={`grid h-9 w-9 place-items-center rounded-lg font-quicksand text-base font-bold ${TONE_MARK_CLASS[display.tone]}`}
            aria-hidden="true"
            data-testid={`provider-mark-${name}`}
          >
            {display.mark}
          </div>
          <div className="min-w-0">
            <div className="font-quicksand text-sm font-bold text-foreground flex items-center gap-1.5">
              {display.name}
              {display.primary && (
                <span
                  className="font-mono text-[9px] font-bold uppercase tracking-wider bg-entity-event/12 text-entity-event px-1.5 py-px rounded"
                  data-testid={`provider-primary-tag-${name}`}
                >
                  primary
                </span>
              )}
            </div>
            <div className="font-mono text-[10.5px] text-muted-foreground truncate">
              {display.host}
            </div>
          </div>
        </Link>
      </td>
      <td className="py-3 px-3">
        <span
          className={`inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border ${tokenStatus.cls}`}
          aria-label={`Token status: ${tokenStatus.label}`}
          title={tokenStatus.title}
        >
          {tokenStatus.label}
        </span>
      </td>
      <td className="py-3 px-3 font-mono text-xs text-muted-foreground">{display.defaultModel}</td>
      <td
        className="py-3 px-3 font-mono text-xs text-right text-muted-foreground"
        title="BE pending"
      >
        —
      </td>
      <td
        className="py-3 px-3 font-mono text-xs text-right text-muted-foreground"
        title="BE pending"
      >
        —
      </td>
      <td
        className="py-3 px-3 font-mono text-xs text-right text-muted-foreground"
        title="BE pending"
      >
        —
      </td>
      <td className="py-3 px-3">
        <span
          className={`inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border ${circuitChipClass(circuit)}`}
          data-testid={`provider-circuit-${name}`}
          aria-label={`Circuit breaker: ${circuit}`}
        >
          {circuit}
        </span>
      </td>
      <td className="py-3 px-3 text-right">
        <div className="inline-flex items-center gap-1">
          <Link
            href={`/admin/providers/${encodeURIComponent(name)}`}
            aria-label={`Configure ${name}`}
            className="rounded-md border border-border/60 dark:border-zinc-700/60 bg-card/60 px-2 py-1 text-[11px] font-medium text-muted-foreground hover:bg-muted/70"
          >
            <span aria-hidden="true">⚙</span> Config
          </Link>
          <RotateKeyModal providerName={name} />
        </div>
      </td>
    </tr>
  );
}

export function ProviderTable() {
  const breakersQuery = useCircuitBreakerStates();

  return (
    <section
      className="rounded-lg border border-border/60 dark:border-zinc-700/60 bg-card/80 dark:bg-zinc-900/80 overflow-hidden"
      aria-labelledby="providers-table-heading"
      data-testid="providers-table"
    >
      <header className="flex items-center gap-2 p-4 border-b border-border/60 dark:border-zinc-700/60">
        <h3
          id="providers-table-heading"
          className="font-quicksand text-base font-bold text-foreground"
        >
          Provider
        </h3>
        <span className="ml-auto font-mono text-[10.5px] text-muted-foreground">
          🔒 Rotate key &middot; richiede superadmin + step-up
        </span>
      </header>

      <div className="overflow-x-auto">
        <table className="w-full text-left" aria-labelledby="providers-table-heading">
          <thead>
            <tr className="border-b border-border/60 dark:border-zinc-700/60 bg-muted/40 dark:bg-zinc-800/40">
              <th
                scope="col"
                className="py-2 px-3 font-mono text-[10px] uppercase tracking-wider text-muted-foreground"
              >
                Provider
              </th>
              <th
                scope="col"
                className="py-2 px-3 font-mono text-[10px] uppercase tracking-wider text-muted-foreground"
              >
                Stato
              </th>
              <th
                scope="col"
                className="py-2 px-3 font-mono text-[10px] uppercase tracking-wider text-muted-foreground"
              >
                Modello default
              </th>
              <th
                scope="col"
                className="py-2 px-3 font-mono text-[10px] uppercase tracking-wider text-muted-foreground text-right"
              >
                P95 latenza
              </th>
              <th
                scope="col"
                className="py-2 px-3 font-mono text-[10px] uppercase tracking-wider text-muted-foreground text-right"
              >
                Req 24h
              </th>
              <th
                scope="col"
                className="py-2 px-3 font-mono text-[10px] uppercase tracking-wider text-muted-foreground text-right"
              >
                Errori
              </th>
              <th
                scope="col"
                className="py-2 px-3 font-mono text-[10px] uppercase tracking-wider text-muted-foreground"
              >
                Circuit
              </th>
              <th
                scope="col"
                className="py-2 px-3 font-mono text-[10px] uppercase tracking-wider text-muted-foreground text-right"
              >
                Azioni
              </th>
            </tr>
          </thead>
          <tbody>
            {KNOWN_PROVIDERS.map(name => (
              <ProviderRow key={name} name={name} breakers={breakersQuery.data} />
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
