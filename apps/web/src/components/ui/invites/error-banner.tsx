/**
 * ErrorBanner — surface-level error/info banner for invite states.
 *
 * Wave A.5b (Issue #611). Mirrors mockup `sp3-accept-invite.jsx` Banner pattern
 * (lines 79-104), specialized for the 3 banner-driven invite states:
 *  - `token-expired`  → tone "error", red `c-danger` palette
 *  - `token-invalid`  → tone "error", red `c-danger` palette
 *  - `already-accepted` → tone "info", blue `c-info` palette
 *  - `state-switch-conflict` (mutation 409) → tone "warning"
 *  - `gone` (mutation 410) → tone "error"
 *
 * `role="alert"` for error tone (WCAG 4.1.3 Status Messages — assertive
 * announcement on screen readers since users are blocked from completing
 * the primary action). `role="status"` for info/warning tone.
 */

import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

export type ErrorBannerTone = 'error' | 'warning' | 'info' | 'success';

const TONE_CLASSES: Record<ErrorBannerTone, string> = {
  error:
    'border-[hsl(var(--c-danger)/0.25)] bg-[hsl(var(--c-danger)/0.10)] text-[hsl(var(--c-danger))]',
  warning:
    'border-[hsl(var(--c-warning)/0.25)] bg-[hsl(var(--c-warning)/0.10)] text-[hsl(var(--c-warning))]',
  info: 'border-[hsl(var(--c-info)/0.25)] bg-[hsl(var(--c-info)/0.10)] text-[hsl(var(--c-info))]',
  success:
    'border-[hsl(var(--c-toolkit)/0.25)] bg-[hsl(var(--c-toolkit)/0.10)] text-[hsl(var(--c-toolkit))]',
};

const TONE_ICON: Record<ErrorBannerTone, string> = {
  error: '✕',
  warning: '!',
  info: 'ℹ',
  success: '✓',
};

const TONE_ICON_BG: Record<ErrorBannerTone, string> = {
  error: 'bg-[hsl(var(--c-danger)/0.18)]',
  warning: 'bg-[hsl(var(--c-warning)/0.18)]',
  info: 'bg-[hsl(var(--c-info)/0.18)]',
  success: 'bg-[hsl(var(--c-toolkit)/0.18)]',
};

export interface ErrorBannerProps {
  readonly tone?: ErrorBannerTone;
  readonly children: ReactNode;
  readonly action?: ReactNode;
  readonly className?: string;
}

export function ErrorBanner({
  tone = 'error',
  children,
  action,
  className,
}: ErrorBannerProps): JSX.Element {
  return (
    <div
      role={tone === 'error' ? 'alert' : 'status'}
      data-slot="invite-error-banner"
      data-tone={tone}
      className={clsx(
        'mb-3.5 flex items-start gap-2.5 rounded-md border px-3 py-2.5 text-[12px] font-semibold leading-5',
        TONE_CLASSES[tone],
        className
      )}
    >
      <span
        aria-hidden="true"
        className={clsx(
          'mt-0.5 inline-flex h-[18px] w-[18px] flex-shrink-0 items-center justify-center rounded-full text-[11px] leading-none',
          TONE_ICON_BG[tone]
        )}
      >
        {TONE_ICON[tone]}
      </span>
      <span className="flex-1 leading-relaxed">{children}</span>
      {action ? <span className="shrink-0">{action}</span> : null}
    </div>
  );
}
