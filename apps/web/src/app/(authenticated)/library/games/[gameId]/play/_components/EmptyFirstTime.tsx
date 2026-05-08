'use client';

import type { ReactElement } from 'react';

export interface EmptyFirstTimeProps {
  gameId: string;
  gameTitle?: string;
  onCreateCampaign?: () => void;
}

/**
 * Stato 01 (mockup G state-01-first-time) — Aaron mai giocato a questo gioco.
 * Hero "Inizia la tua prima campagna" + 3 reassurance row + CTA primary.
 *
 * FREEZE-compliant: solo var(--c-game) / var(--c-agent) / token semantici.
 * Riferimento: admin-mockups/design_files/sp6-libro-game-resume-state.{html,jsx}
 */
export function EmptyFirstTime({
  gameId: _gameId,
  gameTitle,
  onCreateCampaign,
}: EmptyFirstTimeProps): ReactElement {
  return (
    <section
      data-testid="gamebook-resume-empty-first-time"
      data-state="state-01-first-time"
      className="flex flex-col gap-5 px-4 py-6 max-w-screen-sm mx-auto"
    >
      <div
        aria-hidden
        className="aspect-[4/3] rounded-2xl border border-[var(--c-game)]/20 bg-[var(--c-game)]/5 flex items-center justify-center"
      >
        <span className="text-6xl font-bold text-[var(--c-game)]/80">§</span>
      </div>

      <div>
        <h2 className="text-2xl font-bold tracking-tight">Inizia la tua prima campagna</h2>
        <p className="mt-2 text-sm text-muted-foreground leading-relaxed">
          {gameTitle ? `${gameTitle} è una campagna mista lunga. ` : ''}
          Crea un party, fotografa le pagine narrative man mano e l&apos;app terrà il segno
          paragrafo per paragrafo, sera dopo sera.
        </p>
      </div>

      <ul className="flex flex-col gap-2.5">
        {[
          { ic: '§', label: "Riprendi all'ultimo paragrafo", sub: 'Anche dopo settimane' },
          {
            ic: '⊜',
            label: 'Glossario coerente',
            sub: 'I termini restano stabili sessione dopo sessione',
          },
          { ic: '◑', label: 'Più campagne in parallelo', sub: 'Un party per gruppo di amici' },
        ].map(row => (
          <li
            key={row.label}
            className="flex items-center gap-3 rounded-lg border border-border bg-background px-3.5 py-3"
          >
            <span
              aria-hidden
              className="flex h-8 w-8 flex-none items-center justify-center rounded-md bg-[var(--c-game)]/10 text-lg font-bold text-[var(--c-game)]"
            >
              {row.ic}
            </span>
            <div className="min-w-0">
              <div className="text-sm font-semibold">{row.label}</div>
              <div className="text-xs text-muted-foreground">{row.sub}</div>
            </div>
          </li>
        ))}
      </ul>

      <button
        type="button"
        onClick={onCreateCampaign}
        data-testid="gamebook-resume-empty-cta"
        className="rounded-md bg-[var(--c-game)] px-5 py-3 text-sm font-semibold text-white shadow-sm hover:opacity-95 disabled:opacity-50"
      >
        + Crea nuova campagna
      </button>
    </section>
  );
}
