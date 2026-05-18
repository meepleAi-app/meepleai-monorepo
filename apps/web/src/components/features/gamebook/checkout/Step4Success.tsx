'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface Step4Labels {
  readonly title: string;
  readonly subtitle: string;
  readonly recapLabels: {
    readonly previous: string;
    readonly purchased: string;
    readonly balance: string;
    readonly freeQuotaTitle: string;
    readonly resetIn: string;
    readonly rate: string;
  };
  readonly backToGameCta: string;
  readonly receiptLink: string;
}

export interface Step4SuccessProps {
  readonly previousCredits: number;
  readonly purchasedCredits: number;
  readonly freeQuotaUsed: number;
  readonly freeQuotaTotal: number;
  readonly labels: Step4Labels;
  readonly onBackToGame: () => void;
}

const CONFETTI_PIECES = 14;
const CONFETTI_COLORS = [
  'bg-entity-toolkit',
  'bg-entity-event',
  'bg-entity-agent',
  'bg-entity-chat',
  'bg-entity-game',
] as const;

export function Step4Success({
  previousCredits,
  purchasedCredits,
  freeQuotaUsed,
  freeQuotaTotal,
  labels,
  onBackToGame,
}: Step4SuccessProps): ReactElement {
  const balance = previousCredits + purchasedCredits;
  return (
    <div
      data-slot="checkout-step-4"
      className="relative flex flex-col items-stretch gap-4 p-5 text-center"
    >
      <div
        data-slot="confetti"
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 overflow-hidden"
      >
        {Array.from({ length: CONFETTI_PIECES }).map((_, i) => (
          <span
            key={i}
            className={clsx(
              'absolute top-[-10px] block h-3 w-[7px] rounded-sm opacity-85 motion-reduce:opacity-0',
              'animate-[checkout-confetti_2s_ease-in_infinite] motion-reduce:animate-none',
              CONFETTI_COLORS[i % CONFETTI_COLORS.length]
            )}
            style={{
              left: `${(i * 7.3) % 100}%`,
              animationDelay: `${(i * 0.13) % 1.4}s`,
              animationDuration: `${1.6 + (i % 3) * 0.4}s`,
            }}
          />
        ))}
      </div>
      <div className="flex flex-col items-center gap-1.5">
        <div aria-hidden="true" className="text-6xl motion-safe:animate-bounce">
          🎉
        </div>
        <h2 aria-live="polite" className="text-3xl font-extrabold text-foreground">
          {labels.title}
        </h2>
        <p className="text-sm text-muted-foreground">{labels.subtitle}</p>
      </div>
      <section className="flex flex-col gap-1.5 rounded-lg border border-border bg-card px-4 py-3.5 text-left">
        <Row label={labels.recapLabels.previous} value={previousCredits} />
        <Row label={labels.recapLabels.purchased} value={`+${purchasedCredits}`} accent />
        <div className="border-t border-border pt-1.5">
          <Row
            label={labels.recapLabels.balance}
            value={balance}
            testId="credits-balance"
            accent
            bold
          />
        </div>
        <div className="border-t border-border pt-1.5 text-xs text-muted-foreground">
          <Row
            label={labels.recapLabels.freeQuotaTitle}
            value={`${freeQuotaUsed} / ${freeQuotaTotal}`}
            muted
          />
        </div>
        <p className="font-mono text-[10px] text-muted-foreground">
          {labels.recapLabels.resetIn} · {labels.recapLabels.rate}
        </p>
      </section>
      <div className="flex flex-col gap-2">
        <button
          type="button"
          onClick={onBackToGame}
          className={clsx(
            'w-full rounded-lg bg-entity-session px-5 py-3.5 text-sm font-bold text-white',
            'transition-[filter] duration-150 hover:brightness-110 active:scale-[0.98]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.backToGameCta}
        </button>
        <a href="#" className="self-center text-xs text-muted-foreground hover:text-foreground">
          {labels.receiptLink}
        </a>
      </div>
    </div>
  );
}

function Row({
  label,
  value,
  accent = false,
  bold = false,
  muted = false,
  testId,
}: {
  readonly label: string;
  readonly value: string | number;
  readonly accent?: boolean;
  readonly bold?: boolean;
  readonly muted?: boolean;
  readonly testId?: string;
}): ReactElement {
  return (
    <div className="flex items-center justify-between text-sm">
      <span className={clsx(muted ? 'text-muted-foreground' : 'text-foreground')}>{label}</span>
      <span
        data-testid={testId}
        className={clsx(
          'font-mono tabular-nums',
          bold && 'text-base font-extrabold',
          accent && 'text-entity-toolkit',
          muted && 'text-muted-foreground'
        )}
      >
        {value}
      </span>
    </div>
  );
}
