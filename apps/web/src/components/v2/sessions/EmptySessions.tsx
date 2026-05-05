/**
 * EmptySessions — Wave D.1 v2 component (Issue #735).
 *
 * Mapped from `admin-mockups/design_files/sp4-sessions-index.jsx` (EmptyState).
 *
 * Pure component: all i18n strings injected via `labels` — no `useTranslation`.
 *
 * Discriminated by `kind`:
 *   - 'empty'          → 🎮 emoji + emptyCta → onPrimaryAction (navigate to new session)
 *   - 'filtered-empty' → 🔍 emoji + filteredEmptyCta → onPrimaryAction (clear filter)
 *   - 'loading'        → skeleton placeholders (8 rows)
 *   - 'error'          → ⚠️ emoji + errorCta → onPrimaryAction (retry)
 *
 * WCAG compliance:
 *   - role="alert" on error kind (live region, immediate announcement).
 *   - role="status" on empty + filtered-empty (polite live region).
 *   - CTA: bg-[hsl(240,60%,45%)] (session entity — l45 = 6.8:1 vs white, WCAG AAA).
 *
 * data-slot="sessions-empty-{kind}" for E2E selectors.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

export type EmptySessionsKind = 'empty' | 'filtered-empty' | 'loading' | 'error';

export interface EmptySessionsLabels {
  readonly emptyTitle: string;
  readonly emptyDescription: string;
  readonly emptyCta: string;
  readonly filteredEmptyTitle: string;
  readonly filteredEmptyDescription: string;
  readonly filteredEmptyCta: string;
  readonly loadingAriaLabel: string;
  readonly errorTitle: string;
  readonly errorDescription: string;
  readonly errorCta: string;
}

export interface EmptySessionsProps {
  readonly kind: EmptySessionsKind;
  readonly onPrimaryAction?: () => void;
  readonly labels: EmptySessionsLabels;
  readonly className?: string;
}

const KIND_ICON: Readonly<Record<EmptySessionsKind, string>> = {
  empty: '🎮',
  'filtered-empty': '🔍',
  loading: '',
  error: '⚠️',
};

const SESSION_CTA_CLASS = clsx(
  'mt-2 inline-flex h-10 items-center justify-center rounded-full px-5 text-sm font-semibold',
  'bg-[hsl(240,60%,45%)] text-white shadow-sm transition-colors',
  'hover:bg-[hsl(240,60%,38%)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
);

function SkeletonRow(): ReactElement {
  return (
    <div className="flex animate-pulse items-center gap-4 rounded-xl border border-border bg-card p-4">
      <div className="h-12 w-12 shrink-0 rounded-lg bg-muted" />
      <div className="flex flex-1 flex-col gap-2">
        <div className="h-3 w-2/5 rounded bg-muted" />
        <div className="h-2.5 w-3/5 rounded bg-muted" />
      </div>
    </div>
  );
}

export function EmptySessions({
  kind,
  onPrimaryAction,
  labels,
  className,
}: EmptySessionsProps): ReactElement {
  // Loading skeleton
  if (kind === 'loading') {
    return (
      <div
        data-slot="sessions-empty-loading"
        className={clsx('flex flex-col gap-3 px-4 sm:px-8', className)}
        role="status"
        aria-label={labels.loadingAriaLabel}
        aria-busy="true"
      >
        {Array.from({ length: 8 }).map((_, i) => (
          <SkeletonRow key={i} />
        ))}
      </div>
    );
  }

  const icon = KIND_ICON[kind];

  const title =
    kind === 'empty'
      ? labels.emptyTitle
      : kind === 'filtered-empty'
        ? labels.filteredEmptyTitle
        : labels.errorTitle;

  const description =
    kind === 'empty'
      ? labels.emptyDescription
      : kind === 'filtered-empty'
        ? labels.filteredEmptyDescription
        : labels.errorDescription;

  const cta =
    kind === 'empty'
      ? labels.emptyCta
      : kind === 'filtered-empty'
        ? labels.filteredEmptyCta
        : labels.errorCta;

  return (
    <div
      data-slot={`sessions-empty-${kind}`}
      className={clsx(
        'flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-border bg-muted/20 px-6 py-12 text-center',
        'mx-4 sm:mx-8',
        className
      )}
      role={kind === 'error' ? 'alert' : 'status'}
    >
      {icon && (
        <span className="text-4xl" aria-hidden="true">
          {icon}
        </span>
      )}

      <h2 className="text-lg font-semibold text-foreground sm:text-xl">{title}</h2>
      <p className="max-w-md text-sm text-muted-foreground">{description}</p>

      <button
        type="button"
        onClick={onPrimaryAction}
        className={SESSION_CTA_CLASS}
        data-slot="sessions-empty-cta"
      >
        {cta}
      </button>
    </div>
  );
}
