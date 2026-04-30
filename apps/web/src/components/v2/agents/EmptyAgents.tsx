/**
 * EmptyAgents — Wave B.2 v2 component (Issue #634).
 *
 * Mapped from `admin-mockups/design_files/sp4-agents-index.jsx`
 * (EmptyAgents + ErrorState + LoadingState).
 * Spec: docs/superpowers/specs/2026-04-30-v2-migration-wave-b-2-agents.md §3.2
 *
 * Pure component (mirror Wave B.1 GamesEmptyState): all i18n strings injected
 * via `labels`. Discriminates 4 surface states via `kind`:
 *   - 'loading'        → 6 skeleton cards desktop, 3 mobile (compact)
 *   - 'empty'          → 🤖 + "Crea il tuo primo agente" CTA → onCreateAgent
 *   - 'filtered-empty' → 🔎 + "Azzera filtri" CTA → onClearFilters
 *   - 'error'          → ⚠️ + "Riprova" CTA → onRetry
 *
 * Differs from GamesEmptyState:
 *   - Skeleton count: 6 desktop / 3 mobile (vs B.1: 8 desktop / 4 mobile) — to
 *     match agents grid auto-fit minmax(320px,1fr) average density.
 *   - Skeleton layout: matches AgentsResultsGrid CSS-grid auto-fit so the
 *     loading state mirrors the eventual results layout.
 *   - Icons: 🤖 (empty) / 🔎 (filtered) / ⚠️ (error) — agent-specific.
 */

import type { ReactElement } from 'react';

import clsx from 'clsx';

import { Skeleton } from '@/components/ui/feedback/skeleton';

export type EmptyAgentsKind = 'empty' | 'filtered-empty' | 'error' | 'loading';

export interface EmptyAgentsCopy {
  readonly title: string;
  readonly subtitle: string;
  readonly cta: string;
}

export interface EmptyAgentsLabels {
  readonly empty: EmptyAgentsCopy;
  readonly filteredEmpty: EmptyAgentsCopy;
  readonly error: EmptyAgentsCopy;
}

export interface EmptyAgentsProps {
  readonly kind: EmptyAgentsKind;
  readonly labels: EmptyAgentsLabels;
  readonly onCreateAgent?: () => void;
  readonly onClearFilters?: () => void;
  readonly onRetry?: () => void;
  readonly compact?: boolean;
  readonly className?: string;
}

const SKELETON_COUNT_DESKTOP = 6;
const SKELETON_COUNT_MOBILE = 3;

export function EmptyAgents({
  kind,
  labels,
  onCreateAgent,
  onClearFilters,
  onRetry,
  compact = false,
  className,
}: EmptyAgentsProps): ReactElement {
  if (kind === 'loading') {
    const count = compact ? SKELETON_COUNT_MOBILE : SKELETON_COUNT_DESKTOP;
    return (
      <div
        data-slot="agents-empty-state"
        data-kind="loading"
        className={clsx(
          compact
            ? 'grid grid-cols-1 gap-3 px-4 sm:grid-cols-2'
            : 'grid grid-cols-[repeat(auto-fit,minmax(320px,1fr))] gap-4 px-4 sm:px-8 max-w-[1280px] mx-auto',
          className
        )}
        aria-busy="true"
        aria-live="polite"
      >
        {Array.from({ length: count }, (_, index) => (
          <Skeleton
            key={index}
            data-slot="agents-empty-skeleton"
            className="h-48 w-full rounded-2xl"
          />
        ))}
      </div>
    );
  }

  const copy =
    kind === 'empty'
      ? labels.empty
      : kind === 'filtered-empty'
        ? labels.filteredEmpty
        : labels.error;

  const onCtaClick =
    kind === 'empty' ? onCreateAgent : kind === 'filtered-empty' ? onClearFilters : onRetry;

  const icon = kind === 'empty' ? '🤖' : kind === 'filtered-empty' ? '🔎' : '⚠️';

  return (
    <div
      data-slot="agents-empty-state"
      data-kind={kind}
      className={clsx(
        'flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-border bg-muted/20 px-6 py-12 text-center',
        compact ? 'mx-4' : 'mx-4 sm:mx-8',
        className
      )}
      role={kind === 'error' ? 'alert' : 'status'}
    >
      <span className="text-4xl" aria-hidden="true">
        {icon}
      </span>
      <h2 className="text-lg font-semibold text-foreground sm:text-xl">{copy.title}</h2>
      <p className="max-w-md text-sm text-muted-foreground">{copy.subtitle}</p>
      <button
        type="button"
        onClick={onCtaClick}
        className={clsx(
          'mt-2 inline-flex h-10 items-center justify-center rounded-full px-5 text-sm font-medium',
          'bg-primary text-primary-foreground shadow-sm transition-colors',
          'hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
        )}
        data-slot="agents-empty-state-cta"
      >
        {copy.cta}
      </button>
    </div>
  );
}
