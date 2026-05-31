/* eslint-disable local/no-hardcoded-color-utility -- text-white / bg-white on entity-colored CTA + radio dot. Mockup .e-bg pattern; DS-12 will introduce primitives encoding bg via className. */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import {
  CHECKOUT_PACKS,
  formatEur,
  getCheckoutPack,
  type CheckoutPackId,
} from '@/lib/gamebook/checkout-packs';

export interface Step2Labels {
  readonly heading: string;
  readonly subheading: string;
  readonly disclaimer: string;
  readonly totalLabel: string;
  readonly continueCta: string;
  readonly packBadges: { readonly popular: string; readonly save: string };
  readonly packNames: { readonly starter: string; readonly mid: string; readonly pro: string };
  readonly packCreditsSuffix: string;
  readonly perParagraphSuffix: string;
}

export interface Step2PackPickerProps {
  readonly selectedId: CheckoutPackId;
  readonly labels: Step2Labels;
  readonly onSelect: (id: CheckoutPackId) => void;
  readonly onContinue: () => void;
}

export function Step2PackPicker({
  selectedId,
  labels,
  onSelect,
  onContinue,
}: Step2PackPickerProps): ReactElement {
  const selectedPack = getCheckoutPack(selectedId);
  return (
    <div data-slot="checkout-step-2" className="flex flex-col gap-4 p-5">
      <div className="flex flex-col gap-1">
        <h2 className="text-xl font-bold text-foreground">{labels.heading}</h2>
        <p className="text-sm text-muted-foreground">{labels.subheading}</p>
      </div>
      <div
        role="radiogroup"
        aria-label="Scelta pacchetto crediti"
        className="flex flex-col gap-2.5"
      >
        {CHECKOUT_PACKS.map(pack => {
          const selected = pack.id === selectedId;
          const badgeKind = pack.badge;
          const badgeLabel = badgeKind ? labels.packBadges[badgeKind] : null;
          return (
            <label
              key={pack.id}
              data-slot="pack-card"
              data-selected={selected || undefined}
              className={clsx(
                'relative cursor-pointer rounded-lg border-2 px-4 py-3.5 transition-transform',
                'hover:translate-y-[-1px]',
                'grid grid-cols-[1fr_auto_auto] grid-rows-[auto_auto_auto] gap-x-3 gap-y-0.5',
                selected
                  ? 'border-border-strong bg-entity-toolkit/10 shadow-[0_0_0_3px_hsl(var(--entity-toolkit)/0.15)]'
                  : 'border-border bg-card shadow-xs'
              )}
            >
              <input
                type="radio"
                name="pack"
                checked={selected}
                onChange={() => onSelect(pack.id)}
                aria-label={`Pacchetto ${labels.packNames[pack.id]}`}
                className="sr-only"
              />
              {badgeLabel && (
                <span
                  className={clsx(
                    'absolute -top-2 right-3 rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white',
                    badgeKind === 'popular' ? 'bg-entity-toolkit' : 'bg-entity-event'
                  )}
                >
                  {badgeLabel}
                </span>
              )}
              <span className="col-start-1 row-start-1 text-xs font-bold uppercase tracking-wider text-muted-foreground">
                {labels.packNames[pack.id]}
              </span>
              <span className="col-start-1 row-start-2 flex items-baseline gap-1.5 text-2xl font-extrabold text-entity-toolkit">
                <span className="tabular-nums">{pack.credits.toLocaleString('it')}</span>
                <span className="text-xs font-semibold lowercase text-muted-foreground">
                  {labels.packCreditsSuffix}
                </span>
              </span>
              <span className="col-start-2 row-start-2 text-2xl font-extrabold tabular-nums text-foreground">
                {formatEur(pack.priceEur)}
              </span>
              <span className="col-start-1 row-start-3 font-mono text-[11px] text-muted-foreground">
                {formatEur(pack.perParagraphEur)} {labels.perParagraphSuffix}
              </span>
              <span
                aria-hidden="true"
                className={clsx(
                  'col-start-3 row-span-3 flex h-5 w-5 items-center justify-center self-center justify-self-end rounded-full border-2 transition-colors',
                  selected
                    ? 'border-entity-toolkit bg-entity-toolkit'
                    : 'border-border bg-transparent'
                )}
              >
                {selected && <span className="h-2 w-2 rounded-full bg-white" />}
              </span>
            </label>
          );
        })}
      </div>
      <p className="rounded-md bg-muted px-3 py-2.5 text-[11px] leading-relaxed text-muted-foreground">
        {labels.disclaimer}
      </p>
      <div className="flex items-center gap-2.5">
        <div className="flex flex-col">
          <span className="font-mono text-[10px] uppercase tracking-wider text-muted-foreground">
            {labels.totalLabel}
          </span>
          <span
            data-testid="checkout-total"
            className="text-2xl font-extrabold tabular-nums text-foreground"
          >
            {formatEur(selectedPack.priceEur)}
          </span>
        </div>
        <button
          type="button"
          onClick={onContinue}
          className={clsx(
            'flex-1 rounded-lg bg-entity-toolkit px-5 py-3.5 text-sm font-bold text-white',
            'transition-[filter] duration-150 hover:brightness-110 active:scale-[0.98]',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
          )}
        >
          {labels.continueCta}
        </button>
      </div>
    </div>
  );
}
