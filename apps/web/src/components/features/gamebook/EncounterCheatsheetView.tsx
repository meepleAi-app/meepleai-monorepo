/**
 * EncounterCheatsheetView — Issue #1484 (consumes BE #1520).
 *
 * Mapped from `admin-mockups/design_files/librogame-runthrough-encounter-cheatsheet.html`.
 * Renders the on-demand Encounter Book cheatsheet for the Nanolith libro-game
 * runthrough. Pure component: all i18n strings are injected via `labels` and
 * the FSM is driven entirely by props (the orchestrator owns the parse hook).
 *
 * FSM (parse-centric MVP — spec hardened 2026-05-25):
 *   idle     → entry CTA (+ optional story context) that triggers `onParse`
 *   parsing  → busy loading indicator (+ optional cancel)
 *   rendered → cheatsheet card (enemy stats, options w/ dice, win/loss, confidence)
 *   error    → recoverable message (parse-failed 409 / not-found 404 / generic) + retry
 *
 * State D (resolved/consequences) is intentionally out of scope: there is no BE
 * encounter-resolution command, so `onResolve` navigates back to the play
 * session where dice/save-state mechanics live (mirrors the mockup's "Risolvi"
 * which navigates away).
 *
 * Ephemeral by design (spec §9.1): nothing is persisted; the card lives only
 * for the active session. Confidence < 0.6 on any cluster surfaces a manual-
 * verification hint (§9.1 forced-manual fallback).
 *
 * Encounter accent = `entity-event` (mockup `--c-event`). data-slot attributes
 * support E2E selectors.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { EncounterCheatsheet, EncounterDiceRoll } from '@/lib/api/gamebook-encounter';
import { classifyConfidence } from '@/lib/gamebook-upload';

import { ConfidenceBadge, type ConfidenceBadgeLabels } from './ConfidenceBadge';

export type EncounterStatus = 'idle' | 'parsing' | 'rendered' | 'error';

export type EncounterErrorKind = 'parse-failed' | 'not-found' | 'generic';

/** Confidence threshold below which a cluster forces manual verification (§9.1). */
const MANUAL_INPUT_THRESHOLD = 0.6;

export interface EncounterStoryContext {
  /** Originating Storybook paragraph marker, e.g. "§147". */
  readonly paragraphLabel: string;
  /** Short narrative excerpt that referenced the encounter. */
  readonly excerpt: string;
}

export interface EncounterCheatsheetLabels {
  // entry (A)
  readonly entryStoryMeta: string;
  readonly entryRefTitle: string;
  readonly entryRefHint: string;
  readonly entryCta: string;
  readonly entryCtaHint: string;
  // parsing (B)
  readonly loadingTitle: string;
  readonly loadingHint: string;
  readonly cancel: string;
  // rendered (C)
  readonly optionsTitle: string;
  readonly conditionsWin: string;
  readonly conditionsLoss: string;
  readonly parseConfidence: string;
  readonly lowConfidenceHint: string;
  readonly ephemeralNote: string;
  readonly retake: string;
  readonly glossary: string;
  readonly resolve: string;
  // error
  readonly errorParseFailed: string;
  readonly errorNotFound: string;
  readonly errorGeneric: string;
  readonly retry: string;
  // reused ConfidenceBadge SR labels
  readonly confidence: ConfidenceBadgeLabels;
}

export interface EncounterCheatsheetViewProps {
  readonly status: EncounterStatus;
  readonly cheatsheet: EncounterCheatsheet | null;
  readonly errorKind?: EncounterErrorKind;
  readonly storyContext?: EncounterStoryContext | null;
  readonly labels: EncounterCheatsheetLabels;
  /** Entry CTA + retry + retake all (re)trigger the parse. */
  readonly onParse: () => void;
  /** Navigate back to the play session to resolve the encounter (state D). */
  readonly onResolve: () => void;
  readonly onOpenGlossary?: () => void;
  readonly onCancel?: () => void;
  readonly className?: string;
}

/** Formats a dice roll like "1d6+2" (modifier omitted when zero). */
function formatDice(d: EncounterDiceRoll): string {
  const mod = d.modifier > 0 ? `+${d.modifier}` : d.modifier < 0 ? `${d.modifier}` : '';
  return `${d.count}d${d.sides}${mod}`;
}

export function EncounterCheatsheetView({
  status,
  cheatsheet,
  errorKind = 'generic',
  storyContext,
  labels,
  onParse,
  onResolve,
  onOpenGlossary,
  onCancel,
  className,
}: EncounterCheatsheetViewProps): ReactElement {
  return (
    <section
      data-slot="encounter-cheatsheet-view"
      data-status={status}
      className={clsx('mx-auto flex w-full max-w-[640px] flex-col gap-3 p-4', className)}
    >
      {status === 'idle' && (
        <EntryState storyContext={storyContext} labels={labels} onParse={onParse} />
      )}
      {status === 'parsing' && <LoadingState labels={labels} onCancel={onCancel} />}
      {status === 'error' && <ErrorState errorKind={errorKind} labels={labels} onParse={onParse} />}
      {status === 'rendered' && cheatsheet && (
        <RenderedState
          cheatsheet={cheatsheet}
          labels={labels}
          onParse={onParse}
          onResolve={onResolve}
          onOpenGlossary={onOpenGlossary}
        />
      )}
    </section>
  );
}

// ---------------------------------------------------------------------------
// State A — entry from story
// ---------------------------------------------------------------------------

function EntryState({
  storyContext,
  labels,
  onParse,
}: {
  storyContext?: EncounterStoryContext | null;
  labels: EncounterCheatsheetLabels;
  onParse: () => void;
}): ReactElement {
  return (
    <div data-slot="encounter-entry" className="flex flex-col gap-4">
      {storyContext && (
        <div className="rounded-xl border border-border bg-card p-4">
          <div className="mb-2 flex items-center gap-2">
            <span className="font-mono text-2xl font-bold tabular-nums text-entity-event">
              {storyContext.paragraphLabel}
            </span>
            <span className="font-mono text-xs text-muted-foreground">{labels.entryStoryMeta}</span>
          </div>
          <p className="text-sm italic leading-relaxed text-muted-foreground">
            {storyContext.excerpt}
          </p>
        </div>
      )}

      <div className="flex items-center gap-3 rounded-lg border border-dashed border-entity-event/40 bg-entity-event/8 p-3">
        <span aria-hidden="true" className="text-2xl">
          ⚔️
        </span>
        <div className="flex-1 text-sm">
          <strong className="block font-bold text-entity-event">{labels.entryRefTitle}</strong>
          <span className="text-muted-foreground">{labels.entryRefHint}</span>
        </div>
      </div>

      <button
        type="button"
        onClick={onParse}
        aria-label={labels.entryCta}
        data-slot="encounter-entry-cta"
        className={clsx(
          'flex items-center justify-between gap-3 rounded-lg bg-entity-event px-5 py-4 text-left text-white shadow-sm',
          'font-bold transition-colors hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring motion-reduce:transition-none'
        )}
      >
        <span aria-hidden="true" className="text-2xl">
          📷
        </span>
        <span className="flex-1">
          <span className="block">{labels.entryCta}</span>
          <span className="mt-0.5 block text-xs font-normal opacity-90">{labels.entryCtaHint}</span>
        </span>
        <span aria-hidden="true">›</span>
      </button>
    </div>
  );
}

// ---------------------------------------------------------------------------
// State B — photo segmenting / parsing
// ---------------------------------------------------------------------------

function LoadingState({
  labels,
  onCancel,
}: {
  labels: EncounterCheatsheetLabels;
  onCancel?: () => void;
}): ReactElement {
  return (
    <div
      data-slot="encounter-loading"
      role="status"
      aria-busy="true"
      className="flex flex-col items-center gap-4 py-10 text-center"
    >
      <span
        aria-hidden="true"
        className="h-10 w-10 animate-spin rounded-full border-2 border-entity-event/30 border-t-entity-event motion-reduce:animate-none"
      />
      <div>
        <h3 className="font-bold text-foreground">{labels.loadingTitle}</h3>
        <p className="mt-1 text-sm text-muted-foreground">{labels.loadingHint}</p>
      </div>
      {onCancel && (
        <button
          type="button"
          onClick={onCancel}
          className="rounded-md border border-border bg-muted px-3 py-2 text-sm font-semibold text-foreground hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {labels.cancel}
        </button>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Error
// ---------------------------------------------------------------------------

function ErrorState({
  errorKind,
  labels,
  onParse,
}: {
  errorKind: EncounterErrorKind;
  labels: EncounterCheatsheetLabels;
  onParse: () => void;
}): ReactElement {
  const message =
    errorKind === 'parse-failed'
      ? labels.errorParseFailed
      : errorKind === 'not-found'
        ? labels.errorNotFound
        : labels.errorGeneric;

  return (
    <div
      data-slot="encounter-error"
      role="alert"
      className="flex flex-col items-center gap-4 rounded-lg border border-destructive/30 bg-destructive/10 p-6 text-center"
    >
      <span aria-hidden="true" className="text-3xl">
        ⚠️
      </span>
      <p className="text-sm text-foreground">{message}</p>
      <button
        type="button"
        onClick={onParse}
        className="rounded-md bg-entity-event px-4 py-2 text-sm font-semibold text-white hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {labels.retry}
      </button>
    </div>
  );
}

// ---------------------------------------------------------------------------
// State C — cheatsheet rendered
// ---------------------------------------------------------------------------

function RenderedState({
  cheatsheet,
  labels,
  onParse,
  onResolve,
  onOpenGlossary,
}: {
  cheatsheet: EncounterCheatsheet;
  labels: EncounterCheatsheetLabels;
  onParse: () => void;
  onResolve: () => void;
  onOpenGlossary?: () => void;
}): ReactElement {
  const { enemies, options, conditions, confidence } = cheatsheet;
  const optionsLevel = classifyConfidence(confidence.options) ?? 'low';
  const minConfidence = Math.min(confidence.enemies, confidence.options, confidence.conditions);
  const lowConfidence = minConfidence < MANUAL_INPUT_THRESHOLD;

  return (
    <div className="flex flex-col gap-3">
      {enemies.map((enemy, i) => (
        <div
          key={`${enemy.name}-${i}`}
          data-slot="encounter-enemy"
          className="overflow-hidden rounded-xl border border-entity-event/30 bg-card"
        >
          <div className="flex items-center gap-3 border-b border-entity-event/20 bg-entity-event/8 px-4 py-3">
            <span aria-hidden="true" className="text-3xl">
              {enemy.icon ?? '⚔️'}
            </span>
            <span className="flex-1 text-lg font-bold text-foreground">{enemy.name}</span>
            {enemy.paragraphMarker && (
              <span className="rounded-full bg-entity-event/15 px-2 py-0.5 font-mono text-sm font-bold text-entity-event">
                {enemy.paragraphMarker}
              </span>
            )}
          </div>
          <div className="grid grid-cols-4 gap-px bg-border">
            <Stat value={enemy.hp} label="HP" />
            <Stat value={enemy.atk} label="ATK" />
            <Stat value={enemy.def} label="DEF" />
            <Stat value={enemy.mov} label="MOV" />
          </div>
        </div>
      ))}

      <div className="flex items-center justify-between px-1">
        <span className="text-sm font-bold text-foreground">{labels.optionsTitle}</span>
        <span data-slot="encounter-confidence" className="inline-flex items-center gap-1.5">
          <ConfidenceBadge level={optionsLevel} labels={labels.confidence} />
          <span className="font-mono text-[10px] font-bold tabular-nums text-muted-foreground">
            {labels.parseConfidence} {confidence.options.toFixed(2)}
          </span>
        </span>
      </div>

      <ul className="flex flex-col gap-2">
        {options.map((option, i) => (
          <li key={`${option.label}-${i}`}>
            <div
              data-slot="encounter-option"
              className="flex items-center gap-3 rounded-md border border-border bg-card px-4 py-3"
            >
              {option.diceRoll && (
                <span
                  aria-hidden="true"
                  className="flex h-11 w-11 shrink-0 flex-col items-center justify-center rounded-md bg-entity-event/10 font-mono text-xs font-bold leading-tight text-entity-event"
                >
                  <span>{formatDice(option.diceRoll)}</span>
                  <span>≥ {option.diceRoll.threshold}</span>
                </span>
              )}
              <span className="flex-1">
                <span className="block text-sm font-semibold text-foreground">{option.label}</span>
                {option.outcome && (
                  <span className="mt-0.5 block font-mono text-xs text-muted-foreground">
                    {option.outcome}
                  </span>
                )}
              </span>
            </div>
          </li>
        ))}
      </ul>

      <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        {conditions.win && (
          <div className="rounded-md border border-success/30 bg-success/10 p-3">
            <span className="mb-1 block font-mono text-[10px] font-bold uppercase tracking-wide text-success">
              {labels.conditionsWin}
            </span>
            <span className="block text-sm leading-snug text-foreground">{conditions.win}</span>
          </div>
        )}
        {conditions.loss && (
          <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3">
            <span className="mb-1 block font-mono text-[10px] font-bold uppercase tracking-wide text-destructive">
              {labels.conditionsLoss}
            </span>
            <span className="block text-sm leading-snug text-foreground">{conditions.loss}</span>
          </div>
        )}
      </div>

      {lowConfidence && (
        <p
          data-slot="encounter-low-confidence"
          className="rounded-md border border-dashed border-warning/40 bg-warning/10 p-3 font-mono text-xs text-warning"
        >
          {labels.lowConfidenceHint}
        </p>
      )}

      <p className="rounded-md border border-dashed border-border bg-muted px-3 py-2 font-mono text-xs text-muted-foreground">
        {labels.ephemeralNote}
      </p>

      <div
        data-slot="encounter-toolbar"
        className="flex flex-wrap items-center justify-between gap-2 border-t border-border pt-3"
      >
        <button
          type="button"
          onClick={onParse}
          className="rounded-md border border-border bg-card px-3 py-2 text-sm font-semibold text-foreground hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {labels.retake}
        </button>
        {onOpenGlossary && (
          <button
            type="button"
            onClick={onOpenGlossary}
            className="rounded-md border border-border bg-muted px-3 py-2 text-sm font-semibold text-foreground hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {labels.glossary}
          </button>
        )}
        <button
          type="button"
          onClick={onResolve}
          className="rounded-md bg-entity-event px-4 py-2 text-sm font-semibold text-white hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {labels.resolve}
        </button>
      </div>
    </div>
  );
}

function Stat({ value, label }: { value: string; label: string }): ReactElement {
  return (
    <div className="bg-card p-3 text-center">
      <span className="block font-mono text-2xl font-bold leading-none text-entity-event">
        {value}
      </span>
      <span className="mt-1 block font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
        {label}
      </span>
    </div>
  );
}
