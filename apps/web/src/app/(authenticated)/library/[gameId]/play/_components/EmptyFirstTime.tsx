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
 * FREEZE-compliant: solo var(--c-session) / var(--c-agent) / token semantici.
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
      className="flex flex-col gap-5 px-4 py-6 max-w-screen-sm lg:max-w-2xl mx-auto"
    >
      <div
        aria-hidden
        className="aspect-[4/3] rounded-2xl border border-[hsl(var(--c-session)/0.2)] bg-gradient-to-br from-[hsl(var(--c-session)/0.15)] to-[hsl(var(--c-game)/0.1)] flex items-center justify-center"
      >
        <span className="text-6xl">📖</span>
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
              className="flex h-8 w-8 flex-none items-center justify-center rounded-md bg-[var(--c-session)]/10 text-lg font-bold text-[var(--c-session)]"
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
        className="rounded-lg bg-[hsl(var(--c-session))] px-5 py-3.5 font-quicksand text-base font-bold text-white shadow-[0_6px_20px_hsl(var(--c-session)/0.35)] transition-transform duration-[180ms] ease-out hover:-translate-y-px hover:shadow-[0_8px_24px_hsl(var(--c-session)/0.45)] motion-reduce:transition-none disabled:opacity-50"
      >
        📖 Inizia campagna
      </button>
    </section>
  );
}
