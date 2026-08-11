/**
 * Template K presentational mock for SessionEnd outcome modal states.
 *
 * No real standalone component exists for the session-end outcome modal as of
 * DS-17 Phase D-2 (2026-06-22). The modal lives inside an overlay store and
 * is not exposed independently. This mock renders the full modal UI from the
 * mockup spec, driven by a single `outcome` prop.
 *
 * The modal overlays the play session screen (`/library/librogame/play/{campaignId}`)
 * and presents the session result with stats, ManaPips pip row, and CTAs.
 * Pattern: bottom-sheet on mobile (top: 80px, rounded top corners), centered
 * 560 px-wide dialog on desktop.
 *
 * Four outcomes map to the four mockup states (Stati 01–04):
 *   paused     — save & exit, session paused, resume-card with pulse indicator
 *   victory    — campaign complete, highlights timeline, share + new-campaign CTAs
 *   defeat     — party wipe, checkpoint warning, retry CTA
 *   cancelled  — abandon confirmation, destructive warning, archive impact
 *
 * Replace this mock with the real SessionEndModal component when it is extracted
 * from the overlay store. The real component will need:
 *   - Overlay/modal store (Zustand) to trigger open/close
 *   - Session stats from useLiveSessionStore
 *   - Campaign highlight data from session history
 *   - ManaPips entity counts from session store
 *
 * Uses canonical design tokens only (no hardcoded neutral utilities; the
 * `local/no-hardcoded-color-utility` ESLint rule applies).
 *
 * BGG guard: no BGG refs in this file (#2123). Internal campaign framing only.
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-session-end.html
 *   - Stato 01 · paused    (save & exit, sessione interrotta, resume disponibile)
 *   - Stato 02 · victory   (campagna completata, outcome positivo)
 *   - Stato 03 · defeat    (party wipe, retry o abbandona)
 *   - Stato 04 · cancelled (abbandono campagna, conferma destructive)
 *
 * Refs: DS-17 Phase D-2, umbrella #2063, sub-issue #2174.
 */

import type { ReactElement } from 'react';

// ---------------------------------------------------------------------------
// Outcome union type
// ---------------------------------------------------------------------------

export type SessionEndMockOutcome = 'paused' | 'victory' | 'defeat' | 'cancelled';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface SessionEndMockProps {
  /** Which mockup outcome to render. Drives the modal content. */
  readonly outcome?: SessionEndMockOutcome;
  /** Campaign name shown in the hero subtitle. */
  readonly campaignName?: string;
  /** Callback for the primary CTA (outcome-dependent). */
  readonly onPrimary?: () => void;
  /** Callback for the secondary CTA. */
  readonly onSecondary?: () => void;
}

// ---------------------------------------------------------------------------
// Sub-components (inline, no extract needed — render-only mock)
// ---------------------------------------------------------------------------

/** Single stat tile in the stats grid */
function StatTile({
  value,
  label,
  color,
}: {
  value: string;
  label: string;
  /** CSS custom property key, e.g. '--c-kb' */
  color: string;
}): ReactElement {
  return (
    <div className="rounded-md border border-border bg-muted p-3">
      <div
        className="font-mono text-2xl font-bold leading-none tabular-nums"
        style={{ color: `hsl(${color})` }}
      >
        {value}
      </div>
      <div className="mt-1 font-mono text-xs uppercase tracking-[0.04em] text-muted-foreground">
        {label}
      </div>
    </div>
  );
}

/** ManaPips row — entity pip + count + label */
function ManaPipsRow({
  pips,
}: {
  pips: { kind: string; count: string; label: string; color: string }[];
}): ReactElement {
  return (
    <div
      className="mb-4 flex flex-wrap items-center gap-1 rounded-md p-3"
      style={{ background: 'var(--bg-muted, hsl(var(--muted)))' }}
      aria-label="ManaPips sessione"
    >
      <span className="mr-2 flex-shrink-0 font-mono text-[10px] font-bold uppercase tracking-[0.06em] text-muted-foreground">
        Entità
      </span>
      {pips.map(({ kind, count, label, color }) => (
        <span key={kind} className="inline-flex items-center">
          <span
            className="relative inline-flex h-3 w-3 shrink-0 items-center justify-center rounded-full"
            style={{ background: `hsl(${color})` }}
          >
            <span
              className="absolute -top-[7px] left-1/2 -translate-x-1/2 min-w-[14px] rounded-full px-[3px] py-0 text-center font-mono text-[8px] font-bold leading-[11px] text-white"
              style={{ background: `hsl(${color})`, boxShadow: '0 1px 2px rgba(0,0,0,0.2)' }}
            >
              {count}
            </span>
          </span>
          <span className="ml-1 mr-2 font-mono text-[10px]" style={{ color: `hsl(${color})` }}>
            {label}
          </span>
        </span>
      ))}
    </div>
  );
}

/** Single highlight row for victory timeline */
function HighlightRow({
  when,
  children,
}: {
  when: string;
  children: ReactElement | string;
}): ReactElement {
  return (
    <div
      className="flex gap-3 rounded-e-sm px-3 py-2 text-sm"
      style={{
        borderLeft: '3px solid hsl(var(--c-event) / 0.55)',
        background: 'var(--bg, hsl(var(--background)))',
      }}
    >
      <span className="min-w-[50px] shrink-0 font-mono text-xs text-muted-foreground">{when}</span>
      <span className="flex-1 text-foreground/70">{children}</span>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main mock component
// ---------------------------------------------------------------------------

export function SessionEndMock({
  outcome = 'paused',
  campaignName = 'Campagna con i ragazzi',
  onPrimary,
  onSecondary,
}: SessionEndMockProps): ReactElement {
  const isPaused = outcome === 'paused';
  const isVictory = outcome === 'victory';
  const isDefeat = outcome === 'defeat';
  const isCancelled = outcome === 'cancelled';

  // ── Hero config per outcome ──────────────────────────────────────────────

  const heroConfig = {
    paused: {
      gradient:
        'linear-gradient(135deg, hsl(var(--c-session) / 0.14), hsl(var(--c-info, 210 100% 56%) / 0.08))',
      icoBackground: 'hsl(var(--c-session) / 0.18)',
      icoColor: 'hsl(var(--c-session))',
      ico: '⏸️',
      tagBg: 'hsl(var(--c-session) / 0.18)',
      tagColor: 'hsl(var(--c-session))',
      tagText: 'In pausa',
      title: 'Sessione salvata',
      sub: `${campaignName} · §289 · 3h 17m giocate`,
    },
    victory: {
      gradient:
        'linear-gradient(135deg, hsl(var(--c-success) / 0.14), hsl(var(--c-session) / 0.08))',
      icoBackground: 'hsl(var(--c-success) / 0.18)',
      icoColor: 'hsl(var(--c-success))',
      ico: '🏆',
      tagBg: 'hsl(var(--c-success) / 0.18)',
      tagColor: 'hsl(var(--c-success))',
      tagText: 'Vittoria',
      title: 'Campagna completata!',
      sub: `${campaignName} · 612 paragrafi · 14h 32m totali`,
    },
    defeat: {
      gradient:
        'linear-gradient(135deg, hsl(var(--c-danger) / 0.12), hsl(var(--c-warning) / 0.08))',
      icoBackground: 'hsl(var(--c-danger) / 0.18)',
      icoColor: 'hsl(var(--c-danger))',
      ico: '💀',
      tagBg: 'hsl(var(--c-danger) / 0.18)',
      tagColor: 'hsl(var(--c-danger))',
      tagText: 'Sconfitta',
      title: 'Party sconfitto',
      sub: "L'Architetto · Capitolo 4 · §445",
    },
    cancelled: {
      gradient: 'var(--bg-muted, hsl(var(--muted)))',
      icoBackground: 'var(--bg-card, hsl(var(--card)))',
      icoColor: 'var(--text-muted, hsl(var(--muted-foreground)))',
      ico: '⚠️',
      tagBg: 'var(--bg-card, hsl(var(--card)))',
      tagColor: 'var(--text-muted, hsl(var(--muted-foreground)))',
      tagText: 'Conferma',
      title: 'Terminare la campagna?',
      sub: `${campaignName} · §445`,
      tagBorder: '1px solid var(--border, hsl(var(--border)))',
    },
  }[outcome];

  // ── Primary button config per outcome ────────────────────────────────────

  const primaryConfig = {
    paused: { label: 'Riprendi più tardi →', bg: 'hsl(var(--c-session))' },
    victory: { label: '📖 Nuova campagna →', bg: 'hsl(var(--c-success))' },
    defeat: { label: '⟲ Riprendi dal checkpoint', bg: 'hsl(var(--c-warning))' },
    cancelled: { label: 'Conferma e archivia', bg: 'hsl(var(--c-danger))' },
  }[outcome];

  const secondaryConfig = {
    paused: 'Riepilogo',
    victory: '📤 Condividi',
    defeat: 'Termina campagna',
    cancelled: 'Annulla',
  }[outcome];

  // ── ManaPips data per outcome ─────────────────────────────────────────────

  const pipsData: {
    [k in SessionEndMockOutcome]: { kind: string; count: string; label: string; color: string }[];
  } = {
    paused: [
      { kind: 'kb', count: '142', label: 'kb', color: 'var(--c-kb)' },
      { kind: 'chat', count: '12', label: 'chat', color: 'var(--c-chat)' },
      { kind: 'player', count: '4', label: 'player', color: 'var(--c-player)' },
      { kind: 'event', count: '3', label: 'event', color: 'var(--c-event)' },
    ],
    victory: [
      { kind: 'kb', count: '142', label: 'kb', color: 'var(--c-kb)' },
      { kind: 'chat', count: '89', label: 'chat', color: 'var(--c-chat)' },
      { kind: 'session', count: '7', label: 'session', color: 'var(--c-session)' },
      { kind: 'event', count: '22', label: 'event', color: 'var(--c-event)' },
    ],
    defeat: [],
    cancelled: [],
  };

  return (
    <div
      data-slot="session-end-mock"
      data-testid="session-end-mock"
      data-theme="light"
      className="relative flex min-h-screen items-center justify-center overflow-hidden"
      style={{
        background:
          'linear-gradient(0deg,rgba(0,0,0,0.7),rgba(0,0,0,0.7)), radial-gradient(circle at 30% 30%,hsl(var(--c-session)/0.35),transparent 55%), linear-gradient(135deg,#1a1530,#0d0a1a)',
      }}
      aria-label="Sfondo sessione (dimmed)"
    >
      {/* Ghost background text */}
      <div
        className="pointer-events-none absolute left-4 top-5 font-mono text-xs"
        style={{ color: 'rgba(255,255,255,0.35)' }}
        aria-hidden="true"
      >
        Capitolo 4 · §289
      </div>
      <div
        className="pointer-events-none absolute left-4 top-7 font-quicksand text-2xl font-bold"
        style={{ color: 'rgba(255,255,255,0.45)' }}
        aria-hidden="true"
      >
        La grotta dei Goblin
      </div>

      {/* ── Modal (centered, 560 px wide) ── */}
      <div
        role="dialog"
        aria-label={`Sessione: ${heroConfig.tagText}`}
        aria-modal="true"
        className="relative z-10 flex max-h-[90vh] w-full max-w-[560px] flex-col overflow-hidden rounded-xl border border-border-strong"
        style={{
          background: 'var(--bg-card, hsl(var(--card)))',
          boxShadow: 'var(--shadow-lg, 0 20px 60px rgba(0,0,0,0.3))',
        }}
      >
        {/* Close X */}
        <button
          type="button"
          aria-label="Chiudi"
          className="absolute right-3 top-3 z-10 flex h-7 w-7 items-center justify-center rounded-full bg-muted text-sm"
        >
          ✕
        </button>

        {/* ── Hero outcome card ── */}
        <div
          className="relative overflow-hidden px-5 pb-4 pt-6 text-center"
          style={{ background: heroConfig.gradient }}
        >
          <div
            className="mx-auto mb-3 flex h-[88px] w-[88px] items-center justify-center rounded-full text-[44px]"
            style={{
              background: heroConfig.icoBackground,
              color: heroConfig.icoColor,
              boxShadow: '0 8px 24px rgba(0,0,0,0.12)',
            }}
            aria-hidden="true"
          >
            {heroConfig.ico}
          </div>
          <span
            className="mb-2 inline-block rounded-full px-3 py-0.5 font-mono text-xs font-bold uppercase tracking-[0.08em]"
            style={{
              background: heroConfig.tagBg,
              color: heroConfig.tagColor,
              border: 'tagBorder' in heroConfig ? heroConfig.tagBorder : undefined,
            }}
          >
            {heroConfig.tagText}
          </span>
          <h2 className="m-0 mb-1 font-quicksand text-2xl font-bold">{heroConfig.title}</h2>
          <p className="m-0 font-mono text-xs text-muted-foreground">{heroConfig.sub}</p>
        </div>

        {/* ── Modal body (scrollable) ── */}
        <div className="flex-1 overflow-y-auto px-5 py-4">
          {/* PAUSED: resume card */}
          {isPaused && (
            <div
              className="mb-4 flex items-center gap-3 rounded-md px-4 py-3"
              style={{
                background: 'hsl(var(--c-session) / 0.06)',
                border: '1px solid hsl(var(--c-session) / 0.25)',
              }}
            >
              <span
                className="h-2.5 w-2.5 shrink-0 rounded-full"
                style={{ background: 'hsl(var(--c-session))' }}
                aria-hidden="true"
              />
              <div className="flex-1">
                <div className="font-quicksand font-bold">Pronto a riprendere</div>
                <div className="mt-0.5 font-mono text-xs text-muted-foreground">
                  Stato salvato 23:47 · Capitolo 4 in corso
                </div>
              </div>
            </div>
          )}

          {/* PAUSED: session stats */}
          {isPaused && (
            <>
              <h3 className="mb-2 font-mono text-xs font-bold uppercase tracking-[0.06em] text-muted-foreground">
                Statistiche sessione
              </h3>
              <div className="mb-4 grid grid-cols-2 gap-2">
                <StatTile value="12" label="Q&A regole" color="var(--c-kb)" />
                <StatTile value="8" label="Paragrafi" color="var(--c-event)" />
                <StatTile value="3" label="Encounter" color="var(--c-toolkit)" />
                <StatTile value="2" label="Foto traduzione" color="var(--c-chat)" />
              </div>
              <ManaPipsRow pips={pipsData.paused} />
            </>
          )}

          {/* VICTORY: highlights */}
          {isVictory && (
            <>
              <h3 className="mb-2 font-mono text-xs font-bold uppercase tracking-[0.06em] text-muted-foreground">
                Highlights campagna
              </h3>
              <div className="mb-4 flex flex-col gap-2">
                <HighlightRow when="Cap 2">
                  <>
                    <strong className="text-foreground">Patto con i Goblin</strong> — scelta
                    narrativa, +1 alleato
                  </>
                </HighlightRow>
                <HighlightRow when="Cap 4">
                  <>
                    Boss <strong className="text-foreground">Architetto</strong> sconfitto al 2°
                    tentativo
                  </>
                </HighlightRow>
                <HighlightRow when="Cap 6">
                  <>
                    Glossario completato · <strong className="text-foreground">34 termini</strong>
                  </>
                </HighlightRow>
                <HighlightRow when="Epil.">
                  <>
                    <strong className="text-foreground">Finale buono</strong> · ending #2 di 4
                  </>
                </HighlightRow>
              </div>
              <h3 className="mb-2 font-mono text-xs font-bold uppercase tracking-[0.06em] text-muted-foreground">
                Stats finali
              </h3>
              <div className="mb-4 grid grid-cols-2 gap-2">
                <StatTile value="7" label="Sessioni" color="var(--c-session)" />
                <StatTile value="612" label="Paragrafi" color="var(--c-event)" />
                <StatTile value="89" label="Q&A" color="var(--c-kb)" />
                <StatTile value="22" label="Encounter" color="var(--c-toolkit)" />
              </div>
              <ManaPipsRow pips={pipsData.victory} />
            </>
          )}

          {/* DEFEAT: checkpoint warning + stats */}
          {isDefeat && (
            <>
              <div
                className="mb-4 rounded-md px-4 py-3 text-sm text-foreground/70"
                style={{
                  background: 'hsl(var(--c-warning) / 0.08)',
                  border: '1px solid hsl(var(--c-warning) / 0.25)',
                }}
              >
                <strong
                  style={{
                    color: 'hsl(var(--c-warning))',
                    fontFamily: 'var(--f-display, inherit)',
                    display: 'block',
                    marginBottom: '4px',
                  }}
                >
                  ⚠ Checkpoint disponibile
                </strong>
                Ultima safe area: <strong className="text-foreground">§412 · Accampamento</strong>{' '}
                (12 min fa).
              </div>
              <h3 className="mb-2 font-mono text-xs font-bold uppercase tracking-[0.06em] text-muted-foreground">
                Sessione corrente
              </h3>
              <div className="mb-4 grid grid-cols-2 gap-2">
                <StatTile value="33" label="Paragrafi" color="var(--c-event)" />
                <StatTile value="5" label="Encounter" color="var(--c-toolkit)" />
                <StatTile value="8" label="Q&A" color="var(--c-kb)" />
                <StatTile value="1" label="Foto trad" color="var(--c-chat)" />
              </div>
            </>
          )}

          {/* CANCELLED: destructive warning + archive impact ManaPips */}
          {isCancelled && (
            <>
              <div
                className="mb-4 flex gap-3 rounded-md px-4 py-3"
                style={{
                  background: 'hsl(var(--c-danger) / 0.06)',
                  border: '1px solid hsl(var(--c-danger) / 0.3)',
                }}
              >
                <span className="shrink-0 text-[22px]" aria-hidden="true">
                  🚨
                </span>
                <div className="flex-1 text-sm text-foreground/70">
                  <strong
                    style={{ color: 'hsl(var(--c-danger))', display: 'block', marginBottom: '4px' }}
                  >
                    Azione irreversibile
                  </strong>
                  Il progresso narrativo verrà archiviato (read-only). Il glossario sarà preservato
                  e riutilizzabile in future campagne Eldoria.
                </div>
              </div>
              <h3 className="mb-2 font-mono text-xs font-bold uppercase tracking-[0.06em] text-muted-foreground">
                Cosa verrà archiviato
              </h3>
              {/* Persistent pips */}
              <div
                className="mb-2 flex flex-wrap items-center gap-1 rounded-md p-3"
                style={{ background: 'var(--bg-muted, hsl(var(--muted)))' }}
              >
                <span className="mr-2 flex-shrink-0 font-mono text-[10px] font-bold uppercase tracking-[0.06em] text-muted-foreground">
                  Persistenti
                </span>
                {[
                  { kind: 'kb', count: '142', label: 'kb', color: 'var(--c-kb)' },
                  { kind: 'toolkit', count: '34', label: 'glossario', color: 'var(--c-toolkit)' },
                ].map(({ kind, count, label, color }) => (
                  <span key={kind} className="inline-flex items-center">
                    <span
                      className="relative inline-flex h-3 w-3 shrink-0 items-center justify-center rounded-full"
                      style={{ background: `hsl(${color})` }}
                    >
                      <span
                        className="absolute -top-[7px] left-1/2 -translate-x-1/2 min-w-[14px] rounded-full px-[3px] py-0 text-center font-mono text-[8px] font-bold leading-[11px] text-white"
                        style={{
                          background: `hsl(${color})`,
                          boxShadow: '0 1px 2px rgba(0,0,0,0.2)',
                        }}
                      >
                        {count}
                      </span>
                    </span>
                    <span
                      className="ml-1 mr-2 font-mono text-[10px]"
                      style={{ color: `hsl(${color})` }}
                    >
                      {label}
                    </span>
                  </span>
                ))}
              </div>
              {/* Archived pips */}
              <div
                className="mb-4 flex flex-wrap items-center gap-1 rounded-md p-3"
                style={{
                  background: 'hsl(var(--c-danger) / 0.06)',
                  border: '1px solid hsl(var(--c-danger) / 0.2)',
                }}
              >
                <span
                  className="mr-2 flex-shrink-0 font-mono text-[10px] font-bold uppercase tracking-[0.06em]"
                  style={{ color: 'hsl(var(--c-danger))' }}
                >
                  Archiviati
                </span>
                {[
                  { kind: 'event', count: '445', label: 'progresso', color: 'var(--c-event)' },
                  { kind: 'session', count: '4', label: 'sessioni', color: 'var(--c-session)' },
                ].map(({ kind, count, label, color }) => (
                  <span key={kind} className="inline-flex items-center">
                    <span
                      className="relative inline-flex h-3 w-3 shrink-0 items-center justify-center rounded-full"
                      style={{ background: `hsl(${color})` }}
                    >
                      <span
                        className="absolute -top-[7px] left-1/2 -translate-x-1/2 min-w-[14px] rounded-full px-[3px] py-0 text-center font-mono text-[8px] font-bold leading-[11px] text-white"
                        style={{
                          background: `hsl(${color})`,
                          boxShadow: '0 1px 2px rgba(0,0,0,0.2)',
                        }}
                      >
                        {count}
                      </span>
                    </span>
                    <span
                      className="ml-1 mr-2 font-mono text-[10px]"
                      style={{ color: `hsl(${color})` }}
                    >
                      {label}
                    </span>
                  </span>
                ))}
              </div>
            </>
          )}
        </div>

        {/* ── Modal footer ── */}
        <div className="flex shrink-0 items-center gap-2 border-t border-border px-5 py-3">
          <button
            type="button"
            onClick={onSecondary}
            className="rounded-md border border-border bg-card px-4 py-3 font-quicksand font-semibold text-foreground"
          >
            {secondaryConfig}
          </button>
          <button
            type="button"
            onClick={onPrimary}
            className="flex flex-1 items-center justify-center gap-1 rounded-md border-0 px-5 py-3 font-quicksand font-bold text-white"
            style={{
              background: primaryConfig.bg,
              boxShadow: `0 4px 14px ${primaryConfig.bg.replace(')', ' / 0.35)')}`,
            }}
          >
            {primaryConfig.label}
          </button>
        </div>

        {/* forward-refactor notice */}
        <p className="px-5 pb-3 text-xs text-muted-foreground">
          <em>
            forward-refactor — SessionEndModal (overlay store). Richiede useLiveSessionStore per
            stats + session highlight data + campaign progress.
          </em>
        </p>
      </div>
    </div>
  );
}
