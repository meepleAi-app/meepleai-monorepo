/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 primitive — see token-bridge-map.md for migration plan. */
import { forwardRef, type CSSProperties, type ReactNode } from 'react';

import clsx from 'clsx';

export interface AuthCardProps {
  readonly title: string;
  readonly subtitle?: string;
  readonly children: ReactNode;
  readonly footerAction?: ReactNode;
  readonly className?: string;
  readonly showBrand?: boolean;
}

const BRAND_MARK_STYLE: CSSProperties = {
  background:
    'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)) 60%, hsl(var(--c-player)))',
  boxShadow: '0 6px 20px hsl(var(--c-game) / 0.35)',
};

function BrandBlock(): ReactNode {
  return (
    <div className="flex flex-col items-center gap-1.5 mb-5">
      <div
        data-testid="auth-card-brand-mark"
        aria-hidden="true"
        className="flex items-center justify-center w-[52px] h-[52px] rounded-2xl text-white font-extrabold text-2xl font-heading"
        style={BRAND_MARK_STYLE}
      >
        M
      </div>
      <span className="font-heading font-bold text-base tracking-tight text-foreground">
        MeepleAI
      </span>
    </div>
  );
}

export const AuthCard = forwardRef<HTMLDivElement, AuthCardProps>(function AuthCard(
  { title, subtitle, children, footerAction, className, showBrand = true },
  ref
) {
  return (
    <div
      ref={ref}
      className={clsx(
        'flex flex-col items-stretch w-full min-h-full px-[18px] py-6 bg-background',
        className
      )}
    >
      {showBrand && (
        <div className="flex justify-center">
          <BrandBlock />
        </div>
      )}

      <div className="bg-card border border-border rounded-2xl shadow-lg p-6 sm:p-7">
        <h1 className="font-heading font-bold text-2xl tracking-tight text-foreground m-0 mb-1">
          {title}
        </h1>
        {subtitle && (
          <p className="font-body text-sm text-muted-foreground leading-relaxed m-0 mb-4">
            {subtitle}
          </p>
        )}
        {children}
      </div>

      {footerAction && (
        <div className="flex justify-center mt-5 text-sm text-muted-foreground font-body">
          {footerAction}
        </div>
      )}
    </div>
  );
});
