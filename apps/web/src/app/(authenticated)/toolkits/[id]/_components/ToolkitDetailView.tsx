/**
 * ToolkitDetailView — Stage 3 orchestrator for `/toolkits/[id]` (Issue #1145).
 *
 * Tier S route — single hook (useToolkitDetail), URL-bound tab state (?tab=),
 * Path C disabled-shell for 4 Phase-5 tabs (agent / kb / versions / ratings)
 * pending #822 + #819.
 *
 * Wraps `DetailPageLayout` primitive (#1112). FSM 4-state (loading / error /
 * not-found / default). Variant detection server-side via `viewerContext.isOwner`.
 *
 * Spec: docs/superpowers/specs/2026-05-14-stage3-toolkit-detail.md
 * Pattern reference: PlayerDetailView (#1138).
 */

'use client';

import { useCallback, useEffect, useMemo, useState, type ReactElement } from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import { ToolkitSummaryPanel } from '@/components/features/toolkit-detail';
import { DetailPageLayout } from '@/components/ui/detail-layout';
import { useToolkitDetail } from '@/hooks/queries/useToolkitDetail';
import { useTranslation } from '@/hooks/useTranslation';
import { trackEvent } from '@/lib/analytics/track-event';

import { DisabledTabPanel } from './DisabledTabPanel';
import { OverviewTabPanel } from './OverviewTabPanel';
import { ToolkitConnectionBar, type ToolkitConnectionPip } from './ToolkitConnectionBar';
import { TOOLKIT_TAB_KEYS, ToolkitTabs, type ToolkitTabKey } from './ToolkitTabs';
import { ToolsTabPanel } from './ToolsTabPanel';

const DEFAULT_TAB: ToolkitTabKey = 'overview';
const ENABLED_TABS: ReadonlyArray<ToolkitTabKey> = ['overview', 'tools'];
const DISABLED_TABS: ReadonlyArray<ToolkitTabKey> = ['agent', 'kb', 'versions', 'ratings'];

function parseTab(raw: string | null): ToolkitTabKey {
  if (raw && (TOOLKIT_TAB_KEYS as ReadonlyArray<string>).includes(raw)) {
    return raw as ToolkitTabKey;
  }
  return DEFAULT_TAB;
}

export interface ToolkitDetailViewProps {
  readonly toolkitId: string;
}

export function ToolkitDetailView({ toolkitId }: ToolkitDetailViewProps): ReactElement | null {
  const { t } = useTranslation();
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  // ── Tab state SSOT (URL) ─────────────────────────────────────────────────
  const initialTab = parseTab(searchParams.get('tab'));
  const [activeTab, setActiveTab] = useState<ToolkitTabKey>(
    DISABLED_TABS.includes(initialTab) ? DEFAULT_TAB : initialTab
  );

  // Normalize invalid / disabled tab in URL → default
  useEffect(() => {
    const raw = searchParams.get('tab');
    if (
      raw &&
      (!(TOOLKIT_TAB_KEYS as ReadonlyArray<string>).includes(raw) ||
        DISABLED_TABS.includes(raw as ToolkitTabKey))
    ) {
      const params = new URLSearchParams(searchParams.toString());
      params.delete('tab');
      const qs = params.toString();
      router.replace(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
    }
  }, [searchParams, pathname, router]);

  const updateTabInUrl = useCallback(
    (next: ToolkitTabKey) => {
      const params = new URLSearchParams(searchParams.toString());
      if (next === DEFAULT_TAB) params.delete('tab');
      else params.set('tab', next);
      const qs = params.toString();
      router.replace(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
    },
    [searchParams, pathname, router]
  );

  // ── Data ──────────────────────────────────────────────────────────────────
  const query = useToolkitDetail({ toolkitId, enabled: Boolean(toolkitId) });

  // ── Tab change handlers ───────────────────────────────────────────────────
  const handleTabChange = useCallback(
    (next: ToolkitTabKey) => {
      const previous = activeTab;
      setActiveTab(next);
      updateTabInUrl(next);
      trackEvent('toolkit_detail_tab_changed', {
        from: previous,
        to: next,
        toolkitId,
      });
    },
    [activeTab, updateTabInUrl, toolkitId]
  );

  const handleDisabledAttempt = useCallback(
    (key: ToolkitTabKey) => {
      trackEvent('toolkit_disabled_tab_attempted', {
        tab: key,
        toolkitId,
        variant: query.data?.viewerContext.isOwner ? 'own' : 'public',
      });
    },
    [toolkitId, query.data?.viewerContext.isOwner]
  );

  // ── ConnectionBar pip wiring ──────────────────────────────────────────────
  // Per spec AC2: 2 pip wired (game, tools), 4 pip dashed-empty placeholders.
  // Computed before FSM early-returns to satisfy hook-ordering rules.
  const toolkitData = query.data?.toolkit;
  const pips = useMemo<ReadonlyArray<ToolkitConnectionPip>>(() => {
    if (!toolkitData) return [];
    return [
      {
        id: 'tools',
        label: t('pages.toolkitDetail.pips.tools'),
        count: toolkitData.toolsCount,
        icon: '🔧',
      },
      {
        id: 'game',
        label: t('pages.toolkitDetail.pips.game'),
        count: toolkitData.gameName ?? undefined,
        icon: '🎲',
      },
      // Phase-5 placeholders
      { id: 'agent', label: t('pages.toolkitDetail.pips.agent'), icon: '🤖' },
      { id: 'kb', label: t('pages.toolkitDetail.pips.kb'), icon: '📄' },
      { id: 'author', label: t('pages.toolkitDetail.pips.author'), icon: '👤' },
      { id: 'sessions', label: t('pages.toolkitDetail.pips.sessions'), icon: '🎯' },
    ];
  }, [toolkitData, t]);

  // ── FSM shells ────────────────────────────────────────────────────────────
  if (query.isLoading) {
    return (
      <div
        data-slot="toolkit-detail-loading"
        role="status"
        aria-label={t('pages.toolkitDetail.loading.ariaLabel')}
        className="flex flex-col gap-4 p-6"
      >
        <div className="h-40 animate-pulse rounded-2xl bg-muted/60" />
        <div className="h-10 animate-pulse rounded-xl bg-muted/40" />
        <div className="h-64 animate-pulse rounded-xl bg-muted/30" />
      </div>
    );
  }

  if (query.isError) {
    return (
      <div
        data-slot="toolkit-detail-error"
        role="alert"
        className="flex flex-col items-center gap-3 p-12 text-center"
      >
        <span className="text-4xl" aria-hidden="true">
          ⚠️
        </span>
        <h1 className="font-bold font-[Quicksand] text-xl text-foreground">
          {t('pages.toolkitDetail.error.title')}
        </h1>
        <p className="max-w-md text-sm text-muted-foreground">
          {t('pages.toolkitDetail.error.description')}
        </p>
        <button
          type="button"
          onClick={() => query.refetch()}
          className="mt-2 inline-flex items-center rounded-xl bg-foreground px-4 py-2 font-bold font-[Quicksand] text-sm text-background"
        >
          {t('pages.toolkitDetail.error.retry')}
        </button>
      </div>
    );
  }

  const payload = query.data;
  if (!payload) {
    return (
      <div
        data-slot="toolkit-detail-not-found"
        className="flex flex-col items-center gap-3 p-12 text-center"
      >
        <span className="text-4xl" aria-hidden="true">
          🔍
        </span>
        <h1 className="font-bold font-[Quicksand] text-xl text-foreground">
          {t('pages.toolkitDetail.notFound.title')}
        </h1>
        <p className="max-w-md text-sm text-muted-foreground">
          {t('pages.toolkitDetail.notFound.description')}
        </p>
      </div>
    );
  }

  // ── Default render ────────────────────────────────────────────────────────
  // `toolkitData` already extracted above for the pips useMemo.
  const { viewerContext } = payload;
  const variant: 'own' | 'public' = viewerContext.isOwner ? 'own' : 'public';
  // After this guard, toolkitData is guaranteed defined (payload truthy → query.data.toolkit set).
  if (!toolkitData) return null;

  const summaryLabels = {
    authorChipPrefix: t('pages.toolkitDetail.hero.authorChipPrefix'),
    gameChipPrefix: t('pages.toolkitDetail.hero.gameChipPrefix'),
    statsHeading: t('pages.toolkitDetail.hero.statsHeading'),
    installCountLabel: t('pages.toolkitDetail.hero.installCount'),
    ratingLabel: t('pages.toolkitDetail.hero.rating'),
    versionLabel: t('pages.toolkitDetail.hero.version'),
    noRatings: t('pages.toolkitDetail.hero.noRatings'),
  };

  const tabsLabels = {
    tablistAriaLabel: t('pages.toolkitDetail.tabs.ariaLabel'),
    overview: t('pages.toolkitDetail.tabs.overview'),
    agent: t('pages.toolkitDetail.tabs.agent'),
    kb: t('pages.toolkitDetail.tabs.kb'),
    tools: t('pages.toolkitDetail.tabs.tools'),
    versions: t('pages.toolkitDetail.tabs.versions'),
    ratings: t('pages.toolkitDetail.tabs.ratings'),
    disabledTooltip: t('pages.toolkitDetail.tabs.disabledTooltip'),
  };

  const overviewLabels = {
    descriptionHeading: t('pages.toolkitDetail.overview.descriptionHeading'),
    metaHeading: t('pages.toolkitDetail.overview.metaHeading'),
    meta: {
      license: t('pages.toolkitDetail.overview.meta.license'),
      version: t('pages.toolkitDetail.overview.meta.version'),
      size: t('pages.toolkitDetail.overview.meta.size'),
      published: t('pages.toolkitDetail.overview.meta.published'),
      unknown: t('pages.toolkitDetail.overview.meta.unknown'),
    },
    includesHeading: t('pages.toolkitDetail.overview.includesHeading'),
    includes: {
      agent: t('pages.toolkitDetail.includes.agent'),
      kbDocs: t('pages.toolkitDetail.includes.kbDocs'),
      tools: t('pages.toolkitDetail.includes.tools'),
    },
  };

  const toolsLabels = {
    heading: t('pages.toolkitDetail.tools.heading'),
    description: t('pages.toolkitDetail.tools.description'),
    countTemplate: t('pages.toolkitDetail.tools.countTemplate'),
    emptyTitle: t('pages.toolkitDetail.tools.emptyTitle'),
    emptyBody: t('pages.toolkitDetail.tools.emptyBody'),
  };

  const disabledLabelsByTab: Record<
    ToolkitTabKey,
    { title: string; description: string; icon: string }
  > = {
    overview: { title: '', description: '', icon: '' }, // never disabled
    tools: { title: '', description: '', icon: '' }, // never disabled
    agent: {
      title: t('pages.toolkitDetail.disabled.agent.title'),
      description: t('pages.toolkitDetail.disabled.agent.description'),
      icon: '🤖',
    },
    kb: {
      title: t('pages.toolkitDetail.disabled.kb.title'),
      description: t('pages.toolkitDetail.disabled.kb.description'),
      icon: '📄',
    },
    versions: {
      title: t('pages.toolkitDetail.disabled.versions.title'),
      description: t('pages.toolkitDetail.disabled.versions.description'),
      icon: '📌',
    },
    ratings: {
      title: t('pages.toolkitDetail.disabled.ratings.title'),
      description: t('pages.toolkitDetail.disabled.ratings.description'),
      icon: '⭐',
    },
  };

  const phase5Badge = t('pages.toolkitDetail.disabled.phase5Badge');

  const connectionBarLabels = {
    ariaLabel: t('pages.toolkitDetail.connectionBar.ariaLabel'),
    emptyHint: t('pages.toolkitDetail.connectionBar.emptyHint'),
  };

  // ── Footer actions (variant-aware) ────────────────────────────────────────
  const footerActions = (
    <div className="flex flex-wrap items-center gap-2">
      {variant === 'own' ? (
        <>
          <button
            type="button"
            disabled
            title={tabsLabels.disabledTooltip}
            onClick={() =>
              trackEvent('toolkit_action_clicked', { action: 'edit', variant, toolkitId })
            }
            className="inline-flex cursor-not-allowed items-center rounded-xl bg-foreground/30 px-4 py-2 font-bold font-[Quicksand] text-sm text-background"
          >
            {t('pages.toolkitDetail.footer.editAction')}
          </button>
          <button
            type="button"
            disabled
            title={tabsLabels.disabledTooltip}
            onClick={() =>
              trackEvent('toolkit_action_clicked', { action: 'publish', variant, toolkitId })
            }
            className="inline-flex cursor-not-allowed items-center rounded-xl border border-border bg-card px-4 py-2 font-bold font-[Quicksand] text-sm text-muted-foreground"
          >
            {t('pages.toolkitDetail.footer.publishAction')}
          </button>
        </>
      ) : (
        <>
          <button
            type="button"
            disabled
            title={tabsLabels.disabledTooltip}
            onClick={() =>
              trackEvent('toolkit_action_clicked', { action: 'install', variant, toolkitId })
            }
            className="inline-flex cursor-not-allowed items-center rounded-xl bg-foreground/30 px-4 py-2 font-bold font-[Quicksand] text-sm text-background"
          >
            {t('pages.toolkitDetail.footer.installAction')}
          </button>
          <button
            type="button"
            onClick={() => {
              if (typeof navigator !== 'undefined' && navigator.clipboard) {
                navigator.clipboard.writeText(
                  typeof window !== 'undefined' ? window.location.href : ''
                );
              }
              trackEvent('toolkit_action_clicked', { action: 'share', variant, toolkitId });
            }}
            className="inline-flex items-center rounded-xl border border-border bg-card px-4 py-2 font-bold font-[Quicksand] text-sm text-foreground hover:border-border-strong"
          >
            {t('pages.toolkitDetail.footer.shareAction')}
          </button>
        </>
      )}
    </div>
  );

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <DetailPageLayout
      hero={
        <ToolkitSummaryPanel
          title={toolkitData.name}
          description={toolkitData.description}
          authorName={toolkitData.authorName}
          authorAvatarUrl={toolkitData.authorAvatarUrl}
          coverImageUrl={toolkitData.coverImageUrl}
          gameName={toolkitData.gameName}
          installCount={toolkitData.installCount}
          ratingAverage={toolkitData.ratingAverage}
          ratingCount={toolkitData.ratingCount}
          currentVersion={toolkitData.currentVersion}
          labels={summaryLabels}
        />
      }
      connections={<ToolkitConnectionBar pips={pips} labels={connectionBarLabels} />}
      tabs={
        <ToolkitTabs
          activeTab={activeTab}
          onChange={handleTabChange}
          disabledTabs={DISABLED_TABS}
          counts={{ tools: toolkitData.toolsCount, kb: toolkitData.kbDocsCount }}
          labels={tabsLabels}
          onDisabledAttempt={handleDisabledAttempt}
        />
      }
      footer={footerActions}
    >
      <OverviewTabPanel
        toolkit={toolkitData}
        labels={overviewLabels}
        hidden={activeTab !== 'overview'}
      />
      <ToolsTabPanel
        toolsCount={toolkitData.toolsCount}
        labels={toolsLabels}
        hidden={activeTab !== 'tools'}
      />
      {DISABLED_TABS.map(key => (
        <DisabledTabPanel
          key={key}
          tabKey={key}
          icon={disabledLabelsByTab[key].icon}
          labels={{
            title: disabledLabelsByTab[key].title,
            description: disabledLabelsByTab[key].description,
            phase5Badge,
          }}
          hidden={activeTab !== key}
        />
      ))}
      {/* enabledTabs reference to silence unused-vars in some configs */}
      <span aria-hidden="true" data-enabled-tabs={ENABLED_TABS.join(',')} hidden />
    </DetailPageLayout>
  );
}
