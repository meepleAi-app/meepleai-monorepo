/**
 * Presentational mocks for ResumeBooksList aspirational states.
 *
 * forward-refactor: two mockup states describe UI branches that do not yet exist
 * as props on the real ResumeBooksList component:
 *
 *   StaleWarning (state-04):
 *     The mockup shows an amber warning card when a single campaign has been
 *     inactive for > 60 days, with three CTAs: "Riprendi comunque", "Archivia
 *     + Nuova campagna", "Solo nuova (mantieni archivio)".
 *     The real component has no `isStale` / `staleDaysThreshold` prop.
 *     Replace with the real StaleWarningCard component when it is implemented.
 *
 *   WithTutorial (state-05):
 *     The mockup shows a variant of the single-resume hero that surfaces a
 *     "📚 Tutorial pronto" badge and a secondary "Tutorial" CTA when
 *     `DocumentCategory.QuickStart` is available for the game.
 *     The real component has no `tutorialAvailable` / `tutorialSteps` prop.
 *     Replace with the real ResumeHeroWithTutorial component when implemented.
 *
 * Uses canonical design tokens only (no hardcoded neutral utilities forbidden by
 * the `local/no-hardcoded-color-utility` ESLint rule).
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-resume-picker.html
 *   - State 04 · Stale Warning (amber card + 3 CTAs)
 *   - State 05 · Single Resume + Tutorial (kb badge + Tutorial CTA)
 *
 * Refs: DS-17 Phase D-2, umbrella #2063, sub-issue #2174.
 */

import type { ReactElement } from 'react';

// ── ResumePickerStaleMock — stale warning amber card ────────────────────────

export interface ResumePickerStaleMockProps {
  /** Campaign name displayed in the warning heading. */
  readonly campaignName?: string;
  /** ISO date when the campaign was started. */
  readonly startedAt?: string;
  /** ISO date of the last session. */
  readonly lastSessionAt?: string;
  /** Formatted last-read paragraph (e.g. "§289"). */
  readonly lastLocation?: string;
  /** Number of glossary terms recorded for the campaign. */
  readonly glossaryTerms?: number;
  /** Names of party members including the current user. */
  readonly party?: string[];
  /** How many days since the last session. */
  readonly daysSinceLastSession?: number;
  readonly onResume?: () => void;
  readonly onArchiveAndNew?: () => void;
  readonly onNewOnly?: () => void;
}

export function ResumePickerStaleMock({
  campaignName = 'Campagna con i ragazzi',
  startedAt = '5 marzo 2026',
  lastSessionAt = '9 marzo 2026',
  lastLocation = '§289',
  glossaryTerms = 12,
  party = ['Marco', 'Giulia', 'Luca', 'Aaron'],
  daysSinceLastSession = 60,
  onResume,
  onArchiveAndNew,
  onNewOnly,
}: ResumePickerStaleMockProps): ReactElement {
  return (
    <div
      data-slot="resume-picker-stale-mock"
      data-theme="light"
      className="min-h-screen bg-background p-4"
    >
      {/* App bar */}
      <header className="mb-4 flex items-center gap-3 border-b border-border pb-3">
        <button
          type="button"
          aria-label="Torna a Eldoria"
          className="flex h-9 w-9 items-center justify-center rounded-md bg-muted text-lg"
        >
          ←
        </button>
        <span className="font-mono text-xs text-muted-foreground">
          <strong className="font-semibold text-foreground">Eldoria</strong> · Libro game
        </span>
      </header>

      {/* Stale warning card */}
      <article
        role="alert"
        aria-labelledby="stale-heading"
        className="rounded-xl border border-[hsl(var(--c-warning)/0.3)] bg-[hsl(var(--c-warning)/0.06)] p-5"
      >
        {/* Badge */}
        <span className="mb-3 inline-flex items-center gap-1 rounded-full border border-[hsl(var(--c-warning)/0.3)] bg-[hsl(var(--c-warning)/0.18)] px-3 py-1 font-mono text-xs font-bold uppercase tracking-wide text-[hsl(var(--c-warning))]">
          ⚠️ Inattiva da {daysSinceLastSession} giorni
        </span>

        <h2 id="stale-heading" className="mb-2 font-quicksand text-xl font-bold text-foreground">
          {campaignName}
        </h2>

        {/* Info table */}
        <div className="mb-4 rounded-md bg-card p-3">
          {[
            { k: 'Iniziata', v: startedAt },
            { k: 'Ultima sessione', v: lastSessionAt },
            { k: 'Ultimo paragrafo', v: lastLocation },
            { k: 'Glossario', v: `${glossaryTerms} termini` },
            { k: 'Party', v: party.join(' · ') },
          ].map(({ k, v }) => (
            <div key={k} className="flex justify-between py-1 text-sm">
              <span className="font-mono text-muted-foreground">{k}</span>
              <span className="font-mono font-semibold text-foreground tabular-nums">{v}</span>
            </div>
          ))}
        </div>

        <p className="mb-4 text-sm text-foreground">
          Vuoi riprenderla, archiviarla, o iniziare una nuova campagna pulita?
        </p>

        {/* CTAs */}
        <div className="flex flex-col gap-2">
          <button
            type="button"
            onClick={onResume}
            className="w-full rounded-md bg-[hsl(var(--c-session))] py-3 font-quicksand font-bold text-white"
          >
            ▶ Riprendi comunque
          </button>
          <button
            type="button"
            onClick={onArchiveAndNew}
            className="w-full rounded-md border border-border bg-card py-3 font-quicksand font-semibold text-foreground"
          >
            📦 Archivia + Nuova campagna
          </button>
          <button
            type="button"
            onClick={onNewOnly}
            className="w-full rounded-md border border-border bg-card py-3 font-quicksand font-semibold text-foreground"
          >
            ✨ Solo nuova (mantieni archivio)
          </button>
        </div>
      </article>
    </div>
  );
}

// ── ResumePickerTutorialMock — single resume hero with tutorial badge ────────

export interface ResumePickerTutorialMockProps {
  readonly campaignName?: string;
  readonly lastLocation?: string;
  readonly chapterLabel?: string;
  readonly tutorialSteps?: number;
  readonly glossaryTerms?: number;
  readonly qaCount?: number;
  readonly party?: string[];
  readonly onResume?: () => void;
  readonly onTutorial?: () => void;
  readonly onOptions?: () => void;
  readonly onNewCampaign?: () => void;
}

export function ResumePickerTutorialMock({
  campaignName = 'Campagna con i ragazzi',
  lastLocation = '§289',
  chapterLabel = 'Capitolo 4 — La grotta dei Goblin',
  tutorialSteps = 8,
  glossaryTerms = 12,
  qaCount = 47,
  party = ['Marco', 'Giulia', 'Luca', 'Aaron'],
  onResume,
  onTutorial,
  onOptions,
  onNewCampaign,
}: ResumePickerTutorialMockProps): ReactElement {
  return (
    <div
      data-slot="resume-picker-tutorial-mock"
      data-theme="light"
      className="min-h-screen bg-background"
    >
      {/* App bar */}
      <header className="flex items-center gap-3 border-b border-border px-4 py-3">
        <button
          type="button"
          aria-label="Torna a Eldoria"
          className="flex h-9 w-9 items-center justify-center rounded-md bg-muted text-lg"
        >
          ←
        </button>
        <span className="font-mono text-xs text-muted-foreground">
          <strong className="font-semibold text-foreground">Eldoria</strong> · Libro game
        </span>
      </header>

      <div className="px-4 py-4">
        {/* Resume hero card with tutorial badge */}
        <article
          aria-labelledby="tutorial-hero-heading"
          className="rounded-xl border border-[hsl(var(--c-session)/0.25)] bg-gradient-to-br from-[hsl(var(--c-session)/0.08)] to-[hsl(var(--c-game)/0.06)] p-5"
        >
          {/* Header row with pulsing dot + status labels */}
          <div className="mb-4 flex items-center gap-2">
            <span
              aria-hidden="true"
              className="h-3 w-3 shrink-0 rounded-full bg-[hsl(var(--c-session))] shadow-[0_0_0_0_hsl(var(--c-session)/0.5)]"
            />
            <span className="font-mono text-xs font-bold uppercase tracking-wide text-[hsl(var(--c-session))]">
              In corso
            </span>
            <span
              aria-label="Tutorial disponibile"
              className="ml-2 font-mono text-xs font-bold uppercase tracking-wide text-[hsl(var(--c-kb))]"
            >
              📚 Tutorial pronto
            </span>
          </div>

          <h2
            id="tutorial-hero-heading"
            className="mb-2 font-quicksand text-xl font-bold text-foreground"
          >
            {campaignName}
          </h2>

          {/* Meta */}
          <div className="mb-4 flex flex-wrap gap-3 font-mono text-xs text-muted-foreground">
            <span>📅 Iniziata 23 giorni fa</span>
            <span>⏱️ Ultima sessione 7 giorni fa</span>
          </div>

          {/* Progress */}
          <div className="mb-4 flex items-center gap-3 rounded-md bg-card p-3">
            <span className="font-mono text-2xl font-bold text-[hsl(var(--c-kb))] tabular-nums">
              {lastLocation}
            </span>
            <div>
              <div className="font-quicksand font-semibold text-foreground">
                Ultimo paragrafo letto
              </div>
              <div className="mt-0.5 text-xs text-muted-foreground">{chapterLabel}</div>
            </div>
          </div>

          {/* Pips */}
          <div className="mb-4 flex flex-wrap gap-2">
            <span className="inline-flex items-center gap-1 rounded-full border border-[hsl(var(--c-player)/0.2)] bg-[hsl(var(--c-player)/0.12)] px-3 py-1 font-mono text-xs font-semibold text-[hsl(var(--c-player))] tabular-nums">
              👤 {party.join(' · ')}
            </span>
            <span className="inline-flex items-center gap-1 rounded-full border border-[hsl(var(--c-kb)/0.2)] bg-[hsl(var(--c-kb)/0.12)] px-3 py-1 font-mono text-xs font-semibold text-[hsl(var(--c-kb))] tabular-nums">
              📚 Tutorial · {tutorialSteps} step
            </span>
            <span className="inline-flex items-center gap-1 rounded-full border border-[hsl(var(--c-kb)/0.2)] bg-[hsl(var(--c-kb)/0.12)] px-3 py-1 font-mono text-xs font-semibold text-[hsl(var(--c-kb))] tabular-nums">
              📄 {glossaryTerms} termini glossario
            </span>
            <span className="inline-flex items-center gap-1 rounded-full border border-[hsl(var(--c-chat)/0.2)] bg-[hsl(var(--c-chat)/0.12)] px-3 py-1 font-mono text-xs font-semibold text-[hsl(var(--c-chat))] tabular-nums">
              💬 {qaCount} Q&amp;A
            </span>
          </div>

          {/* Actions */}
          <div className="flex gap-2">
            <button
              type="button"
              onClick={onResume}
              aria-describedby="tutorial-hero-heading"
              className="flex-[2] rounded-md bg-[hsl(var(--c-session))] py-3 font-quicksand font-bold text-white"
            >
              ▶ Riprendi
            </button>
            <button
              type="button"
              onClick={onTutorial}
              aria-label="Apri tutorial Quick Start"
              className="flex-1 rounded-md border border-[hsl(var(--c-kb)/0.4)] bg-card py-3 font-quicksand font-semibold text-[hsl(var(--c-kb))]"
            >
              📚 Tutorial
            </button>
            <button
              type="button"
              onClick={onOptions}
              aria-label="Altre opzioni"
              className="flex-1 rounded-md border border-border bg-card py-3 font-quicksand font-semibold text-foreground"
            >
              ⋯ Opzioni
            </button>
          </div>
        </article>

        {/* Secondary row */}
        <div className="mt-0 flex items-center gap-3 border-t border-dashed border-border p-3">
          <span
            aria-hidden="true"
            className="flex h-8 w-8 items-center justify-center rounded-md bg-muted text-sm"
          >
            +
          </span>
          <span className="flex-1 text-sm text-muted-foreground">
            Vuoi giocare con un altro gruppo?
          </span>
          <button
            type="button"
            onClick={onNewCampaign}
            className="rounded px-2 py-1 font-quicksand text-sm font-semibold text-[hsl(var(--c-game))]"
          >
            Nuova campagna
          </button>
        </div>
      </div>
    </div>
  );
}
