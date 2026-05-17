/**
 * AgentDetailViewV2 — Wave C.2 (Issue #581) orchestrator.
 *
 * Phase 0.5 contract enforced (docs/frontend/contracts/agents-id-hooks.md):
 *   - agentId normalized to string|null at page boundary (NEVER undefined)
 *   - useAgent (parent hook) gated by !!agentId
 *   - 4 lazy sub-hooks gated cumulatively: !!agentId && agentQuery.isSuccess && data != null && tab === 'X'
 *   - Knowledge tab 2-STEP CHAIN: also gated by agentQuery.data.gameId != null (Cell 10 guard)
 *   - 5-state FSM via deriveAgentDetailUiState (agentId null check FIRST per Cell 1 contract)
 *   - Visual fixture short-circuit for CI prod build (IS_VISUAL_TEST_BUILD)
 *   - ?state= URL override (dev + visual-test only)
 *
 * Anti-patterns eliminated (mirrors Wave C.1 PR #702):
 *   ❌ const id = params?.id ?? '';     // 'undefined' string passed as agentId
 *   ❌ enabled: !!agentId && (fixture != null || agentQuery.data != null)  // fixture bypass
 *   ❌ Skipping agentQuery.data != null check (Cell 4 race)
 *   ❌ Skipping agentQuery.data.gameId != null check on Knowledge tab (Cell 10 cascade)
 *
 * Tab state lives here (useState) — pure presentation in child components.
 * KbDocsState/ChatHistoryState/SettingsState discriminated unions prevent
 * data + loading co-occurrence (contract sez. 4.3).
 *
 * Refs #581.
 */

'use client';

import { useMemo, useState, type ReactElement } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import {
  AgentDangerZone,
  AgentHero,
  AgentTabs,
  ChatHistoryTimeline,
  KbDocList,
  PersonaCard,
  AgentSettingsForm,
  SystemPromptViewer,
  panelIdFor,
  tabIdFor,
  type AgentTabKey,
  type ChatHistoryState,
  type ChatThreadEntry,
  type KbDocEntry,
  type KbDocsState,
  type SettingsState,
} from '@/components/features/agent-detail';
import { useAgent } from '@/hooks/queries/useAgent';
import { useAgentConfig } from '@/hooks/queries/useAgentConfig';
import { useAgentKbDocs, useAgentThreads } from '@/hooks/queries/useAgentData';
import { useTranslation } from '@/hooks/useTranslation';
import { deriveAgentDetailUiState } from '@/lib/agents/agent-detail-state';
import { deriveAgentVariant } from '@/lib/agents/agent-detail-variant';
import {
  IS_VISUAL_TEST_BUILD,
  tryLoadVisualTestFixture,
} from '@/lib/agents/agent-detail-visual-test-fixture';

// ─── State override hatch (dev / visual-test only) ─────────────────────────

const VALID_OVERRIDES = ['loading', 'error', 'not-found'] as const;
type StateOverride = (typeof VALID_OVERRIDES)[number];

const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;

function parseStateOverride(raw: string | null): StateOverride | null {
  if (!STATE_OVERRIDE_ENABLED) return null;
  if (raw == null) return null;
  return (VALID_OVERRIDES as readonly string[]).includes(raw) ? (raw as StateOverride) : null;
}

// ─── Props ─────────────────────────────────────────────────────────────────

export interface AgentDetailViewV2Props {
  /**
   * Normalized agentId from URL params.
   * MUST be string | null — NEVER undefined or the string 'undefined'.
   * See Phase 0.5 contract sez. 2.1.
   */
  readonly agentId: string | null;
}

// ─── Tab config ─────────────────────────────────────────────────────────────

const ALL_TAB_KEYS: AgentTabKey[] = ['identity', 'knowledge', 'performance', 'history', 'settings'];

// ─── Shells ─────────────────────────────────────────────────────────────────

function LoadingShell({ label }: { label: string }): ReactElement {
  return (
    <div
      data-slot="agent-detail-loading"
      aria-busy="true"
      aria-label={label}
      className="flex min-h-[60vh] flex-col gap-4 p-4 sm:p-8"
    >
      <div className="h-[240px] w-full animate-pulse rounded-2xl bg-muted" />
      <div className="h-12 w-full animate-pulse rounded-xl bg-muted" />
      <div className="h-40 w-full animate-pulse rounded-2xl bg-muted" />
    </div>
  );
}

function ErrorShell({
  title,
  subtitle,
  cta,
  onRetry,
}: {
  title: string;
  subtitle: string;
  cta: string;
  onRetry: () => void;
}): ReactElement {
  return (
    <div
      data-slot="agent-detail-error"
      className="flex min-h-[60vh] flex-col items-center justify-center gap-4 p-4 text-center sm:p-8"
    >
      <span aria-hidden="true" className="text-4xl">
        ⚠️
      </span>
      <h2 className="font-display text-[20px] font-extrabold text-foreground">{title}</h2>
      <p className="max-w-sm text-[14px] text-muted-foreground">{subtitle}</p>
      <button
        type="button"
        onClick={onRetry}
        data-slot="agent-detail-error-retry"
        className="rounded-md border border-border bg-transparent px-4 py-2 font-display text-[13px] font-bold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {cta}
      </button>
    </div>
  );
}

function NotFoundShell({
  title,
  subtitle,
  cta,
}: {
  title: string;
  subtitle: string;
  cta: string;
}): ReactElement {
  return (
    <div
      data-slot="agent-detail-not-found"
      className="flex min-h-[60vh] flex-col items-center justify-center gap-4 p-4 text-center sm:p-8"
    >
      <span aria-hidden="true" className="text-5xl">
        🤖
      </span>
      <h2 className="font-display text-[20px] font-extrabold text-foreground">{title}</h2>
      <p className="max-w-sm text-[14px] text-muted-foreground">{subtitle}</p>
      <a
        href="/agents"
        data-slot="agent-detail-not-found-cta"
        className="inline-flex items-center gap-1.5 rounded-md border-none bg-violet-700 px-4 py-2.5 font-display text-[13px] font-extrabold text-white shadow-md transition-shadow hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {cta}
      </a>
    </div>
  );
}

// ─── Tab locked banner ───────────────────────────────────────────────────────

function LockedTabPanel({ title, subtitle }: { title: string; subtitle: string }): ReactElement {
  return (
    <div
      data-slot="agent-detail-locked-panel"
      className="flex flex-col items-center justify-center gap-3 py-12 text-center"
    >
      <span aria-hidden="true" className="text-3xl">
        🔒
      </span>
      <p className="font-display text-[14px] font-bold text-foreground">{title}</p>
      <p className="max-w-xs text-[13px] text-muted-foreground">{subtitle}</p>
    </div>
  );
}

// ─── KbDocsState mapper ──────────────────────────────────────────────────────

/**
 * Maps kbDocsQuery result + agentGameId into the KbDocsState discriminated union.
 *
 * CRITICAL Cell 10 handling: if agent.gameId === null → return { kind: 'standalone' }
 * immediately, WITHOUT consulting the query (which is disabled by the gating contract).
 */
function mapKbDocsState(
  query: ReturnType<typeof useAgentKbDocs>,
  agentGameId: string | null | undefined,
  onRetry: () => void
): KbDocsState {
  // Cell 10: standalone agent — no game association, dedicated empty state
  if (agentGameId == null) return { kind: 'standalone' };

  if (query.isLoading) return { kind: 'loading' };
  if (query.isError) return { kind: 'error', retry: onRetry };
  const docs = query.data ?? [];
  if (docs.length === 0) return { kind: 'empty' };

  const mapped: KbDocEntry[] = docs.map(d => ({
    id: d.id,
    title: d.fileName,
    status: d.status as KbDocEntry['status'],
    sizeFormatted: '',
    pages: 0,
    chunks: 0,
  }));
  return { kind: 'success', docs: mapped };
}

// ─── ChatHistoryState mapper ─────────────────────────────────────────────────

function mapChatHistoryState(
  query: ReturnType<typeof useAgentThreads>,
  onRetry: () => void
): ChatHistoryState {
  if (query.isLoading) return { kind: 'loading' };
  if (query.isError) return { kind: 'error', retry: onRetry };
  const threads = query.data ?? [];
  if (threads.length === 0) return { kind: 'empty' };

  const mapped: ChatThreadEntry[] = threads.map(t => ({
    id: t.id,
    firstMessage: t.firstMessagePreview,
    when: t.createdAt,
    messageCount: t.messageCount,
    resolved: false,
  }));
  return { kind: 'success', threads: mapped };
}

// ─── SettingsState mapper ────────────────────────────────────────────────────

function mapSettingsState(
  query: ReturnType<typeof useAgentConfig>,
  isReadOnly: boolean,
  onRetry: () => void
): SettingsState {
  if (query.isLoading) return { kind: 'loading' };
  if (query.isError) return { kind: 'error', retry: onRetry };

  const config = query.data;
  const resolvedConfig = {
    strategy: config?.modelType ?? 'llama-3.3-70b-free',
    parameters: config
      ? {
          temperature: config.temperature,
          maxTokens: config.maxTokens,
          personality: config.personality,
          detailLevel: config.detailLevel,
          customInstructions: config.customInstructions,
        }
      : {},
  };

  if (isReadOnly) return { kind: 'read-only', config: resolvedConfig };
  return { kind: 'editable', config: resolvedConfig };
}

// ─── Main orchestrator ───────────────────────────────────────────────────────

/**
 * AgentDetailViewV2 — orchestrator for /agents/[id] v2 surface.
 *
 * Accepts `agentId: string | null` (normalized at page boundary by page.tsx).
 * Never accepts undefined — page.tsx is responsible for the normalization:
 *   const rawId = params?.id;
 *   const agentId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;
 */
export function AgentDetailViewV2({ agentId }: AgentDetailViewV2Props): ReactElement {
  const { t } = useTranslation();
  const router = useRouter();
  const searchParams = useSearchParams();

  // ── Tab state ──────────────────────────────────────────────────────────────
  const [tab, setTab] = useState<AgentTabKey>('identity');

  // ── URL override hatch ─────────────────────────────────────────────────────
  const stateOverride = parseStateOverride(searchParams.get('state'));

  // ── Visual fixture short-circuit (CI prod build) ───────────────────────────
  // Fixture is loaded when IS_VISUAL_TEST_BUILD === true and override not set.
  // Dead-code-eliminated in production builds (IS_VISUAL_TEST_BUILD === false literal).
  const fixture = useMemo(() => {
    if (!IS_VISUAL_TEST_BUILD) return null;
    if (stateOverride != null) return null;
    // Standalone state: load 'standalone' fixture (Cell 10 visual baseline)
    // Default state: load 'default' fixture (active agent with gameId)
    const fixtureState = searchParams.get('fixture') === 'standalone' ? 'standalone' : 'default';
    return tryLoadVisualTestFixture(fixtureState);
  }, [stateOverride, searchParams]);

  // ── Parent hook (Phase 0.5 sez. 2.2) ──────────────────────────────────────
  // agentId is string|null. Hook accepts string but gates internally via enabled.
  // We pass agentId ?? '' to satisfy hook type (empty string is falsy → disabled).
  const agentQuery = useAgent(agentId);

  // ── Effective agent data (fixture takes priority over real data) ───────────
  const agentData = fixture ?? agentQuery.data ?? null;

  // ── FSM state derivation ───────────────────────────────────────────────────
  const realKind = useMemo(() => {
    if (fixture != null) return 'default' as const;
    return deriveAgentDetailUiState({
      agentId,
      isLoading: agentQuery.isLoading,
      isError: agentQuery.isError,
      hasData: agentQuery.data != null,
    });
  }, [fixture, agentId, agentQuery.isLoading, agentQuery.isError, agentQuery.data]);

  const effectiveKind = stateOverride != null ? stateOverride : realKind;

  // ── Variant derivation (active / draft / archived) ─────────────────────────
  const variant = useMemo(
    () =>
      agentData
        ? deriveAgentVariant({
            isActive: agentData.isActive,
            invocationCount: agentData.invocationCount,
          })
        : 'active',
    [agentData]
  );

  // ─── Sub-hooks with explicit enabled options (Phase 0.5 sez. 2.2) ──────────
  // We shadow the imports with explicit enabled contract from Phase 0.5 sez. 2.2.
  // useAgentKbDocs internally uses: enabled: !!gameId (already correct for Cell 10).
  // We add tab-level laziness by computing effective enabled outside the hook call
  // and passing gameId=undefined when disabled (falsy → hook internal gate fires).

  const kbEnabled =
    !!agentId &&
    agentQuery.isSuccess &&
    agentQuery.data != null && // Cell 4 guard
    agentQuery.data.gameId != null && // Cell 10 guard (2-step chain)
    tab === 'knowledge';

  const threadsEnabled =
    !!agentId &&
    agentQuery.isSuccess &&
    agentQuery.data != null && // Cell 4 guard
    tab === 'history';

  const configEnabled =
    !!agentId &&
    agentQuery.isSuccess &&
    agentQuery.data != null && // Cell 4 guard
    tab === 'settings';

  // Knowledge sub-hook: gameId only passed when kbEnabled, undefined otherwise
  // useAgentKbDocs(gameId: string | undefined) — internal enabled: !!gameId
  // By passing undefined when not kbEnabled, we ensure the query never fires
  const kbDocsQueryGated = useAgentKbDocs(
    kbEnabled ? (agentQuery.data?.gameId ?? undefined) : undefined
  );

  // History sub-hook: agentId only passed when enabled ('' is falsy → disabled)
  const threadsQueryGated = useAgentThreads(threadsEnabled ? (agentId ?? '') : '');

  // Settings sub-hook: gameId (config is per-game in existing API)
  // useAgentConfig(gameId: string, enabled: boolean)
  const gameIdForConfig = agentQuery.data?.gameId ?? '';
  const configQueryGated = useAgentConfig(
    gameIdForConfig || (agentId ?? ''), // fallback to agentId if no gameId
    configEnabled
  );

  // ── Shell renders (FSM cells 1, 2, 3, 4) ──────────────────────────────────
  if (effectiveKind === 'loading') {
    return <LoadingShell label={t('pages.agentDetail.states.loading.ariaLabel')} />;
  }

  if (effectiveKind === 'error') {
    return (
      <ErrorShell
        title={t('pages.agentDetail.states.error.title')}
        subtitle={t('pages.agentDetail.states.error.subtitle')}
        cta={t('pages.agentDetail.states.error.cta')}
        onRetry={() => agentQuery.refetch()}
      />
    );
  }

  if (effectiveKind === 'not-found') {
    return (
      <NotFoundShell
        title={t('pages.agentDetail.states.notFound.title')}
        subtitle={t('pages.agentDetail.states.notFound.subtitle')}
        cta={t('pages.agentDetail.states.notFound.cta')}
      />
    );
  }

  // ── Default render — agent data guaranteed non-null (FSM cells 5-10) ───────
  // TypeScript: effectiveKind === 'default' → real FSM ensures agentData != null
  const safeAgent = agentData!;

  // ── Tab config (variant controls locked state) ─────────────────────────────
  const isDraft = variant === 'draft';
  const isArchived = variant === 'archived';

  const tabsConfig = ALL_TAB_KEYS.map(key => ({
    key,
    label: t(`pages.agentDetail.tabs.${key}`),
    locked: isDraft && (key === 'performance' || key === 'history') ? true : false,
  }));

  // ── Hero labels ────────────────────────────────────────────────────────────
  const heroLabels = {
    back: t('pages.agentDetail.hero.back'),
    backAriaLabel: t('pages.agentDetail.hero.backAriaLabel'),
    activeBadge: t('pages.agentDetail.hero.activeBadge'),
    draftBadge: t('pages.agentDetail.hero.draftBadge'),
    archivedBadge: t('pages.agentDetail.hero.archivedBadge'),
    metaType: 'Tipo: {type}',
    metaModel: 'Modello: {model}',
    metaCreated: 'Creato il {date}',
    metaLastUsed: 'Ultimo utilizzo: {date}',
    metaLastUsedNever: t('pages.agentDetail.hero.metaLastUsedNever'),
    metaInvocations: '{count} invocazioni',
    metaGameNone: t('pages.agentDetail.hero.metaGameNone'),
    ctaPlay: t('pages.agentDetail.hero.ctaPlay'),
    ctaSetup: t('pages.agentDetail.hero.ctaSetup'),
    ctaUnarchive: t('pages.agentDetail.hero.ctaUnarchive'),
    ctaShare: t('pages.agentDetail.hero.ctaShare'),
    ctaShareAriaLabel: t('pages.agentDetail.hero.ctaShareAriaLabel'),
    setupBannerTitle: t('pages.agentDetail.hero.setupBannerTitle'),
    setupBannerSubtitle: t('pages.agentDetail.hero.setupBannerSubtitle'),
    archivedBannerTitle: t('pages.agentDetail.hero.archivedBannerTitle'),
    archivedBannerSubtitle: t('pages.agentDetail.hero.archivedBannerSubtitle'),
  };

  // ── Persona/SystemPrompt labels ────────────────────────────────────────────
  const personaLabels = {
    title: t('pages.agentDetail.identity.personaTitle'),
    empty: t('pages.agentDetail.identity.personaEmpty'),
  };

  const systemPromptLabels = {
    title: t('pages.agentDetail.identity.systemPromptTitle'),
    empty: t('pages.agentDetail.identity.systemPromptEmpty'),
    hidden: t('pages.agentDetail.identity.systemPromptHidden'),
  };

  // ── KB doc labels ──────────────────────────────────────────────────────────
  const kbDocLabels = {
    title: t('pages.agentDetail.knowledge.title'),
    subtitle: t('pages.agentDetail.knowledge.subtitle'),
    loadingLabel: t('pages.agentDetail.states.loading.ariaLabel'),
    errorLabel: t('pages.agentDetail.states.error.title'),
    retryLabel: t('pages.agentDetail.states.error.cta'),
    empty: t('pages.agentDetail.knowledge.empty'),
    emptySubtitle: t('pages.agentDetail.knowledge.emptySubtitle'),
    uploadCta: t('pages.agentDetail.knowledge.uploadCta'),
    standaloneTitle: t('pages.agentDetail.knowledge.standaloneTitle'),
    standaloneSubtitle: t('pages.agentDetail.knowledge.standaloneSubtitle'),
    standaloneCta: t('pages.agentDetail.knowledge.standaloneCta'),
    docsCount: '{count} documenti',
    statusIndexed: t('pages.agentDetail.knowledge.statusIndexed'),
    statusProcessing: t('pages.agentDetail.knowledge.statusProcessing'),
    statusFailed: t('pages.agentDetail.knowledge.statusFailed'),
  };

  // ── Chat history labels ────────────────────────────────────────────────────
  const chatHistoryLabels = {
    title: t('pages.agentDetail.history.title'),
    loadingLabel: t('pages.agentDetail.states.loading.ariaLabel'),
    errorLabel: t('pages.agentDetail.states.error.title'),
    retryLabel: t('pages.agentDetail.states.error.cta'),
    empty: t('pages.agentDetail.history.empty'),
    emptySubtitle: t('pages.agentDetail.history.emptySubtitle'),
    threadCount: '{count} conversazioni',
    resolvedLabel: 'Risolto',
    unresolvedLabel: 'Aperto',
    messagesLabel: 'messaggi',
  };

  // ── Settings labels ────────────────────────────────────────────────────────
  const settingsLabels = {
    title: t('pages.agentDetail.settings.title'),
    strategyLabel: t('pages.agentDetail.settings.strategyLabel'),
    parametersLabel: t('pages.agentDetail.settings.parametersLabel'),
    readOnlyBanner: t('pages.agentDetail.settings.readOnlyBanner'),
    saveCta: t('pages.agentDetail.settings.saveCta'),
    cancelCta: t('pages.agentDetail.settings.cancelCta'),
    saveSuccess: t('pages.agentDetail.settings.saveSuccess'),
    saveError: t('pages.agentDetail.settings.saveError'),
    loadingLabel: t('pages.agentDetail.states.loading.ariaLabel'),
    errorLabel: t('pages.agentDetail.states.error.title'),
    retryLabel: t('pages.agentDetail.states.error.cta'),
  };

  // ── Danger zone labels ─────────────────────────────────────────────────────
  const dangerZoneLabels = {
    dangerZoneTitle: t('pages.agentDetail.settings.dangerZoneTitle'),
    archiveCta: t('pages.agentDetail.settings.archiveCta'),
    archiveConfirmTitle: t('pages.agentDetail.settings.archiveConfirmTitle'),
    archiveConfirmSubtitle: t('pages.agentDetail.settings.archiveConfirmSubtitle'),
    archiveConfirm: t('pages.agentDetail.settings.archiveConfirm'),
    archiveCancel: t('pages.agentDetail.settings.archiveCancel'),
  };

  // ── Map query states to discriminated unions ───────────────────────────────
  const kbDocsState: KbDocsState = mapKbDocsState(kbDocsQueryGated, safeAgent.gameId, () =>
    kbDocsQueryGated.refetch()
  );

  const chatHistoryState: ChatHistoryState = mapChatHistoryState(threadsQueryGated, () =>
    threadsQueryGated.refetch()
  );

  const settingsState: SettingsState = mapSettingsState(configQueryGated, isArchived, () =>
    configQueryGated.refetch()
  );

  return (
    <div data-slot="agent-detail-view" className="flex min-h-screen flex-col bg-background">
      {/* Hero — variant matrix (active/draft/archived) */}
      <AgentHero
        variant={variant}
        name={safeAgent.name}
        avatar="🤖"
        persona={null}
        meta={{
          type: safeAgent.type,
          invocations: safeAgent.invocationCount,
          lastUsed: safeAgent.lastInvokedAt ?? undefined,
          createdAt: safeAgent.createdAt,
        }}
        ctaPlay={variant === 'active' ? () => router.push(`/agents/${agentId}/chat`) : undefined}
        ctaSetup={variant === 'draft' ? () => router.push(`/agents/${agentId}/setup`) : undefined}
        ctaUnarchive={
          variant === 'archived' ? () => router.push(`/agents/${agentId}/unarchive`) : undefined
        }
        ctaShare={() => {
          if (typeof navigator !== 'undefined' && navigator.share) {
            void navigator.share({ title: safeAgent.name, url: window.location.href });
          }
        }}
        labels={heroLabels}
      />

      {/* Tab navigation — A11y: role=tablist inside component */}
      <AgentTabs
        tabs={tabsConfig}
        active={tab}
        onChange={setTab}
        ariaLabel={t('pages.agentDetail.tabs.ariaLabel')}
      />

      {/* Tab panels — role=tabpanel, aria-labelledby wired to tabIdFor */}
      <div className="container mx-auto max-w-4xl px-4 py-6 sm:px-8">
        {/* Identity tab — Cell 5: persona + system prompt (no sub-hook fetch) */}
        <div
          role="tabpanel"
          id={panelIdFor('identity')}
          aria-labelledby={tabIdFor('identity')}
          hidden={tab !== 'identity'}
          data-slot="agent-detail-panel-identity"
        >
          <div className="flex flex-col gap-5">
            <PersonaCard persona={null} labels={personaLabels} />
            <SystemPromptViewer prompt={null} labels={systemPromptLabels} />
          </div>
        </div>

        {/* Knowledge tab — Cells 6-10 */}
        <div
          role="tabpanel"
          id={panelIdFor('knowledge')}
          aria-labelledby={tabIdFor('knowledge')}
          hidden={tab !== 'knowledge'}
          data-slot="agent-detail-panel-knowledge"
        >
          <KbDocList state={kbDocsState} labels={kbDocLabels} />
        </div>

        {/* Performance tab — draft variant: LOCKED */}
        <div
          role="tabpanel"
          id={panelIdFor('performance')}
          aria-labelledby={tabIdFor('performance')}
          hidden={tab !== 'performance'}
          data-slot="agent-detail-panel-performance"
        >
          {isDraft ? (
            <LockedTabPanel
              title={t('pages.agentDetail.performance.lockedDraftTitle')}
              subtitle={t('pages.agentDetail.performance.lockedDraftSubtitle')}
            />
          ) : (
            <div data-slot="agent-detail-performance-stats" className="flex flex-col gap-4">
              <p className="font-display text-[14px] text-muted-foreground">
                {safeAgent.invocationCount > 0
                  ? `${safeAgent.invocationCount} invocazioni totali`
                  : t('pages.agentDetail.performance.empty')}
              </p>
            </div>
          )}
        </div>

        {/* History tab — draft variant: LOCKED */}
        <div
          role="tabpanel"
          id={panelIdFor('history')}
          aria-labelledby={tabIdFor('history')}
          hidden={tab !== 'history'}
          data-slot="agent-detail-panel-history"
        >
          {isDraft ? (
            <LockedTabPanel
              title={t('pages.agentDetail.history.lockedDraftTitle')}
              subtitle={t('pages.agentDetail.history.lockedDraftSubtitle')}
            />
          ) : (
            <ChatHistoryTimeline state={chatHistoryState} labels={chatHistoryLabels} />
          )}
        </div>

        {/* Settings tab — archived variant: read-only, danger zone hidden */}
        <div
          role="tabpanel"
          id={panelIdFor('settings')}
          aria-labelledby={tabIdFor('settings')}
          hidden={tab !== 'settings'}
          data-slot="agent-detail-panel-settings"
        >
          <div className="flex flex-col gap-6">
            <AgentSettingsForm
              state={settingsState}
              labels={settingsLabels}
              onSave={() => {
                /* mutations handled by child */
              }}
              onCancel={() => {
                /* no-op: form handles internally */
              }}
            />
            {/* Danger zone: only for active variant per contract sez. 4 */}
            <AgentDangerZone
              variant={variant}
              onArchive={() => router.push(`/agents/${agentId}/archive`)}
              labels={dangerZoneLabels}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
