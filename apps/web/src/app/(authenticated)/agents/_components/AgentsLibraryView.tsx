/**
 * AgentsLibraryView — Wave B.2 (Issue #634) orchestrator for `/agents`.
 *
 * Mirrors Wave B.1 GamesLibraryView pattern, adapted for agents (spec §3.4):
 *   - `useAgents({})` TanStack hook (no activeOnly flag — needs archived for
 *     the `archiviato` status filter to work client-side)
 *   - i18n labels resolved upfront via a single `useTranslation()`
 *   - 4 derivation pipelines (filterByStatus → matchQuery → sortAgents →
 *     deriveStats) over `AgentDto`
 *   - 5-state FSM: `default | loading | empty | filtered-empty | error`
 *   - `?state=...` URL override (NODE_ENV !== 'production' OR visual-test build)
 *   - Visual-test fixture short-circuit (`IS_VISUAL_TEST_BUILD`) for CI without
 *     a backend API at `:8080`
 *   - clearFilters CTA: resets `query` + `status` (sort preserved); when an
 *     override is active drops `?state=` from the URL via `router.push(pathname)`
 *
 * AC-13 separation of concerns: this orchestrator does NOT host the
 * `AgentCreationSheet` modal v1. It surfaces an `onCreateAgent` callback prop
 * that page.tsx wires to `setCreationSheetOpen(true)`. This keeps the
 * orchestrator agnostic of modal lifecycle and preserves the wave-1 modal
 * unchanged in production.
 */

'use client';

import { useCallback, useMemo, useState, type ReactElement } from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import {
  AgentFilters,
  AgentsHero,
  AgentsResultsGrid,
  EmptyAgents,
  type AgentFiltersLabels,
  type AgentsHeroLabels,
  type AgentsSortKey,
  type AgentsStatusKey,
  type EmptyAgentsLabels,
} from '@/components/v2/agents';
import { useAgents } from '@/hooks/queries/useAgents';
import { useTranslation } from '@/hooks/useTranslation';
import { deriveStats, filterByStatus, matchQuery, sortAgents } from '@/lib/agents/library-filters';
import { IS_VISUAL_TEST_BUILD, tryLoadVisualTestFixture } from '@/lib/agents/visual-test-fixture';

// ─── State override hatch (dev / visual-test only) ─────────────────────────

const VALID_OVERRIDES = ['loading', 'empty', 'filtered-empty', 'error'] as const;
type StateOverride = (typeof VALID_OVERRIDES)[number];

const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;

function parseStateOverride(raw: string | null): StateOverride | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  if (raw == null) return null;
  return (VALID_OVERRIDES as readonly string[]).includes(raw) ? (raw as StateOverride) : null;
}

type SurfaceKind = 'default' | 'loading' | 'empty' | 'filtered-empty' | 'error';

// ─── Props ──────────────────────────────────────────────────────────────────

export interface AgentsLibraryViewProps {
  /**
   * Hero CTA + empty-state CTA delegate. AC-13: page.tsx hosts the
   * `AgentCreationSheet` modal v1 and wires this callback to its open setter.
   * Optional so storybook / visual-test builds can render without wiring.
   */
  readonly onCreateAgent?: () => void;
}

// ─── Component ──────────────────────────────────────────────────────────────

export function AgentsLibraryView({ onCreateAgent }: AgentsLibraryViewProps = {}): ReactElement {
  const { t } = useTranslation();
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  const [query, setQuery] = useState<string>('');
  const [status, setStatus] = useState<AgentsStatusKey>('all');
  const [sort, setSort] = useState<AgentsSortKey>('recent');

  const stateOverride = parseStateOverride(searchParams.get('state'));

  const agentsQuery = useAgents({});

  const fixtureAgents = useMemo(() => {
    if (!IS_VISUAL_TEST_BUILD) return null;
    return tryLoadVisualTestFixture(stateOverride === 'empty' ? 'empty' : 'default');
  }, [stateOverride]);

  const agents = useMemo(
    () => fixtureAgents ?? agentsQuery.data ?? [],
    [fixtureAgents, agentsQuery.data]
  );

  const stats = useMemo(() => deriveStats(agents), [agents]);

  const filtered = useMemo(() => {
    const byStatus = filterByStatus(agents, status);
    const byQuery = byStatus.filter(agent => matchQuery(agent, query));
    return sortAgents(byQuery, sort);
  }, [agents, status, query, sort]);

  const heroLabels = useMemo<AgentsHeroLabels>(
    () => ({
      eyebrow: t('pages.agents.hero.eyebrow'),
      title: t('pages.agents.hero.title'),
      subtitle: t('pages.agents.hero.subtitle'),
      ctaCreate: t('pages.agents.hero.cta.create'),
    }),
    [t]
  );

  const heroStats = useMemo(
    () => [
      { label: t('pages.agents.hero.stats.attivo'), value: stats.attivo },
      { label: t('pages.agents.hero.stats.inSetup'), value: stats.inSetup },
      { label: t('pages.agents.hero.stats.archiviato'), value: stats.archiviato },
      { label: t('pages.agents.hero.stats.totalInvocations'), value: stats.totalInvocations },
    ],
    [t, stats]
  );

  const filtersLabels = useMemo<AgentFiltersLabels>(
    () => ({
      search: {
        placeholder: t('pages.agents.filters.search.placeholder'),
        ariaLabel: t('pages.agents.filters.search.ariaLabel'),
        clearAriaLabel: t('pages.agents.filters.search.clearAriaLabel'),
      },
      status: {
        label: t('pages.agents.filters.status.label'),
        options: {
          all: t('pages.agents.filters.status.all'),
          attivo: t('pages.agents.filters.status.attivo'),
          'in-setup': t('pages.agents.filters.status.in-setup'),
          archiviato: t('pages.agents.filters.status.archiviato'),
        },
      },
      sort: {
        label: t('pages.agents.filters.sort.label'),
        options: {
          recent: t('pages.agents.filters.sort.recent'),
          alpha: t('pages.agents.filters.sort.alpha'),
          used: t('pages.agents.filters.sort.used'),
        },
      },
      resultCount: (count: number) => t('pages.agents.filters.resultCount', { count }),
    }),
    [t]
  );

  const emptyLabels = useMemo<EmptyAgentsLabels>(
    () => ({
      empty: {
        title: t('pages.agents.empty.default.title'),
        subtitle: t('pages.agents.empty.default.subtitle'),
        cta: t('pages.agents.empty.default.cta'),
      },
      filteredEmpty: {
        title: t('pages.agents.empty.filtered.title'),
        subtitle: t('pages.agents.empty.filtered.subtitle'),
        cta: t('pages.agents.empty.filtered.cta'),
      },
      error: {
        title: t('pages.agents.empty.error.title'),
        subtitle: t('pages.agents.empty.error.subtitle'),
        cta: t('pages.agents.empty.error.cta'),
      },
    }),
    [t]
  );

  const realKind: SurfaceKind = useMemo(() => {
    if (agentsQuery.isLoading && fixtureAgents == null) return 'loading';
    if (agentsQuery.isError) return 'error';
    if (agents.length === 0) return 'empty';
    if (filtered.length === 0) return 'filtered-empty';
    return 'default';
  }, [agentsQuery.isLoading, agentsQuery.isError, fixtureAgents, agents.length, filtered.length]);

  const effectiveKind: SurfaceKind = stateOverride ?? realKind;

  const handleCreateAgent = useCallback(() => {
    onCreateAgent?.();
  }, [onCreateAgent]);

  const handleRetry = useCallback(() => {
    void agentsQuery.refetch?.();
  }, [agentsQuery]);

  const handleClearFilters = useCallback(() => {
    setQuery('');
    setStatus('all');
    if (stateOverride != null) {
      router.push(pathname);
    }
  }, [stateOverride, router, pathname]);

  return (
    <div data-slot="agents-library-view" className="flex flex-col gap-6 pb-24">
      <AgentsHero labels={heroLabels} stats={heroStats} onCreateAgent={handleCreateAgent} />
      <AgentFilters
        labels={filtersLabels}
        query={query}
        onQueryChange={setQuery}
        status={status}
        onStatusChange={setStatus}
        sort={sort}
        onSortChange={setSort}
        resultCount={filtered.length}
      />
      {effectiveKind === 'default' ? (
        <AgentsResultsGrid agents={filtered} />
      ) : (
        <EmptyAgents
          kind={effectiveKind}
          labels={emptyLabels}
          onCreateAgent={handleCreateAgent}
          onClearFilters={handleClearFilters}
          onRetry={handleRetry}
        />
      )}
    </div>
  );
}
