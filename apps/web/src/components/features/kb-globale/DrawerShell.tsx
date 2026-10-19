/**
 * DrawerShell — FSM orchestrator routing on `state.status`.
 *
 * 5 sub-states (D-L spec-panel 2026-05-30):
 *   idle | streaming | completed | completed-empty | error
 *
 * D-F: citations as numbered list below answer (no inline parsing).
 *
 * Pure presentational — consumes `KbAskStreamState` directly from hook.
 *
 * @see admin-mockups/design_files/sp4-kb-globale.jsx:1968 (DrawerShell)
 */

'use client';

import { type JSX } from 'react';

import { TierLimitRegion } from '@/components/chat-unified/TierLimitNotice';
import { readTierLimit, TIER_LIMIT_EXCEEDED } from '@/lib/tier-limit';

import { DrawerCompleted, type DrawerCompletedLabels } from './DrawerCompleted';
import { DrawerEmpty, type DrawerEmptyLabels } from './DrawerEmpty';
import { DrawerError, type DrawerErrorLabels } from './DrawerError';
import { DrawerIdle, type DrawerIdleLabels } from './DrawerIdle';
import { DrawerStreaming, type DrawerStreamingLabels } from './DrawerStreaming';

import type { KbAskStreamState } from '../../../hooks/useKbAskStream';

export interface DrawerShellLabels {
  title: string;
  subtitle: string;
  closeLabel: string;
  idle: DrawerIdleLabels;
  streaming: DrawerStreamingLabels;
  completed: DrawerCompletedLabels;
  empty: DrawerEmptyLabels;
  error: DrawerErrorLabels;
}

export interface DrawerShellProps {
  state: KbAskStreamState;
  suggestions: readonly string[];
  labels: DrawerShellLabels;
  onAsk: (query: string) => void;
  onStop: () => void;
  onReset: () => void;
  /** #4510: asks the last question again (`useKbAskStream.retry`), from the error state. */
  onRetry: () => void;
  onClose: () => void;
  onEmptyCta: () => void;
  onCopy?: () => void;
  onRegenerate?: () => void;
  /** Citation click handler receiving deep-link payload with optional chunkId (#1702) */
  onCitationClick?: (link: { docId: string; page: number; chunkId?: string }) => void;
}

export function DrawerShell({
  state,
  suggestions,
  labels,
  onAsk,
  onStop,
  onReset,
  onRetry,
  onClose,
  onEmptyCta,
  onCopy,
  onRegenerate,
  onCitationClick,
}: DrawerShellProps): JSX.Element {
  // #4168 (5g-b): la quota del piano non è un guasto. Niente riquadro d'errore e niente «Riprova»,
  // che ripeterebbe una domanda fermata allo stesso modo fino a domani: l'avviso della chat, con
  // i numeri del piano e il link ai piani.
  const tierLimit =
    state.status === 'error' && state.error?.code === TIER_LIMIT_EXCEEDED
      ? readTierLimit(state.error.tierLimit)
      : null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={labels.title}
      data-slot="kb-globale-drawer"
      className="w-[420px] h-[620px] bg-card rounded-xl border border-entity-kb/20 shadow-2xl flex flex-col overflow-hidden"
    >
      {/* Header */}
      <div className="px-4 py-3 border-b border-entity-kb/15 bg-gradient-to-br from-entity-kb/5 to-transparent flex items-center gap-3">
        <span className="w-8 h-8 rounded-sm bg-entity-kb/15 text-entity-kb flex items-center justify-center text-base">
          🤖
        </span>
        <div className="flex-1 min-w-0">
          <div className="font-display font-bold text-sm truncate">{labels.title}</div>
          <div className="font-mono text-[10px] text-muted-foreground">{labels.subtitle}</div>
        </div>
        <button
          type="button"
          aria-label={labels.closeLabel}
          onClick={onClose}
          className="w-7 h-7 rounded-full bg-muted text-muted-foreground hover:text-foreground"
        >
          ✕
        </button>
      </div>

      {/* FSM-routed body */}
      {state.status === 'idle' && (
        <DrawerIdle suggestions={suggestions} onAsk={onAsk} labels={labels.idle} />
      )}
      {state.status === 'streaming' && (
        <DrawerStreaming
          partialText={state.partialText}
          totalTokens={state.totalTokens}
          elapsedMs={state.elapsedMs}
          onStop={onStop}
          labels={labels.streaming}
        />
      )}
      {state.status === 'completed' && (
        <DrawerCompleted
          text={state.partialText}
          citations={state.citations}
          totalTokens={state.totalTokens}
          elapsedMs={state.elapsedMs}
          onCopy={onCopy}
          onRegenerate={onRegenerate}
          labels={labels.completed}
          onCitationClick={onCitationClick}
        />
      )}
      {state.status === 'completed-empty' && (
        <DrawerEmpty onCta={onEmptyCta} labels={labels.empty} />
      )}
      {/* La regione c'è da prima, vuota: l'avviso arriva dopo l'invio e senza che il fuoco si sposti. */}
      <TierLimitRegion limit={tierLimit} className="p-4 pb-0" />
      {tierLimit && (
        <div className="px-4 pt-3">
          {/* Il pannello resta montato quando lo si chiude: senza un modo di lasciare l'avviso,
              riaprirlo lo rimostrerebbe, anche il giorno dopo. */}
          <button
            type="button"
            onClick={onReset}
            className="px-3 py-1.5 rounded-md bg-muted text-xs text-foreground"
          >
            Ho capito
          </button>
        </div>
      )}
      {/* #4510: «Riprova» asks the same question again. It used to send the partial ANSWER as the
          question (or an empty one, which the backend refuses with 422). */}
      {state.status === 'error' && state.error && !tierLimit && (
        <DrawerError
          error={state.error}
          partialText={state.partialText}
          onRetry={onRetry}
          onCancel={onReset}
          labels={labels.error}
        />
      )}
    </div>
  );
}
