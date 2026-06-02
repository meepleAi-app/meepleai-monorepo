'use client';

import React from 'react';

import { ArrowRight, Users, Zap, Activity, Infinity as InfinityIcon, RotateCw } from 'lucide-react';

import type { AiTurnTemplateSuggestion } from '@/lib/api/schemas/toolkit.schemas';

// ── Types ─────────────────────────────────────────────────────────────────────

export interface TurnIndicatorRendererProps {
  /** TurnTemplate from the toolkit. Null if no turn structure is configured. */
  template: AiTurnTemplateSuggestion | null;
  /** Current round (1-indexed). Optional. */
  currentRound?: number;
  /** Current turn within the round (1-indexed). Optional. */
  currentTurn?: number;
  /** Current phase id (when game uses phases). Optional. */
  currentPhaseIndex?: number;
  /** Active player id/name for sequential modes. */
  activePlayer?: { id: string; name: string } | null;
  /** All players, used to highlight everyone in Simultaneous mode. */
  players?: ReadonlyArray<{ id: string; name: string }>;
  'data-testid'?: string;
}

// ── Empty state ───────────────────────────────────────────────────────────────

function TurnEmpty({ testId }: { testId?: string }) {
  return (
    <div
      className="flex min-h-24 flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-border bg-muted/30 p-4 text-muted-foreground"
      data-testid={testId ?? 'turn-indicator-empty'}
    >
      <RotateCw className="h-6 w-6 opacity-30" aria-hidden="true" />
      <p className="text-sm">No turn structure configured for this game.</p>
    </div>
  );
}

// ── Subcomponents ─────────────────────────────────────────────────────────────

function RoundProgress({
  rounds,
  currentRound,
  turnsPerRound,
  currentTurn,
}: {
  rounds: number;
  currentRound?: number;
  turnsPerRound?: number[] | null;
  currentTurn?: number;
}) {
  const round = currentRound ?? 1;
  const turn = currentTurn ?? 1;
  const totalTurns = turnsPerRound?.[round - 1];
  return (
    <div
      className="flex items-center gap-2 rounded-md bg-muted px-3 py-1 text-xs font-mono"
      data-testid="turn-round-progress"
    >
      <span className="font-semibold text-foreground">
        Round {round}/{rounds}
      </span>
      {totalTurns !== undefined && (
        <>
          <span className="text-muted-foreground">·</span>
          <span className="text-muted-foreground">
            Turn {turn}/{totalTurns}
          </span>
        </>
      )}
    </div>
  );
}

function PhaseStepper({
  phases,
  currentPhaseIndex,
}: {
  phases: string[];
  currentPhaseIndex?: number;
}) {
  const idx = currentPhaseIndex ?? 0;
  return (
    <ol
      className="flex flex-wrap items-center gap-1 text-xs"
      data-testid="turn-phase-stepper"
      aria-label="Phases"
    >
      {phases.map((phase, i) => (
        <React.Fragment key={`${i}-${phase}`}>
          <li
            className={`rounded-full px-2 py-1 ${
              i === idx
                ? 'bg-primary text-primary-foreground font-medium'
                : i < idx
                  ? 'bg-muted text-muted-foreground line-through opacity-60'
                  : 'bg-muted text-foreground'
            }`}
            aria-current={i === idx ? 'step' : undefined}
          >
            {phase}
          </li>
          {i < phases.length - 1 && (
            <ArrowRight className="h-3 w-3 shrink-0 text-muted-foreground" aria-hidden="true" />
          )}
        </React.Fragment>
      ))}
    </ol>
  );
}

function ActivePlayerBadge({
  player,
  direction,
}: {
  player: { id: string; name: string } | null | undefined;
  direction?: string | null;
}) {
  if (!player) {
    return (
      <span
        className="rounded-md bg-muted px-2 py-1 text-xs text-muted-foreground"
        data-testid="turn-active-player-empty"
      >
        Waiting for active player…
      </span>
    );
  }
  return (
    <span
      className="inline-flex items-center gap-1 rounded-md bg-primary/10 px-2 py-1 text-xs font-medium text-primary"
      data-testid="turn-active-player"
    >
      <ArrowRight className="h-3 w-3" aria-hidden="true" />
      {player.name}
      {direction && direction !== 'none' && (
        <span className="ml-1 text-[10px] uppercase tracking-wide opacity-70">{direction}</span>
      )}
    </span>
  );
}

function SimultaneousPlayersBadge({
  players,
}: {
  players: ReadonlyArray<{ id: string; name: string }>;
}) {
  return (
    <div className="flex items-center gap-2 text-xs" data-testid="turn-simultaneous-players">
      <Users className="h-4 w-4 text-primary" aria-hidden="true" />
      <span className="text-muted-foreground">All players act simultaneously:</span>
      <span className="flex flex-wrap gap-1">
        {players.map(p => (
          <span key={p.id} className="rounded-full bg-primary/10 px-2 py-0.5 text-primary">
            {p.name}
          </span>
        ))}
      </span>
    </div>
  );
}

function RealtimeBanner() {
  return (
    <div
      className="flex items-center gap-2 rounded-md bg-warning/10 px-3 py-2 text-xs"
      data-testid="turn-realtime-banner"
    >
      <Zap className="h-4 w-4 text-warning" aria-hidden="true" />
      <span className="font-medium text-warning">Real-time play</span>
      <span className="text-muted-foreground">— no turns, react fast.</span>
    </div>
  );
}

function NoneBanner() {
  return (
    <div
      className="flex items-center gap-2 rounded-md bg-muted px-3 py-2 text-xs text-muted-foreground"
      data-testid="turn-none-banner"
    >
      <InfinityIcon className="h-4 w-4" aria-hidden="true" />
      <span>No turn order — open play.</span>
    </div>
  );
}

// ── Per-TurnOrderType layouts ─────────────────────────────────────────────────

interface LayoutContext {
  template: AiTurnTemplateSuggestion;
  currentRound?: number;
  currentTurn?: number;
  currentPhaseIndex?: number;
  activePlayer?: { id: string; name: string } | null;
  players?: ReadonlyArray<{ id: string; name: string }>;
}

function RoundRobinLayout(ctx: LayoutContext) {
  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <ActivePlayerBadge player={ctx.activePlayer} direction={ctx.template.direction} />
        {ctx.template.rounds && (
          <RoundProgress
            rounds={ctx.template.rounds}
            currentRound={ctx.currentRound}
            turnsPerRound={ctx.template.turnsPerRound}
            currentTurn={ctx.currentTurn}
          />
        )}
      </div>
      {ctx.template.phases.length > 0 && (
        <PhaseStepper phases={ctx.template.phases} currentPhaseIndex={ctx.currentPhaseIndex} />
      )}
    </div>
  );
}

function SequentialLayout(ctx: LayoutContext) {
  // Sequential (team-based, e.g. Codenames): same as RoundRobin but phases drive flow more than active player.
  return (
    <div className="space-y-2">
      {ctx.template.phases.length > 0 && (
        <PhaseStepper phases={ctx.template.phases} currentPhaseIndex={ctx.currentPhaseIndex} />
      )}
      {ctx.activePlayer && (
        <ActivePlayerBadge player={ctx.activePlayer} direction={ctx.template.direction} />
      )}
    </div>
  );
}

function SimultaneousLayout(ctx: LayoutContext) {
  return (
    <div className="space-y-2">
      {ctx.players && ctx.players.length > 0 && <SimultaneousPlayersBadge players={ctx.players} />}
      {ctx.template.phases.length > 0 && (
        <PhaseStepper phases={ctx.template.phases} currentPhaseIndex={ctx.currentPhaseIndex} />
      )}
    </div>
  );
}

function RealtimeLayout(ctx: LayoutContext) {
  return (
    <div className="space-y-2">
      <RealtimeBanner />
      {ctx.template.phases.length > 0 && (
        <PhaseStepper phases={ctx.template.phases} currentPhaseIndex={ctx.currentPhaseIndex} />
      )}
    </div>
  );
}

function NoneLayout(_ctx: LayoutContext) {
  return <NoneBanner />;
}

function CustomLayout(ctx: LayoutContext) {
  // Custom/Free: render whatever info we have without strong assumptions
  return (
    <div className="space-y-2">
      <span
        className="inline-flex items-center gap-1 rounded-md bg-muted px-2 py-1 text-xs text-muted-foreground"
        data-testid="turn-custom-indicator"
      >
        <Activity className="h-3 w-3" aria-hidden="true" />
        Custom turn order
      </span>
      {ctx.template.phases.length > 0 && (
        <PhaseStepper phases={ctx.template.phases} currentPhaseIndex={ctx.currentPhaseIndex} />
      )}
      {ctx.activePlayer && (
        <ActivePlayerBadge player={ctx.activePlayer} direction={ctx.template.direction} />
      )}
    </div>
  );
}

// ── Lookup table for dispatch ─────────────────────────────────────────────────

type LayoutFn = (ctx: LayoutContext) => React.ReactElement;

const LAYOUT_REGISTRY: Readonly<Record<string, LayoutFn>> = Object.freeze({
  RoundRobin: RoundRobinLayout,
  Sequential: SequentialLayout,
  Simultaneous: SimultaneousLayout,
  Realtime: RealtimeLayout,
  None: NoneLayout,
  Custom: CustomLayout,
  Free: CustomLayout,
});

// ── Main dispatcher ───────────────────────────────────────────────────────────

/**
 * TurnIndicatorRenderer — polymorphic UI for the TurnTemplate of a toolkit.
 *
 * Issue #1749 (B19-4a). Switches on TurnOrderType to render the appropriate
 * indicator for each turn pattern:
 *
 * - **RoundRobin**: active player badge + round progress (Round X/Y · Turn N/M)
 *   when v3 Rounds/TurnsPerRound are provided. Phase stepper if Phases present.
 * - **Sequential** (team-based): phase stepper takes precedence over active player.
 * - **Simultaneous**: shows all players highlighted together (co-op like Paleo).
 * - **Realtime**: warning banner — no turns, time-pressure play.
 * - **None**: open-play indicator.
 * - **Custom / Free**: generic custom-turn indicator + phases if any.
 *
 * Unknown TurnOrderType falls back to Custom layout.
 */
export function TurnIndicatorRenderer({
  template,
  currentRound,
  currentTurn,
  currentPhaseIndex,
  activePlayer,
  players,
  'data-testid': testId,
}: TurnIndicatorRendererProps) {
  if (!template) {
    return <TurnEmpty testId={testId} />;
  }

  const ctx: LayoutContext = {
    template,
    currentRound,
    currentTurn,
    currentPhaseIndex,
    activePlayer,
    players,
  };

  const layout = LAYOUT_REGISTRY[template.turnOrderType] ?? CustomLayout;

  return (
    <section
      className="flex flex-col gap-2"
      data-testid={testId ?? 'turn-indicator'}
      data-turn-order-type={template.turnOrderType}
      aria-label="Turn indicator"
    >
      {layout(ctx)}
    </section>
  );
}
