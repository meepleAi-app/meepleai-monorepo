/**
 * GamebookUploadView — SP6 Phase C.1.C orchestrator skeleton (Issue #789).
 *
 * Foundation static-fixture orchestrator for the /gamebook/upload Tier L
 * 3-step wizard. Renders the 14-cell FSM via the
 * `lib/gamebook-upload/visual-test-fixture` sentinel + URL state SSOT.
 *
 * Foundation/Interactions split (per contract §1 §2 §6 §19 + Wave D.2 PR
 * #749 lesson AP10 — cherry-pick chain anti-pattern):
 *   - Foundation (THIS file): URL state SSOT (?step= ?gameId= ?batchId=
 *     ?fixture=), FSM cell rendering, i18n label resolution (Gate A ICU
 *     plural pre-resolved upstream), 5 read-only components wired (Step 1
 *     hero/search/cards/no-results), placeholders for Step 2/3 components
 *     pending Interactions sub-PR.
 *   - Interactions (next sub-PR): real-data hooks (`useGames`,
 *     `useBggSearch`, `usePhotoBatchUpload`, `usePhotoBatchStatus`),
 *     `CameraViewfinder` + `PageThumb` + `OfflineBanner` + `CancelModal` +
 *     `DesktopDropFallback` components, side-effect handlers (camera
 *     permissions, light-meter sampling, retry budget, idempotency-key
 *     submission, 403 rollback).
 *
 * Per Wave D.3 SessionSummaryView blueprint:
 *   - Pure-component pattern: orchestrator pre-resolves ALL i18n labels via
 *     `useTranslation().t(key, valuesOrDefault)` (Gate A) and injects via
 *     `labels` props.
 *   - URL state SSOT: no `useState` mirror of step/gameId/batchId/fixture.
 *     `?fixture=` override gated by `STATE_OVERRIDE_ENABLED` (foundation
 *     visual-test-fixture flag).
 *   - FSM derived via `deriveWizardState(...)` from foundation; in fixture
 *     mode the function is fed fixture-supplied query slices so cells map
 *     deterministically.
 *
 * Schema reality v1 carryover (Gate B): hooks NOT wired in Foundation —
 * default values stub `gameSearchQuery` / `bggSearchQuery` / `batchStatus`
 * as success-empty / pending-false. Interactions sub-PR replaces these
 * stubs with `useQuery` results from the real backend hooks (per contract
 * §3 hook composition).
 *
 * Production safety: in production builds `STATE_OVERRIDE_ENABLED` resolves
 * to `false` literal (visual-test-fixture flag), so the bundler can
 * dead-code-eliminate the `?fixture=` URL hatch. The fixture data itself is
 * dead code in prod because the orchestrator only reads it when the hatch
 * is enabled.
 *
 * @see docs/for-developers/frontend/contracts/gamebook-upload-hooks.md §1 §2 §6 §19
 */

'use client';

import { useCallback, useMemo, type ReactElement } from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import {
  GameSearchBar,
  GameSearchCard,
  NoResultsPanel,
  StepIndicator,
  type GameSearchBarLabels,
  type GameSearchCardLabels,
  type NoResultsPanelLabels,
  type StepIndicatorLabels,
} from '@/components/v2/gamebook';
import { useTranslation } from '@/hooks/useTranslation';
import {
  deriveWizardState,
  parseStateOverride,
  STATE_OVERRIDE_ENABLED,
  wizardFixtures,
  type GameSearchTab,
  type WizardFixture,
  type WizardFSMCell,
  type WizardStep,
} from '@/lib/gamebook-upload';

// ─── URL parsers ──────────────────────────────────────────────────────────

function parseStep(raw: string | null): WizardStep {
  if (raw === '2') return 2;
  if (raw === '3') return 3;
  return 1;
}

function parseTab(raw: string | null): GameSearchTab {
  return raw === 'bgg' ? 'bgg' : 'catalog';
}

// ─── Component ────────────────────────────────────────────────────────────

export function GamebookUploadView(): ReactElement {
  const { t } = useTranslation();
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  // ── URL state SSOT (Foundation: read-only) ──────────────────────────────
  const stepParam = parseStep(searchParams.get('step'));
  const gameIdParam = searchParams.get('gameId');
  const batchIdParam = searchParams.get('batchId');
  const tabParam = parseTab(searchParams.get('tab'));
  const queryParam = searchParams.get('q') ?? '';

  // ── Visual fixture override (gated by STATE_OVERRIDE_ENABLED) ──────────
  const fixtureOverride = STATE_OVERRIDE_ENABLED
    ? parseStateOverride(searchParams.get('fixture'))
    : null;

  const fixture: WizardFixture | null = useMemo(() => {
    if (fixtureOverride === null) return null;
    return wizardFixtures[fixtureOverride];
  }, [fixtureOverride]);

  // ── FSM cell derivation ─────────────────────────────────────────────────
  // Foundation passes fixture-supplied data when override active; otherwise
  // uses default stubs (Interactions sub-PR replaces with real hook results).
  const cell: WizardFSMCell = useMemo(() => {
    if (fixture !== null) {
      return deriveWizardState({
        step: fixture.step,
        gameId: fixture.gameId,
        batchId: fixture.batchId,
        gameSearchQuery: {
          isPending: false,
          isError: false,
          data: fixture.catalogResults,
        },
        searchInput: fixture.searchInput,
        activeTab: fixture.activeTab,
        bggSearchQuery: {
          isPending: fixture.bggIsLoading,
          isError: false,
          data: fixture.bggResults,
        },
        cameraPermission: fixture.cameraPermission,
        lightMeterValue: fixture.lightMeterValue,
        detectionScore: fixture.detectionScore,
        isCapturing: fixture.isCapturing,
        capturedCount: fixture.capturedPages.length,
        batchStatus: {
          isPending: false,
          isError: false,
          data: fixture.batchStatus,
        },
        isOffline: fixture.isOffline,
        retryAttempt: fixture.retryAttempt,
        nextRetryInMs: fixture.nextRetryInMs,
        cancelModalOpen: fixture.cancelModalOpen,
        isWizardCancelled: false,
      });
    }

    // Default Foundation stubs — hooks not wired yet (Gate B v1 carryover).
    return deriveWizardState({
      step: stepParam,
      gameId: gameIdParam,
      batchId: batchIdParam,
      gameSearchQuery: {
        isPending: false,
        isError: false,
        data: [],
      },
      searchInput: queryParam,
      activeTab: tabParam,
      bggSearchQuery: {
        isPending: false,
        isError: false,
        data: [],
      },
      cameraPermission: 'granted',
      lightMeterValue: 1.0,
      detectionScore: 1.0,
      isCapturing: false,
      capturedCount: 0,
      batchStatus: {
        isPending: false,
        isError: false,
        data: null,
      },
      isOffline: false,
      retryAttempt: 0,
      nextRetryInMs: 0,
      cancelModalOpen: false,
      isWizardCancelled: false,
    });
  }, [fixture, stepParam, gameIdParam, batchIdParam, queryParam, tabParam]);

  // ── Step indicator step (cell.kind → 1|2|3) ─────────────────────────────
  const currentStep: WizardStep = useMemo(() => {
    if (cell.kind === 'wizard-cancelled') return 1;
    if (cell.kind.startsWith('step1-')) return 1;
    if (cell.kind.startsWith('step2-')) return 2;
    return 3;
  }, [cell]);

  // ── i18n labels (Gate A: ICU plural pre-resolved here) ──────────────────
  const stepIndicatorLabels: StepIndicatorLabels = useMemo(
    () => ({
      step1: t('gamebook.upload.wizard.stepIndicator.stepGame'),
      step2: t('gamebook.upload.wizard.stepIndicator.stepPhoto'),
      step3: t('gamebook.upload.wizard.stepIndicator.stepIndex'),
      ariaCurrent: `Passo ${currentStep} di 3`,
    }),
    [t, currentStep]
  );

  const searchBarLabels: GameSearchBarLabels = useMemo(
    () => ({
      placeholder: t('gamebook.upload.wizard.step1.searchPlaceholder'),
      tabsCatalog: t('gamebook.upload.wizard.step1.tabCatalog'),
      tabsBgg: t('gamebook.upload.wizard.step1.tabBgg'),
      searchAria: t('gamebook.upload.wizard.step1.searchPlaceholder'),
    }),
    [t]
  );

  const cardLabelsFor = useCallback(
    (sharedByCount: number, isIndexed: boolean): GameSearchCardLabels => ({
      selectedAria: t('common.selected', 'Selezionato'),
      sharedByCount: sharedByCount > 0 ? `${sharedByCount}` : '',
      alreadyIndexedBadge: isIndexed
        ? t('gamebook.upload.wizard.step1.actionCardCreate', 'Già indicizzato')
        : '',
    }),
    [t]
  );

  const noResultsLabels = useCallback(
    (query: string): NoResultsPanelLabels => ({
      title: t('gamebook.upload.wizard.step1.noResultsTitle', { query }),
      description: t('gamebook.upload.wizard.step1.noResultsSubtitle'),
      actionCardCreate: {
        title: t('gamebook.upload.wizard.step1.actionCardCreate'),
        description: t('gamebook.upload.wizard.step1.actionCardCreateSubtitle'),
      },
      actionCardBgg: {
        title: t('gamebook.upload.wizard.step1.actionCardBgg'),
        description: t('gamebook.upload.wizard.step1.actionCardBggSubtitle'),
      },
      actionCardPrivate: {
        title: t('gamebook.upload.wizard.step1.actionCardPrivate'),
        description: t('gamebook.upload.wizard.step1.actionCardPrivateSubtitle'),
      },
    }),
    [t]
  );

  // ── URL state mutation helpers ──────────────────────────────────────────
  const buildQuery = useCallback(
    (overrides: Partial<Record<string, string | null>>): string => {
      const next = new URLSearchParams(searchParams.toString());
      Object.entries(overrides).forEach(([k, v]) => {
        if (v == null || v === '') next.delete(k);
        else next.set(k, v);
      });
      const qs = next.toString();
      return qs ? `?${qs}` : '';
    },
    [searchParams]
  );

  const handleQueryChange = useCallback(
    (next: string) => {
      router.replace(`${pathname}${buildQuery({ q: next || null })}`, { scroll: false });
    },
    [router, pathname, buildQuery]
  );

  const handleTabChange = useCallback(
    (next: GameSearchTab) => {
      router.replace(`${pathname}${buildQuery({ tab: next === 'catalog' ? null : next })}`, {
        scroll: false,
      });
    },
    [router, pathname, buildQuery]
  );

  const handleGameSelect = useCallback(
    (id: string) => {
      // Foundation behaviour: navigate to step 2 with selected gameId.
      // Interactions sub-PR adds the real selection write + camera priming.
      router.push(`${pathname}${buildQuery({ step: '2', gameId: id })}`);
    },
    [router, pathname, buildQuery]
  );

  // Step 1 NoResults action stubs — Interactions sub-PR wires real flows
  // (BGG search dialog, create-new modal, private install).
  const handleCreateNew = useCallback(() => {
    router.replace(`${pathname}${buildQuery({ q: null, tab: null })}`, { scroll: false });
  }, [router, pathname, buildQuery]);

  const handleSearchBgg = useCallback(() => {
    router.replace(`${pathname}${buildQuery({ tab: 'bgg' })}`, { scroll: false });
  }, [router, pathname, buildQuery]);

  const handleAddPrivate = useCallback(() => {
    // Stub — Interactions sub-PR routes through private install flow.
    router.replace(`${pathname}${buildQuery({ q: null, tab: null })}`, { scroll: false });
  }, [router, pathname, buildQuery]);

  // ── Render ──────────────────────────────────────────────────────────────
  return (
    <div
      data-slot="gamebook-upload-view"
      data-ui-state={cell.kind}
      className="mx-auto flex min-h-[calc(100vh-4rem)] max-w-4xl flex-col"
    >
      <StepIndicator currentStep={currentStep} labels={stepIndicatorLabels} />

      {renderCell({
        cell,
        searchBarLabels,
        cardLabelsFor,
        noResultsLabels,
        currentQuery: queryParam,
        currentTab: tabParam,
        onQueryChange: handleQueryChange,
        onTabChange: handleTabChange,
        onGameSelect: handleGameSelect,
        onCreateNew: handleCreateNew,
        onSearchBgg: handleSearchBgg,
        onAddPrivate: handleAddPrivate,
        t,
      })}
    </div>
  );
}

// ─── Cell renderer ────────────────────────────────────────────────────────
// Extracted so the FSM-cell switch lives in a single location and can be
// unit-tested independently. Step 2/3 cells render placeholder divs in
// Foundation — Interactions sub-PR replaces with real components.

interface CellRenderInput {
  readonly cell: WizardFSMCell;
  readonly searchBarLabels: GameSearchBarLabels;
  readonly cardLabelsFor: (sharedByCount: number, isIndexed: boolean) => GameSearchCardLabels;
  readonly noResultsLabels: (query: string) => NoResultsPanelLabels;
  readonly currentQuery: string;
  readonly currentTab: GameSearchTab;
  readonly onQueryChange: (q: string) => void;
  readonly onTabChange: (tab: GameSearchTab) => void;
  readonly onGameSelect: (gameId: string) => void;
  readonly onCreateNew: () => void;
  readonly onSearchBgg: () => void;
  readonly onAddPrivate: () => void;
  readonly t: (key: string, values?: Record<string, string | number>) => string;
}

function renderCell(input: CellRenderInput): ReactElement {
  const { cell } = input;

  switch (cell.kind) {
    // ── Step 1 cells ─────────────────────────────────────────────────────
    case 'step1-default':
      return (
        <Step1Shell
          {...input}
          query=""
          tabPending={false}
          renderResults={() => (
            <CatalogGrid
              cards={cell.catalogResults}
              source="catalog"
              selectedId={cell.selectedGameId}
              cardLabelsFor={input.cardLabelsFor}
              onSelect={input.onGameSelect}
            />
          )}
        />
      );

    case 'step1-searching':
      return (
        <Step1Shell
          {...input}
          query={cell.query}
          tabPending={false}
          renderResults={() => (
            <CatalogGrid
              cards={cell.activeTab === 'bgg' ? cell.bggResults : cell.catalogResults}
              source={cell.activeTab}
              selectedId={null}
              cardLabelsFor={input.cardLabelsFor}
              onSelect={input.onGameSelect}
            />
          )}
        />
      );

    case 'step1-no-results':
      return (
        <Step1Shell
          {...input}
          query={cell.query}
          tabPending={false}
          renderResults={() => (
            <NoResultsPanel
              query={cell.query}
              onCreateNew={input.onCreateNew}
              onSearchBgg={input.onSearchBgg}
              onAddPrivate={input.onAddPrivate}
              labels={input.noResultsLabels(cell.query)}
            />
          )}
        />
      );

    case 'step1-bgg-loading':
      return (
        <Step1Shell
          {...input}
          query={cell.query}
          tabPending={true}
          renderResults={() => (
            <div
              data-slot="bgg-loading-spinner"
              role="status"
              aria-live="polite"
              className="flex flex-col items-center gap-3 px-4 py-12 text-center"
            >
              <span aria-hidden="true" className="text-3xl">
                🌐
              </span>
              <p className="text-sm font-semibold text-foreground">
                {input.t('gamebook.upload.wizard.step1.bggLoadingTitle')}
              </p>
              <p className="text-xs text-slate-700">
                {input.t('gamebook.upload.wizard.step1.bggLoadingSubtitle')}
              </p>
            </div>
          )}
        />
      );

    // ── Step 2 cells (placeholders — Interactions sub-PR replaces) ───────
    case 'step2-ready':
    case 'step2-capturing':
    case 'step2-low-light':
    case 'step2-failed':
      return (
        <Step2Placeholder
          kind={cell.kind}
          gameId={cell.gameId}
          capturedCount={'capturedCount' in cell ? cell.capturedCount : 0}
          t={input.t}
        />
      );

    case 'step2-denied':
      return (
        <Step2Placeholder
          kind={cell.kind}
          gameId={cell.gameId}
          capturedCount={0}
          t={input.t}
          permissionState={cell.permissionState}
        />
      );

    // ── Step 3 cells (placeholders — Interactions sub-PR replaces) ───────
    case 'step3-progress':
      return (
        <Step3Placeholder
          kind={cell.kind}
          batchId={cell.batchId}
          processedPages={cell.processedPages}
          totalPages={cell.totalPages}
          t={input.t}
        />
      );

    case 'step3-partial':
      return (
        <Step3Placeholder
          kind={cell.kind}
          batchId={cell.batchId}
          processedPages={cell.processedPages}
          totalPages={cell.totalPages}
          t={input.t}
        />
      );

    case 'step3-complete':
      return (
        <Step3Placeholder
          kind={cell.kind}
          batchId={cell.batchId}
          processedPages={cell.totalPages}
          totalPages={cell.totalPages}
          t={input.t}
        />
      );

    case 'step3-offline':
      return (
        <Step3Placeholder
          kind={cell.kind}
          batchId={cell.batchId}
          processedPages={0}
          totalPages={0}
          t={input.t}
        />
      );

    case 'step3-cancel-modal':
      return (
        <div data-slot="step3-cancel-modal-shell" className="relative">
          <Step3Placeholder
            kind="step3-progress"
            batchId={cell.batchId}
            processedPages={0}
            totalPages={0}
            t={input.t}
          />
          <div
            data-slot="cancel-modal-placeholder"
            role="dialog"
            aria-modal="true"
            aria-label={input.t('gamebook.upload.wizard.cancelModal.title')}
            className="fixed inset-0 z-20 flex items-center justify-center bg-black/40 p-4"
          >
            <div className="w-full max-w-sm rounded-xl bg-card p-6 shadow-xl">
              <p className="text-base font-bold text-foreground">
                {input.t('gamebook.upload.wizard.cancelModal.title')}
              </p>
              <p className="mt-2 text-sm text-slate-700">
                {/* placeholder — Interactions sub-PR resolves count via ICU plural */}
                {input.t('gamebook.upload.wizard.step3.cancelButton')}
              </p>
            </div>
          </div>
        </div>
      );

    case 'wizard-cancelled':
      return (
        <div
          data-slot="wizard-cancelled-shell"
          role="status"
          className="flex flex-col items-center gap-3 px-4 py-12 text-center"
        >
          <p className="text-base font-semibold text-foreground">
            {input.t('gamebook.upload.wizard.step3.cancelButton')}
          </p>
        </div>
      );

    default: {
      // Exhaustiveness — TypeScript will flag any missed cell.
      const _exhaustive: never = cell;
      void _exhaustive;
      return <></>;
    }
  }
}

// ─── Step 1 shared shell ──────────────────────────────────────────────────

interface Step1ShellProps {
  readonly query: string;
  readonly tabPending: boolean;
  readonly searchBarLabels: GameSearchBarLabels;
  readonly currentTab: GameSearchTab;
  readonly onQueryChange: (q: string) => void;
  readonly onTabChange: (tab: GameSearchTab) => void;
  readonly renderResults: () => ReactElement;
}

function Step1Shell({
  query,
  tabPending,
  searchBarLabels,
  currentTab,
  onQueryChange,
  onTabChange,
  renderResults,
}: Step1ShellProps): ReactElement {
  return (
    <section data-slot="step1-shell" className="flex flex-1 flex-col gap-4 px-4 py-6 sm:px-6">
      <GameSearchBar
        query={query}
        onQueryChange={onQueryChange}
        activeTab={currentTab}
        onTabChange={onTabChange}
        isPending={tabPending}
        labels={searchBarLabels}
      />
      <div data-slot="step1-results">{renderResults()}</div>
    </section>
  );
}

// ─── Catalog grid ─────────────────────────────────────────────────────────

interface CatalogGridProps {
  readonly cards: ReadonlyArray<unknown>;
  readonly source: 'catalog' | 'bgg';
  readonly selectedId: string | null;
  readonly cardLabelsFor: (sharedByCount: number, isIndexed: boolean) => GameSearchCardLabels;
  readonly onSelect: (id: string) => void;
}

function CatalogGrid({
  cards,
  source,
  selectedId,
  cardLabelsFor,
  onSelect,
}: CatalogGridProps): ReactElement {
  if (cards.length === 0) {
    return (
      <div
        data-slot="catalog-grid-empty"
        role="status"
        aria-live="polite"
        className="flex flex-col items-center gap-2 px-4 py-12 text-center"
      >
        <span aria-hidden="true" className="text-3xl">
          📚
        </span>
        <p className="text-sm text-slate-700">
          {/* Placeholder copy — Interactions sub-PR replaces with proper i18n */}
          Nessun gioco da mostrare.
        </p>
      </div>
    );
  }
  return (
    <div
      data-slot="catalog-grid"
      data-source={source}
      className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4"
    >
      {cards.map((game, idx) => {
        // Discriminate by source — Foundation passes either CatalogGameRef or
        // BggSearchResult. Cast is safe because consumer types match parent.
        const isCatalog = source === 'catalog';
        const stableId = isCatalog
          ? (game as { id: string }).id
          : `bgg:${(game as { bggId: number }).bggId}`;
        const sharedByCount = isCatalog ? (game as { sharedByCount: number }).sharedByCount : 0;
        const isIndexed = isCatalog ? (game as { isIndexed: boolean }).isIndexed : false;

        return (
          <GameSearchCard
            key={`${source}-${stableId}-${idx}`}
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            game={game as any}
            source={source}
            isSelected={selectedId === stableId}
            onClick={onSelect}
            labels={cardLabelsFor(sharedByCount, isIndexed)}
          />
        );
      })}
    </div>
  );
}

// ─── Step 2 placeholder (Interactions sub-PR replaces) ───────────────────

interface Step2PlaceholderProps {
  readonly kind:
    | 'step2-ready'
    | 'step2-capturing'
    | 'step2-low-light'
    | 'step2-failed'
    | 'step2-denied';
  readonly gameId: string;
  readonly capturedCount: number;
  readonly t: (key: string, values?: Record<string, string | number>) => string;
  readonly permissionState?: 'denied' | 'unsupported';
}

function Step2Placeholder({
  kind,
  gameId,
  capturedCount,
  t,
  permissionState,
}: Step2PlaceholderProps): ReactElement {
  if (kind === 'step2-denied') {
    return (
      <section
        data-slot="step2-placeholder"
        data-cell={kind}
        data-permission={permissionState}
        data-game-id={gameId}
        className="flex flex-1 flex-col items-center gap-3 px-4 py-12 text-center"
      >
        <span aria-hidden="true" className="text-4xl">
          📷
        </span>
        <p className="text-base font-bold text-foreground">
          {permissionState === 'unsupported'
            ? t('gamebook.upload.wizard.step2.unsupportedTitle')
            : t('gamebook.upload.wizard.step2.deniedTitle')}
        </p>
        <p className="max-w-sm text-sm text-slate-700">
          {permissionState === 'unsupported'
            ? t('gamebook.upload.wizard.step2.unsupportedSubtitle')
            : t('gamebook.upload.wizard.step2.deniedSubtitle')}
        </p>
      </section>
    );
  }

  return (
    <section
      data-slot="step2-placeholder"
      data-cell={kind}
      data-game-id={gameId}
      data-captured-count={capturedCount}
      className="flex flex-1 flex-col items-center gap-3 px-4 py-12 text-center"
    >
      <span aria-hidden="true" className="text-4xl">
        📷
      </span>
      <p className="text-base font-semibold text-foreground">
        {t('gamebook.upload.wizard.step2.title')}
      </p>
      {/* Interactions sub-PR replaces this placeholder with CameraViewfinder */}
    </section>
  );
}

// ─── Step 3 placeholder (Interactions sub-PR replaces) ───────────────────

interface Step3PlaceholderProps {
  readonly kind: 'step3-progress' | 'step3-partial' | 'step3-complete' | 'step3-offline';
  readonly batchId: string;
  readonly processedPages: number;
  readonly totalPages: number;
  readonly t: (key: string, values?: Record<string, string | number>) => string;
}

function Step3Placeholder({
  kind,
  batchId,
  processedPages,
  totalPages,
  t,
}: Step3PlaceholderProps): ReactElement {
  // Gate A — ICU plural pre-resolved here for `progressLabel`.
  const progressLabel = t('gamebook.upload.wizard.step3.progressLabel', {
    processedPages,
    totalPages,
  });

  const titleKey =
    kind === 'step3-complete'
      ? 'gamebook.upload.wizard.step3.completeTitle'
      : kind === 'step3-partial'
        ? 'gamebook.upload.wizard.step3.partialTitle'
        : 'gamebook.upload.wizard.step3.progressTitle';

  return (
    <section
      data-slot="step3-placeholder"
      data-cell={kind}
      data-batch-id={batchId}
      className="flex flex-1 flex-col items-center gap-3 px-4 py-12 text-center"
    >
      <span aria-hidden="true" className="text-4xl">
        {kind === 'step3-complete' ? '✅' : kind === 'step3-offline' ? '📡' : '⚙️'}
      </span>
      <p className="text-base font-bold text-foreground">{t(titleKey)}</p>
      <p className="text-sm text-slate-700">
        {kind === 'step3-offline' ? t('gamebook.upload.wizard.offline.bannerTitle') : progressLabel}
      </p>
      {/* Interactions sub-PR replaces this with PageThumb grid + OfflineBanner */}
    </section>
  );
}
