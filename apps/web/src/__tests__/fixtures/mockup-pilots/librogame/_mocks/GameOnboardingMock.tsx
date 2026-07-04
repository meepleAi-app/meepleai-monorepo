/**
 * Template K presentational mock for GameOnboarding prereq-stepper states.
 *
 * No real component exists for the prereq-stepper onboarding flow as of DS-17
 * Phase D-2 (2026-06-22). This mock renders the full stepper UI from the
 * mockup spec, driven by a single `state` prop.
 *
 * The stepper guides the user through three prerequisite steps before the
 * "Avvia libro game" CTA is unlocked:
 *   1. PDF upload (regolamento + Press Start)
 *   2. KB indexing (chunks + embeddings + pgvector)
 *   3. Tutor agent creation (linked to the KB)
 *
 * Four states map to the four mockup sections A–D:
 *   prereq-missing  — 3 steps todo; active step 1 with "Carica PDF" CTA
 *   pdf-uploading   — step 1 active; 2 PDFs in concurrent upload (1 done, 1 at 62%)
 *   kb-indexing     — step 1 done; step 2 active with sub-steps + ETA
 *   ready           — all 3 steps done; primary CTA "Avvia libro game" unlocked
 *
 * Replace this mock with the real GameOnboardingPanel component when it is
 * implemented. The component will need access to:
 *   - PDF upload progress stream
 *   - KB indexing job status (SSE or polling)
 *   - Agent creation API
 *
 * Uses canonical design tokens only (no hardcoded neutral utilities; the
 * `local/no-hardcoded-color-utility` ESLint rule applies).
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-game-onboarding.html
 *   - Stato A · prereq-missing   (0/3 step completati)
 *   - Stato B · pdf-uploading    (upload in corso, 1 di 2 file a 62%)
 *   - Stato C · kb-indexing      (embedding e5-base 43%, ETA ~70 s)
 *   - Stato D · ready            (tutto pronto, CTA session attiva)
 *
 * Refs: DS-17 Phase D-2, umbrella #2063, sub-issue #2174.
 */

import type { ReactElement } from 'react';

// ---------------------------------------------------------------------------
// State union type
// ---------------------------------------------------------------------------

export type GameOnboardingMockState = 'prereq-missing' | 'pdf-uploading' | 'kb-indexing' | 'ready';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface GameOnboardingMockProps {
  /** Which mockup state to render. Drives the stepper content. */
  readonly state?: GameOnboardingMockState;
  /** Game title shown in the hero cover. */
  readonly gameTitle?: string;
  /** Publisher + year shown under the title. */
  readonly gameMeta?: string;
  /** Callback for the "Carica PDF" CTA (prereq-missing state). */
  readonly onUploadPdf?: () => void;
  /** Callback for the "Avvia libro game" primary CTA (ready state). */
  readonly onStartSession?: () => void;
}

// ---------------------------------------------------------------------------
// Sub-components (inline, no extract needed — render-only mock)
// ---------------------------------------------------------------------------

/** Hero card: gradient cover + 4-stat meta row */
function HeroCover({
  title,
  meta,
  badge,
}: {
  title: string;
  meta: string;
  badge: string;
}): ReactElement {
  return (
    <article className="overflow-hidden rounded-xl border border-border bg-card">
      <div
        className="relative flex flex-col justify-end p-4"
        style={{
          aspectRatio: '7/4',
          background:
            'linear-gradient(135deg, hsl(var(--c-game)) 0%, hsl(var(--c-event) / 0.8) 100%)',
          color: '#fff',
        }}
      >
        <span
          className="absolute right-3 top-3 rounded-full px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-widest"
          style={{ background: 'rgba(255,255,255,0.18)', backdropFilter: 'blur(8px)' }}
        >
          {badge}
        </span>
        <h2 className="m-0 font-quicksand text-3xl font-bold leading-tight">{title}</h2>
        <span className="mt-0.5 font-mono text-sm opacity-85">{meta}</span>
      </div>
      <div className="grid grid-cols-4 gap-2 p-4">
        {[
          { v: '3-4', l: 'giocatori' },
          { v: "90'", l: 'durata' },
          { v: '14+', l: 'età' },
          { v: 'EN', l: 'lingua' },
        ].map(({ v, l }) => (
          <div key={l} className="flex flex-col">
            <span className="font-mono font-bold text-foreground">{v}</span>
            <span className="mt-0.5 text-xs text-muted-foreground">{l}</span>
          </div>
        ))}
      </div>
    </article>
  );
}

/** Single step row in the prereq stepper */
function StepRow({
  kind,
  icon,
  name,
  badge,
  detail,
  children,
}: {
  kind: 'active' | 'done' | 'todo';
  icon: string;
  name: string;
  badge: string;
  detail: string;
  children?: ReactElement;
}): ReactElement {
  const containerCls = [
    'flex items-start gap-3 rounded-xl border p-3',
    kind === 'done' && 'border-[hsl(var(--c-success)/0.3)] bg-[hsl(var(--c-success)/0.04)]',
    kind === 'active' && 'border-[hsl(var(--c-game)/0.4)] bg-[hsl(var(--c-game)/0.04)]',
    kind === 'todo' && 'border-border bg-card opacity-70',
  ]
    .filter(Boolean)
    .join(' ');

  const icoCls = [
    'flex h-8 w-8 shrink-0 items-center justify-center rounded-md text-base',
    kind === 'done' && 'bg-[hsl(var(--c-success)/0.15)] text-[hsl(var(--c-success))]',
    kind === 'active' && 'bg-[hsl(var(--c-game)/0.15)] text-[hsl(var(--c-game))]',
    kind === 'todo' && 'bg-muted text-muted-foreground',
  ]
    .filter(Boolean)
    .join(' ');

  const badgeCls = [
    'rounded-full px-2 py-0.5 font-mono text-[10px] font-bold uppercase tracking-wide',
    kind === 'done' && 'bg-[hsl(var(--c-success)/0.15)] text-[hsl(var(--c-success))]',
    kind === 'active' && 'bg-[hsl(var(--c-game)/0.15)] text-[hsl(var(--c-game))]',
    kind === 'todo' && 'bg-muted text-muted-foreground',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <div className={containerCls}>
      <span className={icoCls} aria-hidden="true">
        {icon}
      </span>
      <div className="min-w-0 flex-1">
        <div className="mb-0.5 flex items-center justify-between gap-2">
          <span className="font-quicksand text-sm font-bold text-foreground">{name}</span>
          <span className={badgeCls}>{badge}</span>
        </div>
        <div className="font-mono text-xs text-foreground/60">{detail}</div>
        {children}
      </div>
    </div>
  );
}

/** Upload progress row for a single PDF file */
function UploadRow({
  icon,
  name,
  meta,
  percent,
  done,
}: {
  icon: string;
  name: string;
  meta: string;
  percent: number;
  done: boolean;
}): ReactElement {
  return (
    <div className="flex items-center gap-3 rounded-md border border-border bg-card p-3">
      <span className="shrink-0 text-2xl" aria-hidden="true">
        {icon}
      </span>
      <div className="min-w-0 flex-1">
        <div className="truncate font-quicksand text-sm font-semibold text-foreground">{name}</div>
        <div className="mt-0.5 font-mono text-xs text-muted-foreground">{meta}</div>
        <div className="mt-2 h-1 overflow-hidden rounded-full bg-muted">
          <div
            className="h-full rounded-full"
            style={{
              width: `${percent}%`,
              background: done ? 'hsl(var(--c-success))' : 'hsl(var(--c-game))',
            }}
            aria-label={done ? 'completato' : `${percent}%`}
          />
        </div>
      </div>
      <span
        className="shrink-0 font-mono text-xs font-bold tabular-nums"
        style={{ color: done ? 'hsl(var(--c-success))' : 'hsl(var(--c-game))' }}
      >
        {done ? '✓' : `${percent}%`}
      </span>
    </div>
  );
}

/** KB indexing sub-step row */
function IndexStep({
  kind,
  icon,
  label,
  num,
}: {
  kind: 'done' | 'active' | 'todo';
  icon: string;
  label: string;
  num: string;
}): ReactElement {
  return (
    <div
      className={['flex items-center gap-2 font-mono text-xs', kind === 'todo' && 'opacity-50']
        .filter(Boolean)
        .join(' ')}
    >
      <span
        className="text-xs"
        style={{
          color:
            kind === 'done'
              ? 'hsl(var(--c-success))'
              : kind === 'active'
                ? 'hsl(var(--c-kb))'
                : undefined,
        }}
        aria-hidden="true"
      >
        {icon}
      </span>
      <span className="flex-1 text-foreground/70">{label}</span>
      <span
        className="tabular-nums font-bold"
        style={{
          color: kind === 'todo' ? undefined : 'hsl(var(--c-kb))',
        }}
      >
        {num}
      </span>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main mock component
// ---------------------------------------------------------------------------

export function GameOnboardingMock({
  state = 'prereq-missing',
  gameTitle = 'Eldoria',
  gameMeta = 'Mythic Games · 2024',
  onUploadPdf,
  onStartSession,
}: GameOnboardingMockProps): ReactElement {
  const isPrereqMissing = state === 'prereq-missing';
  const isPdfUploading = state === 'pdf-uploading';
  const isKbIndexing = state === 'kb-indexing';
  const isReady = state === 'ready';

  const heroBadge = isReady ? '📖 libro game · pronto' : '📖 libro game';

  /* ── prereq-head counter ── */
  const completedCount = isPrereqMissing || isPdfUploading ? 0 : isKbIndexing ? 1 : 3;
  const headLabel = isReady ? '✓ Setup completato' : '📋 Prepara libro game';
  const counterSuffix = isPdfUploading ? ' · in corso' : isKbIndexing ? ' · in corso' : '';

  return (
    <div
      data-slot="game-onboarding-mock"
      data-testid="game-onboarding-mock"
      data-theme="light"
      className="min-h-screen bg-background"
      aria-busy={isPdfUploading || isKbIndexing}
    >
      {/* App bar */}
      <header className="flex items-center gap-3 border-b border-border px-4 py-3">
        <button
          type="button"
          aria-label="Torna alla libreria"
          className="flex h-9 w-9 items-center justify-center rounded-md bg-muted text-lg"
        >
          ←
        </button>
        <span className="flex-1 font-mono text-xs text-muted-foreground">
          <strong className="font-semibold text-foreground">Libreria</strong>
          {' · '}
          {gameTitle}
          {isPdfUploading ? ' · Upload PDF…' : isKbIndexing ? ' · Indicizzazione…' : ''}
        </span>
        {!isPdfUploading && (
          <button
            type="button"
            aria-label="Altre opzioni"
            className="flex h-9 w-9 items-center justify-center rounded-md bg-muted text-sm"
          >
            ⋯
          </button>
        )}
      </header>

      <div className="p-4">
        {/* Hero card */}
        <HeroCover title={gameTitle} meta={gameMeta} badge={heroBadge} />

        {/* ── READY state: primary CTA above stepper ── */}
        {isReady && (
          <div className="mt-4 flex flex-col gap-2">
            <button
              type="button"
              onClick={onStartSession}
              autoFocus
              className="flex items-center justify-between gap-3 rounded-xl border-0 px-5 py-4 font-quicksand font-bold text-white"
              style={{
                background: 'hsl(var(--c-session))',
                boxShadow: '0 4px 16px hsl(var(--c-session) / 0.3)',
              }}
            >
              <span className="flex-1 text-left">
                <span className="block">🎲 Avvia libro game</span>
                <span className="mt-0.5 block text-xs font-normal opacity-85">
                  prima sessione · setup ~2 min
                </span>
              </span>
              <span className="text-2xl" aria-hidden="true">
                ›
              </span>
            </button>
            <div className="flex flex-wrap justify-center gap-2">
              {['✓ 137 MB indicizzati', '✓ 1.842 chunks', '✓ Arbitro Eldoria'].map(chip => (
                <span
                  key={chip}
                  className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-[10px] font-bold"
                  style={{
                    background: 'hsl(var(--c-success) / 0.1)',
                    color: 'hsl(var(--c-success))',
                  }}
                >
                  {chip}
                </span>
              ))}
            </div>
          </div>
        )}

        {/* ── Prereq stepper panel ── */}
        <div className="mt-4 flex flex-col gap-3">
          {/* Header */}
          <div className="flex items-center justify-between gap-3 px-1 py-1">
            <h3 className="m-0 font-quicksand font-bold text-foreground">{headLabel}</h3>
            <span className="font-mono text-xs text-muted-foreground">
              <strong style={{ color: 'hsl(var(--c-game))' }}>{completedCount}</strong>
              {' / 3'}
              {counterSuffix}
            </span>
          </div>

          {/* ── STEP 1: PDF upload ── */}
          {(isPrereqMissing || isPdfUploading) && (
            <StepRow
              kind="active"
              icon="📄"
              name={
                isPdfUploading
                  ? 'Carica regolamento e Press Start'
                  : 'Carica regolamento e Press Start'
              }
              badge={isPdfUploading ? 'in corso' : '1 di 3'}
              detail={
                isPdfUploading
                  ? 'EXIF GPS rimosso · upload in corso'
                  : '2 PDF necessari · ~140 MB totali'
              }
            >
              {isPdfUploading ? (
                <div className="mt-3 flex flex-col gap-2">
                  <UploadRow
                    icon="📑"
                    name="Eldoria Press Start ENG.pdf"
                    meta="36 MB · 24 pag · tipo: Press Start"
                    percent={100}
                    done
                  />
                  <UploadRow
                    icon="📕"
                    name="Eldoria Rules ENG.pdf"
                    meta="101 MB · 248 pag · tipo: Rules"
                    percent={62}
                    done={false}
                  />
                </div>
              ) : (
                <button
                  type="button"
                  onClick={onUploadPdf}
                  className="mt-3 self-start rounded-md px-3 py-2 font-quicksand text-sm font-bold text-white"
                  style={{ background: 'hsl(var(--c-game))' }}
                >
                  Carica PDF →
                </button>
              )}
            </StepRow>
          )}

          {/* STEP 1 done (indexing + ready states) */}
          {(isKbIndexing || isReady) && (
            <StepRow
              kind="done"
              icon="✓"
              name="PDF caricati"
              badge={isReady ? '✓' : '✓ 1 di 3'}
              detail="Press Start + Rules · 137 MB totali"
            />
          )}

          {/* ── STEP 2: KB indexing ── */}
          {(isPrereqMissing || isPdfUploading) && (
            <StepRow
              kind="todo"
              icon="🧠"
              name="Indicizza conoscenza"
              badge="2 di 3"
              detail={
                isPdfUploading
                  ? 'in attesa upload completato'
                  : 'chunks + embeddings · automatico · ~2 min'
              }
            />
          )}

          {isKbIndexing && (
            <StepRow
              kind="active"
              icon="🧠"
              name="Indicizza conoscenza"
              badge="2 di 3 · 43%"
              detail="e5-base embeddings · pgvector index"
            >
              <div
                className="mt-3 flex flex-col gap-2 rounded-md border p-3"
                style={{
                  background: 'hsl(var(--c-kb) / 0.04)',
                  borderColor: 'hsl(var(--c-kb) / 0.2)',
                }}
              >
                <IndexStep kind="done" icon="✓" label="Parsing PDF text-layer" num="272 pag" />
                <IndexStep kind="done" icon="✓" label="Chunking semantic" num="1.842 chunks" />
                <IndexStep kind="active" icon="⏳" label="Embedding e5-base" num="792 / 1.842" />
                <IndexStep kind="todo" icon="○" label="pgvector index" num="attesa" />
                <div className="mt-2 text-center font-mono text-[10px] text-muted-foreground">
                  ⏱ ETA ~70 s · puoi chiudere, ti notifichiamo
                </div>
              </div>
            </StepRow>
          )}

          {isReady && (
            <StepRow
              kind="done"
              icon="✓"
              name="Conoscenza indicizzata"
              badge="✓"
              detail="1.842 chunks · pgvector ready"
            />
          )}

          {/* ── STEP 3: Agent creation ── */}
          {(isPrereqMissing || isPdfUploading || isKbIndexing) && (
            <StepRow
              kind="todo"
              icon="🤖"
              name="Crea agente Tutor"
              badge="3 di 3"
              detail={
                isKbIndexing
                  ? 'in attesa indicizzazione'
                  : 'linked a regolamento + Press Start · 1 click'
              }
            />
          )}

          {isReady && (
            <StepRow
              kind="done"
              icon="✓"
              name="Agente Arbitro Eldoria"
              badge="✓"
              detail="RulesInterpreter · linked a 2 KB"
            />
          )}
        </div>

        {/* forward-refactor notice */}
        <p className="mt-4 text-xs text-muted-foreground">
          <em>
            forward-refactor — GameOnboardingPanel (prereq-stepper). Richiede PDF upload progress
            stream + KB indexing job status (SSE) + agent creation API.
          </em>
        </p>
      </div>
    </div>
  );
}
