/* eslint-disable local/no-hardcoded-color-utility -- text-white on entity-colored CTA + spinner border-white on parent's entity-toolkit bg. Mockup .e-bg pattern; DS-12 will introduce primitives encoding bg via className. */

'use client';

import type { ReactElement, ReactNode } from 'react';

import clsx from 'clsx';

export type Step3SubState = 'filled' | 'loading' | 'failed';

export interface Step3Labels {
  readonly heading: string;
  /** Pre-resolved summary line e.g. "Pacchetto Starter · 100 crediti · 5 €". */
  readonly summary: string;
  readonly fieldLabels: {
    readonly card: string;
    readonly expiry: string;
    readonly cvc: string;
    readonly name: string;
    readonly country: string;
  };
  readonly trustChips: readonly string[];
  readonly payCta: string;
  readonly loadingCta: string;
  readonly backLink: string;
  readonly failedBanner: { readonly title: string; readonly detail: string };
  readonly recapLabels: { readonly credits: string; readonly vat: string; readonly total: string };
  readonly recapValues: { readonly credits: string; readonly total: string };
}

export interface Step3CheckoutFormProps {
  readonly subState: Step3SubState;
  readonly labels: Step3Labels;
  readonly onPay: () => void;
  readonly onBack: () => void;
}

const FIXED_PLACEHOLDERS = {
  card: '•••• •••• •••• 4242',
  cardBrand: 'VISA',
  expiry: '12 / 27',
  cvc: '•••',
  name: 'Sara Bianchi',
  country: '🇮🇹 Italia',
} as const;

export function Step3CheckoutForm({
  subState,
  labels,
  onPay,
  onBack,
}: Step3CheckoutFormProps): ReactElement {
  const isLoading = subState === 'loading';
  const isFailed = subState === 'failed';
  return (
    <div data-slot="checkout-step-3" className="flex flex-col gap-4 p-5">
      <div className="flex flex-col gap-1">
        <h2 className="text-xl font-bold text-foreground">{labels.heading}</h2>
        <p className="text-sm text-muted-foreground">{labels.summary}</p>
      </div>
      {isFailed && (
        <div
          role="alert"
          data-slot="checkout-error-banner"
          className="flex items-start gap-2.5 rounded-md border border-entity-event/40 bg-entity-event/10 px-3 py-2.5"
        >
          <span
            aria-hidden="true"
            className="flex h-5 w-5 items-center justify-center rounded-full bg-entity-event text-xs font-bold text-white"
          >
            !
          </span>
          <div className="flex-1">
            <p className="text-sm font-bold text-entity-event">{labels.failedBanner.title}</p>
            <p className="text-xs text-muted-foreground">{labels.failedBanner.detail}</p>
          </div>
        </div>
      )}
      <div data-slot="checkout-form" className="flex flex-col gap-2.5">
        <Field label={labels.fieldLabels.card}>
          <span className="font-mono text-sm text-foreground">{FIXED_PLACEHOLDERS.card}</span>
          <span
            aria-hidden="true"
            className="font-mono text-[10px] font-bold tracking-widest text-muted-foreground"
          >
            {FIXED_PLACEHOLDERS.cardBrand}
          </span>
        </Field>
        <div className="flex gap-2.5">
          <Field label={labels.fieldLabels.expiry} className="flex-1">
            <span className="font-mono text-sm text-foreground">{FIXED_PLACEHOLDERS.expiry}</span>
          </Field>
          <Field label={labels.fieldLabels.cvc} className="flex-1">
            <span className="font-mono text-sm text-foreground">{FIXED_PLACEHOLDERS.cvc}</span>
          </Field>
        </div>
        <Field label={labels.fieldLabels.name}>
          <span className="text-sm text-foreground">{FIXED_PLACEHOLDERS.name}</span>
        </Field>
        <Field label={labels.fieldLabels.country}>
          <span className="text-sm text-foreground">{FIXED_PLACEHOLDERS.country}</span>
          <span aria-hidden="true" className="text-[10px] text-muted-foreground">
            ▾
          </span>
        </Field>
      </div>
      <section className="flex flex-col gap-1.5 rounded-md border border-border bg-card px-3.5 py-3">
        <div className="flex items-center justify-between text-sm text-foreground">
          <span>{labels.recapLabels.credits}</span>
          <span className="tabular-nums">{labels.recapValues.credits}</span>
        </div>
        <div className="flex items-center justify-between text-[11px] text-muted-foreground">
          <span>{labels.recapLabels.vat}</span>
          <span>—</span>
        </div>
        <div className="flex items-center justify-between border-t border-border pt-1.5 text-base font-extrabold text-foreground">
          <span>{labels.recapLabels.total}</span>
          <span className="tabular-nums">{labels.recapValues.total}</span>
        </div>
      </section>
      <div className="flex flex-wrap justify-center gap-1.5">
        {labels.trustChips.map((chip) => (
          <span
            key={chip}
            className="inline-flex items-center gap-1 rounded-full bg-muted px-2.5 py-1 font-mono text-[10px] text-muted-foreground"
          >
            <span aria-hidden="true">✓</span>
            <span>{chip}</span>
          </span>
        ))}
      </div>
      <div className="flex flex-col gap-2">
        <button
          type="button"
          onClick={onPay}
          disabled={isLoading}
          aria-busy={isLoading || undefined}
          className={clsx(
            'flex w-full items-center justify-center gap-2 rounded-lg bg-entity-toolkit px-5 py-3.5 text-sm font-bold text-white',
            'transition-[filter] duration-150 hover:brightness-110 active:scale-[0.98]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
            'disabled:cursor-wait disabled:opacity-85'
          )}
        >
          {isLoading ? (
            <>
              <Spinner />
              <span>{labels.loadingCta}</span>
            </>
          ) : (
            <span>{labels.payCta}</span>
          )}
        </button>
        <button
          type="button"
          onClick={onBack}
          disabled={isLoading}
          className="self-center text-xs text-muted-foreground hover:text-foreground disabled:opacity-50"
        >
          {labels.backLink}
        </button>
      </div>
    </div>
  );
}

function Field({
  label,
  children,
  className,
}: {
  readonly label: string;
  readonly children: ReactNode;
  readonly className?: string;
}): ReactElement {
  return (
    <div className={clsx('flex flex-col gap-1', className)}>
      <span className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
        {label}
      </span>
      <div className="flex min-h-[44px] items-center justify-between rounded-md border border-border bg-card px-3.5 py-3">
        {children}
      </div>
    </div>
  );
}

function Spinner(): ReactElement {
  return (
    <span
      aria-hidden="true"
      className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-white motion-reduce:animate-none"
    />
  );
}
