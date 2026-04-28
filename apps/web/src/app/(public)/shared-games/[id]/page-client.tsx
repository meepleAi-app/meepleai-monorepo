/**
 * /shared-games/[id] — client body (V2, Wave A.4, Issue #603).
 *
 * Owns:
 *   - i18n resolution (single `useTranslation()` call → all labels resolved upfront)
 *   - URL hash state for `tab` (deep-link friendly, SSR-safe)
 *   - SSR seed → React Query → Hero + Tabs + entity list rendering
 *   - `?state=` visual-test override guarded by NODE_ENV
 *   - StickyCta gating for guest users (legacy contributor section reused 1:1)
 *
 * Tab activation = focus (WAI-ARIA "automatic" pattern). Absorbs Issue #588
 * Arrow-key navigation through `Tabs` keyboard contract.
 *
 * Mockup parity: `admin-mockups/design_files/sp3-shared-game-detail.jsx`
 * Spec: `docs/superpowers/specs/2026-04-28-v2-migration-wave-a-4-shared-game-detail.md` §3.4
 */

'use client';

import { useMemo, type JSX } from 'react';

import { useSearchParams } from 'next/navigation';

import { ContributorsSection } from '@/components/shared-games/ContributorsSection';
import {
  AgentListItem,
  ContributorsStrip,
  EmptyState,
  Hero,
  KbDocItem,
  StickyCta,
  TAB_KEYS,
  Tabs,
  ToolkitListItem,
  tabId,
  tabPanelId,
  type TabKey,
} from '@/components/ui/v2/shared-game-detail';
import { useSharedGameDetail } from '@/hooks/useSharedGameDetail';
import { useTranslation } from '@/hooks/useTranslation';
import { useUrlHashState } from '@/hooks/useUrlHashState';
import { type SharedGameDetailV2, type TopContributor } from '@/lib/api/shared-games';

const VALID_TAB_KEYS: ReadonlySet<TabKey> = new Set(TAB_KEYS);

function tabDeserialize(raw: string): TabKey {
  return VALID_TAB_KEYS.has(raw as TabKey) ? (raw as TabKey) : 'overview';
}

function tabSerialize(value: TabKey): string | null {
  return value === 'overview' ? null : value;
}

/**
 * 5-state surface for visual-test escape hatch. Mirror Wave A.3b pattern.
 *  - default     → real data renders
 *  - loading     → mounted skeleton, suppress data
 *  - error       → ErrorState card, suppress data
 *  - not-found   → handled server-side via notFound(); kept for symmetry
 *  - empty-tab   → forces `toolkits=[], agents=[], kbs=[]` to exercise EmptyState
 */
type DetailStateOverride = 'default' | 'loading' | 'error' | 'empty-tab';

const IS_NON_PROD = process.env.NODE_ENV !== 'production';
// Visual-regression CI sets this to '1' before `pnpm build` so the bootstrap
// workflow (which runs against a prod build) can still drive the `?state=`
// surface coverage. In real production deploys the env var is unset →
// constant-fold removes both the override branch and the fixture short-circuit.
const IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1';
const STATE_OVERRIDE_ENABLED = IS_NON_PROD || IS_VISUAL_TEST_BUILD;
const VALID_STATE_OVERRIDES: ReadonlySet<DetailStateOverride> = new Set([
  'default',
  'loading',
  'error',
  'empty-tab',
]);

function parseStateOverride(raw: string | null): DetailStateOverride | undefined {
  if (!STATE_OVERRIDE_ENABLED || !raw) return undefined;
  return VALID_STATE_OVERRIDES.has(raw as DetailStateOverride)
    ? (raw as DetailStateOverride)
    : undefined;
}

export interface SharedGameDetailPageClientProps {
  readonly id: string;
  readonly detail: SharedGameDetailV2;
  readonly contributors: readonly TopContributor[];
}

export function SharedGameDetailPageClient({
  id,
  detail,
  contributors,
}: SharedGameDetailPageClientProps): JSX.Element {
  const { t, formatMessage } = useTranslation();

  const searchParams = useSearchParams();
  const stateOverride = parseStateOverride(searchParams?.get('state') ?? null);

  const [activeTab, setActiveTab] = useUrlHashState<TabKey>('tab', 'overview', {
    serialize: tabSerialize,
    deserialize: tabDeserialize,
  });

  const { data, isError, isLoading } = useSharedGameDetail({ id, initialData: detail });

  // SSR seed guarantees `data` is defined on first paint, but TanStack Query
  // can briefly undefine it during refetch. Fall back to the SSR `detail` prop.
  const game: SharedGameDetailV2 = data ?? detail;

  // Override `empty-tab` clears nested arrays so EmptyState renders unobstructed.
  const toolkits = stateOverride === 'empty-tab' ? [] : (game.toolkits ?? []);
  const agents = stateOverride === 'empty-tab' ? [] : (game.agents ?? []);
  const kbs = stateOverride === 'empty-tab' ? [] : (game.kbs ?? []);

  const showError = stateOverride === 'error' || isError;
  const showLoading = stateOverride === 'loading' || (isLoading && !data);

  // --- Resolve labels (single useMemo per surface to keep child components pure) ---

  const heroLabels = useMemo(
    () => ({
      entityLabel: t('pages.sharedGameDetail.hero.entityLabel'),
      ratingAriaLabel: t('pages.sharedGameDetail.hero.ratingAriaLabel'),
      ratingOf: t('pages.sharedGameDetail.hero.ratingOf'),
      metaPlayers: t('pages.sharedGameDetail.hero.metaPlayers'),
      metaMinutes: t('pages.sharedGameDetail.hero.metaMinutes'),
      metaComplexity: t('pages.sharedGameDetail.hero.metaComplexity'),
      metaAuthor: t('pages.sharedGameDetail.hero.metaAuthor'),
      toolkitsLabel: t('pages.sharedGameDetail.hero.toolkitsLabel'),
      agentsLabel: t('pages.sharedGameDetail.hero.agentsLabel'),
      kbsLabel: t('pages.sharedGameDetail.hero.kbsLabel'),
    }),
    [t]
  );

  const tabsLabels = useMemo(
    () => ({
      tablistAriaLabel: t('pages.sharedGameDetail.tabs.tablistAriaLabel'),
    }),
    [t]
  );

  const tabDescriptors = useMemo(
    () => [
      {
        key: 'overview' as TabKey,
        label: t('pages.sharedGameDetail.tabs.overview'),
        icon: '📋',
      },
      {
        key: 'toolkits' as TabKey,
        label: t('pages.sharedGameDetail.tabs.toolkits'),
        icon: '🧰',
        count: game.toolkitsCount,
      },
      {
        key: 'agents' as TabKey,
        label: t('pages.sharedGameDetail.tabs.agents'),
        icon: '🤖',
        count: game.agentsCount,
      },
      {
        key: 'knowledge' as TabKey,
        label: t('pages.sharedGameDetail.tabs.knowledge'),
        icon: '📚',
        count: game.kbsCount,
      },
      {
        key: 'community' as TabKey,
        label: t('pages.sharedGameDetail.tabs.community'),
        icon: '👥',
        count: game.contributorsCount,
      },
    ],
    [t, game.toolkitsCount, game.agentsCount, game.kbsCount, game.contributorsCount]
  );

  const toolkitItemLabels = useMemo(
    () => ({
      authorPrefix: t('pages.sharedGameDetail.toolkits.authorPrefix'),
      updatedPrefix: t('pages.sharedGameDetail.toolkits.updatedPrefix'),
      previewLabel: t('pages.sharedGameDetail.toolkits.previewLabel'),
      previewAriaLabel: (name: string): string =>
        formatMessage({ id: 'pages.sharedGameDetail.toolkits.previewAriaLabel' }, { name }),
    }),
    [t, formatMessage]
  );

  const agentItemLabels = useMemo(
    () => ({
      updatedPrefix: t('pages.sharedGameDetail.agents.updatedPrefix'),
      invocationsLabel: (count: number): string =>
        formatMessage({ id: 'pages.sharedGameDetail.agents.invocationsLabel' }, { count }),
      tryLabel: t('pages.sharedGameDetail.agents.tryLabel'),
      tryAriaLabel: (name: string): string =>
        formatMessage({ id: 'pages.sharedGameDetail.agents.tryAriaLabel' }, { name }),
    }),
    [t, formatMessage]
  );

  const kbItemLabels = useMemo(
    () => ({
      indexedPrefix: t('pages.sharedGameDetail.knowledge.indexedPrefix'),
      chunksLabel: t('pages.sharedGameDetail.knowledge.chunksLabel'),
      openLabel: t('pages.sharedGameDetail.knowledge.openLabel'),
      openAriaLabel: (title: string): string =>
        formatMessage({ id: 'pages.sharedGameDetail.knowledge.openAriaLabel' }, { title }),
    }),
    [t, formatMessage]
  );

  const contributorsStripLabels = useMemo(
    () => ({
      title: t('pages.sharedGameDetail.community.contributorsTitle'),
      playersLabel: (count: number): string =>
        formatMessage({ id: 'pages.sharedGameDetail.community.playersLabel' }, { count }),
      overflowAriaLabel: (count: number): string =>
        formatMessage({ id: 'pages.sharedGameDetail.community.overflowAriaLabel' }, { count }),
    }),
    [t, formatMessage]
  );

  const stickyCtaLabels = useMemo(
    () => ({
      regionAriaLabel: t('pages.sharedGameDetail.stickyCta.regionAriaLabel'),
      mobileLabel: t('pages.sharedGameDetail.stickyCta.mobileLabel'),
      desktopHint: t('pages.sharedGameDetail.stickyCta.desktopHint'),
      desktopCtaLabel: t('pages.sharedGameDetail.stickyCta.desktopCtaLabel'),
    }),
    [t]
  );

  const contributorAvatars = useMemo(
    () =>
      contributors.map(c => ({
        id: c.userId,
        name: c.displayName,
        avatarUrl: c.avatarUrl,
      })),
    [contributors]
  );

  // --- Render ---

  if (showLoading) {
    return (
      <main
        data-testid="shared-game-detail-page"
        aria-busy="true"
        aria-live="polite"
        aria-label={t('pages.sharedGameDetail.states.loading.ariaLabel')}
        className="min-h-screen bg-background"
      >
        <div className="mx-auto max-w-[1024px] px-4 py-8 sm:px-6 lg:px-8">
          <div className="h-[360px] animate-pulse rounded-lg bg-muted" />
        </div>
      </main>
    );
  }

  if (showError) {
    return (
      <main data-testid="shared-game-detail-page" className="min-h-screen bg-background">
        <div className="mx-auto max-w-[1024px] px-4 py-8 sm:px-6 lg:px-8">
          <div className="rounded-lg border border-destructive/40 bg-destructive/5 p-6 text-center">
            <h1 className="font-display text-xl font-semibold text-foreground">
              {t('pages.sharedGameDetail.states.error.title')}
            </h1>
            <p className="mt-2 text-sm text-[hsl(var(--text-muted))]">
              {t('pages.sharedGameDetail.states.error.description')}
            </p>
          </div>
        </div>
      </main>
    );
  }

  return (
    <main
      data-testid="shared-game-detail-page"
      data-active-tab={activeTab}
      className="min-h-screen bg-background"
      style={{
        backgroundImage:
          'radial-gradient(120% 80% at 80% 0%, hsl(var(--c-game) / .07), transparent 60%), radial-gradient(80% 70% at 0% 100%, hsl(var(--c-toolkit) / .06), transparent 70%)',
      }}
    >
      <div className="mx-auto flex max-w-[1024px] flex-col gap-6 px-4 py-8 sm:px-6 lg:px-8">
        <Hero
          title={game.title}
          coverUrl={game.imageUrl && game.imageUrl.length > 0 ? game.imageUrl : null}
          year={game.yearPublished > 0 ? game.yearPublished : null}
          minPlayers={game.minPlayers > 0 ? game.minPlayers : null}
          maxPlayers={game.maxPlayers > 0 ? game.maxPlayers : null}
          playingTimeMinutes={game.playingTimeMinutes > 0 ? game.playingTimeMinutes : null}
          complexityRating={game.complexityRating}
          // Backend `averageRating` is on a 0..10 scale (BGG-style). Convert to 0..5.
          rating={game.averageRating != null ? game.averageRating / 2 : null}
          toolkitsCount={game.toolkitsCount}
          agentsCount={game.agentsCount}
          kbsCount={game.kbsCount}
          labels={heroLabels}
        />

        <Tabs
          tabs={tabDescriptors}
          activeTab={activeTab}
          onChange={setActiveTab}
          labels={tabsLabels}
          idBase="sg-detail"
        />

        {/* Tabpanels — statically rendered, hidden via attribute (mirror tabs.tsx contract) */}

        <section
          role="tabpanel"
          id={tabPanelId('sg-detail', 'overview')}
          aria-labelledby={tabId('sg-detail', 'overview')}
          hidden={activeTab !== 'overview'}
          className="flex flex-col gap-3"
        >
          <h2 className="m-0 font-display text-lg font-semibold text-foreground">
            {t('pages.sharedGameDetail.overview.descriptionHeading')}
          </h2>
          {game.description && game.description.length > 0 ? (
            <p className="m-0 whitespace-pre-line text-sm leading-relaxed text-foreground/90">
              {game.description}
            </p>
          ) : (
            <p className="m-0 text-sm italic text-[hsl(var(--text-muted))]">
              {t('pages.sharedGameDetail.overview.noDescription')}
            </p>
          )}
        </section>

        <section
          role="tabpanel"
          id={tabPanelId('sg-detail', 'toolkits')}
          aria-labelledby={tabId('sg-detail', 'toolkits')}
          hidden={activeTab !== 'toolkits'}
          className="flex flex-col gap-3"
        >
          <h2 className="m-0 font-display text-lg font-semibold text-foreground">
            {t('pages.sharedGameDetail.toolkits.sectionTitle')}
          </h2>
          {toolkits.length > 0 ? (
            <ul role="list" className="flex flex-col gap-2.5">
              {toolkits.map(tk => (
                <li key={tk.id}>
                  <ToolkitListItem
                    id={tk.id}
                    name={tk.name}
                    ownerName={tk.ownerName}
                    lastUpdatedAt={tk.lastUpdatedAt}
                    labels={toolkitItemLabels}
                  />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState
              kind="no-toolkits"
              labels={{
                title: t('pages.sharedGameDetail.toolkits.emptyTitle'),
                description: t('pages.sharedGameDetail.toolkits.emptyDescription'),
              }}
            />
          )}
        </section>

        <section
          role="tabpanel"
          id={tabPanelId('sg-detail', 'agents')}
          aria-labelledby={tabId('sg-detail', 'agents')}
          hidden={activeTab !== 'agents'}
          className="flex flex-col gap-3"
        >
          <h2 className="m-0 font-display text-lg font-semibold text-foreground">
            {t('pages.sharedGameDetail.agents.sectionTitle')}
          </h2>
          {agents.length > 0 ? (
            <ul role="list" className="flex flex-col gap-2.5">
              {agents.map(ag => (
                <li key={ag.id}>
                  <AgentListItem
                    id={ag.id}
                    name={ag.name}
                    invocationCount={ag.invocationCount}
                    lastUpdatedAt={ag.lastUpdatedAt}
                    labels={agentItemLabels}
                  />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState
              kind="no-agents"
              labels={{
                title: t('pages.sharedGameDetail.agents.emptyTitle'),
                description: t('pages.sharedGameDetail.agents.emptyDescription'),
              }}
            />
          )}
        </section>

        <section
          role="tabpanel"
          id={tabPanelId('sg-detail', 'knowledge')}
          aria-labelledby={tabId('sg-detail', 'knowledge')}
          hidden={activeTab !== 'knowledge'}
          className="flex flex-col gap-3"
        >
          <h2 className="m-0 font-display text-lg font-semibold text-foreground">
            {t('pages.sharedGameDetail.knowledge.sectionTitle')}
          </h2>
          {kbs.length > 0 ? (
            <ul role="list" className="flex flex-col gap-2">
              {kbs.map(kb => (
                <li key={kb.id}>
                  <KbDocItem
                    id={kb.id}
                    title={t('pages.sharedGameDetail.knowledge.documentTitleFallback')}
                    language={kb.language}
                    totalChunks={kb.totalChunks}
                    indexedAt={kb.indexedAt}
                    labels={kbItemLabels}
                  />
                </li>
              ))}
            </ul>
          ) : (
            <EmptyState
              kind="no-kbs"
              labels={{
                title: t('pages.sharedGameDetail.knowledge.emptyTitle'),
                description: t('pages.sharedGameDetail.knowledge.emptyDescription'),
              }}
            />
          )}
        </section>

        <section
          role="tabpanel"
          id={tabPanelId('sg-detail', 'community')}
          aria-labelledby={tabId('sg-detail', 'community')}
          hidden={activeTab !== 'community'}
          className="flex flex-col gap-5"
        >
          <h2 className="m-0 font-display text-lg font-semibold text-foreground">
            {t('pages.sharedGameDetail.community.sectionTitle')}
          </h2>
          <ContributorsStrip
            contributors={contributorAvatars}
            totalCount={game.contributorsCount}
            labels={contributorsStripLabels}
          />
          {/* Reuse legacy ContributorsSection 1:1 — Issue #2746 / Epic #2718. */}
          <ContributorsSection gameId={id} />
        </section>
      </div>

      <StickyCta signInHref="/login" labels={stickyCtaLabels} />
    </main>
  );
}
