/**
 * ErrorBanner — inline error state for dashboard data hooks.
 * Pixel-faithful to admin-mockups/design_files/sp4-dashboard.jsx `ErrorState`.
 *
 * Hero remains visible above; this banner replaces the sections grid.
 * Danger-tinted card with circular warning icon + message + retry CTA.
 */

'use client';

import type { JSX } from 'react';

export interface ErrorBannerLabels {
  readonly title: string;
  readonly message: string;
  readonly retry: string;
}

export interface ErrorBannerProps {
  readonly labels: ErrorBannerLabels;
  readonly onRetry?: () => void;
}

export function ErrorBanner({ labels, onRetry }: ErrorBannerProps): JSX.Element {
  return (
    <div
      data-slot="dashboard-error-banner"
      role="alert"
      className="col-span-1 flex flex-col items-center rounded-[18px] border px-5 py-8 text-center sm:col-span-2 sm:px-8 sm:py-12"
      style={{
        background: 'hsl(var(--c-danger) / 0.08)',
        borderColor: 'hsl(var(--c-danger) / 0.3)',
      }}
    >
      <div
        aria-hidden="true"
        className="mb-3 flex h-14 w-14 items-center justify-center rounded-full text-[26px]"
        style={{
          background: 'hsl(var(--c-danger) / 0.15)',
          color: 'hsl(var(--c-danger))',
        }}
      >
        ⚠
      </div>
      <h3 className="mb-1 font-quicksand text-base font-extrabold text-foreground">
        {labels.title}
      </h3>
      <p className="mb-4 max-w-[360px] text-[12.5px] text-muted-foreground">{labels.message}</p>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="rounded-[10px] px-4 py-2 font-quicksand text-xs font-extrabold text-[#fff]"
          style={{ background: 'hsl(var(--c-danger))' }}
        >
          ↻ {labels.retry}
        </button>
      )}
    </div>
  );
}
