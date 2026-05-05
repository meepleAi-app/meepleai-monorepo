import type { CSSProperties, JSX } from 'react';

import clsx from 'clsx';

import { Btn } from '../btn';

export interface PricingFeature {
  readonly label: string;
  readonly included: boolean;
}

export interface PricingCardCta {
  readonly label: string;
  readonly onClick?: () => void;
  readonly href?: string;
}

export interface PricingCardProps {
  readonly tier: string;
  readonly price: string;
  readonly description?: string;
  readonly features: readonly PricingFeature[];
  readonly cta: PricingCardCta;
  readonly highlighted?: boolean;
  readonly className?: string;
}

function CheckIcon(): JSX.Element {
  return (
    <svg
      aria-hidden="true"
      className="shrink-0 w-4 h-4"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ color: 'hsl(var(--e-toolkit))' }}
    >
      <polyline points="20 6 9 17 4 12" />
    </svg>
  );
}

function DashIcon(): JSX.Element {
  return (
    <svg
      aria-hidden="true"
      className="shrink-0 w-4 h-4 text-muted-foreground"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
    >
      <line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  );
}

export function PricingCard({
  tier,
  price,
  description,
  features,
  cta,
  highlighted = false,
  className,
}: PricingCardProps): JSX.Element {
  const rootStyle: CSSProperties | undefined = highlighted
    ? { borderColor: 'hsl(var(--e-game))' }
    : undefined;

  const ctaEl = cta.href ? (
    <Btn variant={highlighted ? 'primary' : 'outline'} entity="game" size="lg" fullWidth asChild>
      <a href={cta.href}>{cta.label}</a>
    </Btn>
  ) : (
    <Btn
      variant={highlighted ? 'primary' : 'outline'}
      entity="game"
      size="lg"
      fullWidth
      onClick={cta.onClick}
    >
      {cta.label}
    </Btn>
  );

  return (
    <div
      data-highlighted={highlighted ? 'true' : 'false'}
      className={clsx(
        'relative rounded-2xl bg-card p-6 md:p-8 border-2 border-border flex flex-col',
        highlighted && 'md:scale-105 shadow-xl',
        className
      )}
      style={rootStyle}
    >
      {highlighted && (
        <span
          className="absolute -top-3 left-1/2 -translate-x-1/2 px-3 py-1 rounded-full text-xs font-semibold text-white"
          style={{ backgroundColor: 'hsl(var(--e-game))' }}
        >
          Consigliato
        </span>
      )}

      <div className="text-sm uppercase tracking-wide text-muted-foreground font-semibold">
        {tier}
      </div>

      <div
        className="text-4xl font-bold mt-2 text-foreground"
        style={{ fontFamily: 'var(--f-display)' }}
      >
        {price}
      </div>

      {description && <p className="text-sm text-muted-foreground mt-2 m-0">{description}</p>}

      <ul className="flex flex-col gap-2 mt-6 mb-6 flex-1">
        {features.map((feature, i) => (
          <li
            key={`${feature.label}-${i}`}
            data-included={feature.included ? 'true' : 'false'}
            className={clsx(
              'flex items-center gap-2 text-sm',
              feature.included ? 'text-foreground' : 'text-muted-foreground'
            )}
          >
            {feature.included ? <CheckIcon /> : <DashIcon />}
            <span>{feature.label}</span>
          </li>
        ))}
      </ul>

      <div className="mt-auto">{ctaEl}</div>
    </div>
  );
}
