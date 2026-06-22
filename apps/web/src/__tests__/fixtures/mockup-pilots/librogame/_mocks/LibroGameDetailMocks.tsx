/**
 * Presentational mocks for LibroGameDetailView loading / error / not-found states.
 *
 * forward-refactor: these mocks replicate the parent-page-level skeleton / error /
 * not-found UI that lives in the `/library/[gameId]/page.tsx` shell, NOT inside
 * LibroGameDetailView itself.  The real component has no loading/error/notFound
 * prop — those states are handled by the page shell (React Query suspend-boundary +
 * conditional render).
 *
 * When the skeleton and error surfaces are extracted as shared primitives, replace
 * these mocks with the real components.
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-game-detail.html
 *   - State 2 · Loading (skeleton hero + meta + CTA placeholder)
 *   - State 3 · Error (network error card + retry CTA)
 *   - State 4 · Not Found (not-in-collection card + add-to-catalog CTA)
 *
 * Uses canonical design tokens only (no hardcoded color utilities).
 * Refs: DS-17 Phase D-2, umbrella #2063, sub-issue #2174.
 */

import type { ReactElement } from 'react';

// ── Loading mock — skeleton hero + meta grid + CTA placeholder ─────────────

export function LibroGameDetailLoadingMock(): ReactElement {
  return (
    <div
      data-theme="light"
      data-slot="libro-game-detail-loading-mock"
      className="min-h-screen bg-[#f7f3ee] text-[#2b1f12]"
    >
      {/* App bar skeleton */}
      <header className="flex items-center gap-3 border-b border-[rgba(180,130,80,0.1)] px-4 py-3">
        <div className="h-9 w-9 animate-pulse rounded-[10px] bg-muted" />
        <div className="h-4 w-40 animate-pulse rounded bg-muted" />
      </header>

      <main className="mx-auto max-w-4xl px-4 py-4">
        {/* Hero card skeleton */}
        <article className="overflow-hidden rounded-[18px] border border-[rgba(180,130,80,0.1)] bg-card shadow-[0_2px_8px_rgba(90,60,20,0.08)]">
          {/* Cover skeleton */}
          <div
            className="animate-pulse bg-muted"
            style={{ aspectRatio: '12 / 4' }}
            aria-hidden="true"
          />

          {/* Meta grid skeleton */}
          <div className="grid grid-cols-4 gap-2 p-4">
            {[0, 1, 2, 3].map(i => (
              <div key={i} className="flex flex-col gap-1">
                <div className="h-4 w-10 animate-pulse rounded bg-muted" />
                <div className="h-3 w-14 animate-pulse rounded bg-muted" />
              </div>
            ))}
          </div>

          {/* Pip bar skeleton */}
          <div className="flex gap-2 px-4 pb-4">
            {[0, 1, 2, 3, 4].map(i => (
              <div key={i} className="h-6 w-14 animate-pulse rounded-full bg-muted" />
            ))}
          </div>
        </article>

        {/* CTA skeleton */}
        <div className="px-0 pt-4">
          <div className="h-14 w-full animate-pulse rounded-[14px] bg-muted" />
          <div className="mt-2 h-3 w-48 animate-pulse rounded bg-muted" />
        </div>

        {/* Tab bar skeleton */}
        <div className="mt-4 flex gap-1 overflow-x-auto border-b border-[rgba(180,130,80,0.1)] pb-2">
          {[0, 1, 2, 3].map(i => (
            <div key={i} className="h-6 w-16 animate-pulse rounded bg-muted" />
          ))}
        </div>

        {/* Content skeleton lines */}
        <div className="mt-4 grid gap-3">
          <div className="h-4 w-4/5 animate-pulse rounded bg-muted" />
          <div className="h-4 w-3/5 animate-pulse rounded bg-muted" />
          <div className="h-4 w-2/3 animate-pulse rounded bg-muted" />
        </div>
      </main>
    </div>
  );
}

// ── Error mock — network error card ─────────────────────────────────────────

export interface LibroGameDetailErrorMockProps {
  readonly onRetry?: () => void;
  readonly onBack?: () => void;
}

export function LibroGameDetailErrorMock({
  onRetry,
  onBack,
}: LibroGameDetailErrorMockProps): ReactElement {
  return (
    <div
      data-theme="light"
      data-slot="libro-game-detail-error-mock"
      className="min-h-screen bg-[#f7f3ee] text-[#2b1f12]"
    >
      {/* App bar */}
      <header className="flex items-center gap-3 border-b border-[rgba(180,130,80,0.1)] px-4 py-3">
        <button
          type="button"
          onClick={onBack}
          aria-label="Torna alla libreria"
          className="flex h-9 w-9 items-center justify-center rounded-[10px] bg-[#efe6d9] text-lg hover:bg-[rgba(180,130,80,0.12)]"
        >
          ←
        </button>
        <span className="font-mono text-[11px] text-[#9a8870]">
          <strong className="font-semibold text-[#5a4a38]">Libreria</strong> · Errore
        </span>
      </header>

      <main className="mx-auto max-w-4xl px-4 py-4">
        <div
          role="alert"
          className="mt-16 rounded-[18px] border border-border bg-card p-8 text-center shadow-sm"
        >
          <div className="mb-3 text-5xl" aria-hidden="true">
            ⚠️
          </div>
          <h2 className="mb-2 font-quicksand text-xl font-bold text-foreground">
            Impossibile caricare il gioco
          </h2>
          <p className="mb-6 text-[15px] text-muted-foreground">
            Si è verificato un errore di rete. Controlla la connessione e riprova.
          </p>
          <div className="flex flex-wrap justify-center gap-2">
            <button
              type="button"
              onClick={onBack}
              className="rounded-md border border-border bg-card px-4 py-2 font-quicksand font-semibold text-foreground hover:bg-muted"
            >
              ← Libreria
            </button>
            <button
              type="button"
              onClick={onRetry}
              className="rounded-md bg-[hsl(var(--c-game))] px-4 py-2 font-quicksand font-bold text-white hover:shadow-[0_4px_14px_hsl(var(--c-game)/0.35)]"
            >
              🔄 Riprova
            </button>
          </div>
        </div>
      </main>
    </div>
  );
}

// ── Not Found mock — game not in collection card ─────────────────────────────

export interface LibroGameDetailNotFoundMockProps {
  readonly gameTitle?: string;
  readonly onBack?: () => void;
  readonly onAddToCatalog?: () => void;
}

export function LibroGameDetailNotFoundMock({
  gameTitle = 'Nanolith',
  onBack,
  onAddToCatalog,
}: LibroGameDetailNotFoundMockProps): ReactElement {
  return (
    <div
      data-theme="light"
      data-slot="libro-game-detail-not-found-mock"
      className="min-h-screen bg-[#f7f3ee] text-[#2b1f12]"
    >
      {/* App bar */}
      <header className="flex items-center gap-3 border-b border-[rgba(180,130,80,0.1)] px-4 py-3">
        <button
          type="button"
          onClick={onBack}
          aria-label="Torna alla libreria"
          className="flex h-9 w-9 items-center justify-center rounded-[10px] bg-[#efe6d9] text-lg hover:bg-[rgba(180,130,80,0.12)]"
        >
          ←
        </button>
        <span className="font-mono text-[11px] text-[#9a8870]">
          <strong className="font-semibold text-[#5a4a38]">Libreria</strong> · 404
        </span>
      </header>

      <main className="mx-auto max-w-4xl px-4 py-4">
        <div className="mt-16 rounded-[18px] border border-border bg-card p-8 text-center shadow-sm">
          <div className="mb-3 text-5xl" aria-hidden="true">
            🎲
          </div>
          <h2 className="mb-2 font-quicksand text-xl font-bold text-foreground">
            {gameTitle} non in collezione
          </h2>
          <p className="mb-6 text-[15px] text-muted-foreground">
            Questo gioco non è ancora nella tua libreria. Puoi aggiungerlo dal catalogo pubblico per
            accedere alla modalità libro game.
          </p>
          <div className="flex flex-wrap justify-center gap-2">
            <button
              type="button"
              onClick={onBack}
              className="rounded-md border border-border bg-card px-4 py-2 font-quicksand font-semibold text-foreground hover:bg-muted"
            >
              ← Libreria
            </button>
            <button
              type="button"
              onClick={onAddToCatalog}
              className="rounded-md bg-[hsl(var(--c-game))] px-4 py-2 font-quicksand font-bold text-white hover:shadow-[0_4px_14px_hsl(var(--c-game)/0.35)]"
            >
              + Aggiungi al catalogo
            </button>
          </div>
        </div>
      </main>
    </div>
  );
}
