/**
 * HubToolkitsBody — orchestrator wrapper for `/toolkits` hub.
 *
 * Wave 4 (#1480). Maps the mockup HubToolkitsBody (sp4-hub-toolkits.jsx:500-567).
 * Composes Hero + Filters + (Grid | SkeletonGrid | EmptyFiltered | ErrorState)
 * based on the `state` prop. Pure presentational — filter state and FSM are
 * controlled by the parent page orchestrator (which owns the React Query call,
 * the filter logic, and the analytics tracking).
 */

'use client';

import type { JSX } from 'react';

import clsx from 'clsx';

import { ErrorState, type ErrorStateLabels } from './ErrorState';
import { HubEmptyFiltered, type HubEmptyFilteredLabels } from './HubEmptyFiltered';
import {
  HubFilters,
  type HubFiltersLabels,
  type HubFiltersSort,
  type HubFiltersStatus,
} from './HubFilters';
import {
  HubToolkitCardGrid,
  type HubToolkitCardGridLabels,
  type HubToolkitCardItem,
} from './HubToolkitCardGrid';
import {
  HubToolkitsHero,
  type HubToolkitsHeroLabels,
  type HubToolkitsHeroStat,
} from './HubToolkitsHero';
import { SkeletonCard } from './SkeletonCard';

export type HubToolkitsBodyState = 'default' | 'loading' | 'error';

export interface HubToolkitsBodyProps {
  readonly state: HubToolkitsBodyState;
  readonly toolkits: readonly HubToolkitCardItem[];
  readonly heroStats: readonly HubToolkitsHeroStat[];
  // Filter state (controlled)
  readonly query: string;
  readonly onQueryChange: (query: string) => void;
  readonly status: HubFiltersStatus;
  readonly onStatusChange: (status: HubFiltersStatus) => void;
  readonly sort: HubFiltersSort;
  readonly onSortChange: (sort: HubFiltersSort) => void;
  readonly onRetry: () => void;
  readonly onInstall?: (id: string) => void;
  readonly onCardClick?: (id: string) => void;
  // Labels (one set per child)
  readonly heroLabels: HubToolkitsHeroLabels;
  readonly filterLabels: HubFiltersLabels;
  readonly cardLabels: HubToolkitCardGridLabels;
  readonly emptyLabels: HubEmptyFilteredLabels;
  readonly errorLabels: ErrorStateLabels;
  readonly compact?: boolean;
  readonly className?: string;
}

const SKELETON_COUNT_DESKTOP = 12;
const SKELETON_COUNT_COMPACT = 6;

export function HubToolkitsBody({
  state,
  toolkits,
  heroStats,
  query,
  onQueryChange,
  status,
  onStatusChange,
  sort,
  onSortChange,
  onRetry,
  onInstall,
  onCardClick,
  heroLabels,
  filterLabels,
  cardLabels,
  emptyLabels,
  errorLabels,
  compact = false,
  className,
}: HubToolkitsBodyProps): JSX.Element {
  const isError = state === 'error';
  const isLoading = state === 'loading';
  const skeletonCount = compact ? SKELETON_COUNT_COMPACT : SKELETON_COUNT_DESKTOP;
  const gridCols = compact ? 'grid-cols-2' : 'grid-cols-2 sm:grid-cols-3 lg:grid-cols-4';

  function resetFilters(): void {
    onQueryChange('');
    onStatusChange('all');
    onSortChange('popular');
  }

  return (
    <div data-slot="toolkits-index-body" className={clsx('flex flex-col', className)}>
      <HubToolkitsHero stats={heroStats} labels={heroLabels} compact={compact} />

      {!isError && (
        <HubFilters
          query={query}
          onQueryChange={onQueryChange}
          status={status}
          onStatusChange={onStatusChange}
          sort={sort}
          onSortChange={onSortChange}
          count={toolkits.length}
          labels={filterLabels}
          compact={compact}
        />
      )}

      <div className={clsx(compact ? 'px-4 pb-6 pt-3.5' : 'px-8 pb-16 pt-6')}>
        {isError && <ErrorState labels={errorLabels} onRetry={onRetry} />}

        {!isError && isLoading && (
          <div className={clsx('grid gap-3 sm:gap-4', gridCols)}>
            {Array.from({ length: skeletonCount }).map((_, i) => (
              <SkeletonCard key={i} compact={compact} />
            ))}
          </div>
        )}

        {!isError && !isLoading && toolkits.length === 0 && (
          <HubEmptyFiltered labels={emptyLabels} onReset={resetFilters} />
        )}

        {!isError && !isLoading && toolkits.length > 0 && (
          <div className={clsx('grid gap-3 sm:gap-4', gridCols)}>
            {toolkits.map(t => (
              <HubToolkitCardGrid
                key={t.id}
                toolkit={t}
                labels={cardLabels}
                onInstall={onInstall}
                onClick={onCardClick}
                compact={compact}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
