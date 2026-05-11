import type { CSSProperties, JSX, ReactNode } from 'react';

import clsx from 'clsx';

import { Btn } from '../btn';

export interface HeroGradientCta {
  readonly label: string;
  readonly href?: string;
  readonly onClick?: () => void;
}

export interface HeroGradientProps {
  readonly title: ReactNode;
  readonly subtitle?: string;
  readonly primaryCta?: HeroGradientCta;
  readonly secondaryCta?: HeroGradientCta;
  readonly className?: string;
  readonly children?: ReactNode;
}

const GRADIENT_STYLE: CSSProperties = {
  background:
    'linear-gradient(135deg, hsl(var(--e-game) / 0.08), hsl(var(--e-event) / 0.06), hsl(var(--e-player) / 0.08))',
};

function renderCta(cta: HeroGradientCta, variant: 'primary' | 'outline'): JSX.Element {
  if (cta.href) {
    return (
      <Btn variant={variant} entity="game" size="lg" asChild>
        <a href={cta.href}>{cta.label}</a>
      </Btn>
    );
  }
  return (
    <Btn variant={variant} entity="game" size="lg" onClick={cta.onClick}>
      {cta.label}
    </Btn>
  );
}

export function HeroGradient({
  title,
  subtitle,
  primaryCta,
  secondaryCta,
  className,
  children,
}: HeroGradientProps): JSX.Element {
  return (
    <section
      className={clsx('relative py-16 md:py-24 px-4 text-center overflow-hidden', className)}
      style={GRADIENT_STYLE}
    >
      <div className="max-w-4xl mx-auto">
        <h1
          className="font-heading font-bold text-4xl md:text-6xl tracking-tight text-foreground m-0 mb-4"
          style={{ fontFamily: 'var(--f-display)' }}
        >
          {title}
        </h1>
        {subtitle && (
          <p className="font-body text-lg text-muted-foreground leading-relaxed max-w-2xl mx-auto m-0">
            {subtitle}
          </p>
        )}
        {(primaryCta || secondaryCta) && (
          <div className="flex flex-col sm:flex-row gap-3 justify-center mt-8">
            {primaryCta && renderCta(primaryCta, 'primary')}
            {secondaryCta && renderCta(secondaryCta, 'outline')}
          </div>
        )}
        {children && <div className="mt-8">{children}</div>}
      </div>
    </section>
  );
}
