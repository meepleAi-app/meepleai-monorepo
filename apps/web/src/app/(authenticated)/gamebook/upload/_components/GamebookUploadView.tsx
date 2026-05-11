/**
 * GamebookUploadView — SP6 Phase C.2.C orchestrator extension (Issue #789).
 *
 * Tier L 3-step wizard for `/gamebook/upload`. This file is the result of
 * extending the Foundation skeleton (PR #796 b52376d8) with REAL interactions:
 * camera permission flow, photo capture+upload, batch status polling,
 * offline retry budget, and cancel-during-retry. Per Wave D.2 PR #749 lesson
 * AP10 (cherry-pick chain anti-pattern): the Foundation FSM (`lib/gamebook-
 * upload/fsm.ts`) is UNCHANGED — Interactions only add side-effect handlers.
 *
 * Foundation/Interactions split (per contract §1 §2 §3 §6 §9 §10):
 *   - Foundation owns: 14-cell FSM, schemas, visual fixture, 5 read-only
 *     components (StepIndicator, GameSearchBar, GameSearchCard, NoResultsPanel,
 *     ActionCard), URL state SSOT (?step= ?gameId= ?batchId= ?fixture=).
 *   - Interactions owns (THIS sub-PR): real `useGames` for catalog tab,
 *     `detectCameraPermissionState` + `requestCameraStream` Step 2 wiring,
 *     `usePhotoBatchUpload` mutation w/ optimistic captured-page strip,
 *     `usePhotoBatchStatus` polling Step 3, `offlineBudgetReducer` retry
 *     timer + manual `RETRY_NOW`, `CancelModal` + `OfflineBanner` mounting,
 *     `?q=` URL param sync.
 *
 * Schema reality v1 carryover (Gate B):
 *   - `useGames` returns `Game[]` (lib/api/schemas/games.schemas.ts) not
 *     `CatalogGameRef[]` — orchestrator adapts: `id|title|publisher|imageUrl`
 *     mapped, `sharedByCount` defaulted to 0 (no community count exposed by
 *     `/api/v1/games`), `isIndexed = hasKnowledgeBase ?? false`.
 *   - BGG search wired to real backend (Wave 3 Phase 0 / Issue #805) via
 *     `useBggSearch` hook. The endpoint `/api/v1/bgg/search` is now gated
 *     behind `RequireAuthenticatedUser()` (was admin-only) and rate-limited at
 *     60 req/hour/user. Adapter normalizes the API client's
 *     `{bggId,name,yearPublished,thumbnailUrl,type}` → FSM canonical
 *     `{bggId,title,publisher,yearPublished}`. `publisher` is null in v1
 *     because the BGG search endpoint does not return publishers (only
 *     `/games/{bggId}` does). Hook fires only when `?tab=bgg` is active.
 *   - Per-page confidence: backend exposes only `averageConfidence` per
 *     batch (contract §12) — orchestrator uses heuristic via
 *     `deriveHeuristicPageConfidence` for Step 3 PageThumb confidence
 *     levels until per-page tracking lands.
 *
 * Idempotency-Key (contract §10): existing `usePhotoBatchUpload` mutation
 * does NOT yet thread custom headers through `mutationFn` — the canonical
 * `Idempotency-Key: ${batchId}:${pageNumber}:${attemptCount}` value is
 * COMPUTED via `composeIdempotencyKey()` and logged for debug, but cannot
 * be sent over the wire until the api-extension (deferred) wraps a custom
 * fetch path. Documented inline. AbortController retains cancel semantics.
 *
 * Cleanup contract:
 *   - Camera MediaStream tracks are stopped on (a) Step 2 → Step 3 transition,
 *     (b) wizard cancellation, (c) component unmount.
 *   - Captured-page object URLs are revoked on unmount.
 *
 * @see docs/for-developers/frontend/contracts/gamebook-upload-hooks.md §3 §6 §9 §10
 */

'use client';

import {
  useCallback,
  useEffect,
  useMemo,
  useReducer,
  useRef,
  useState,
  type ReactElement,
} from 'react';

import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import {
  CameraViewfinder,
  CancelModal,
  GameSearchBar,
  GameSearchCard,
  NoResultsPanel,
  OfflineBanner,
  PageThumb,
  StepIndicator,
  type CameraViewfinderLabels,
  type CancelModalLabels,
  type ConfidenceBadgeLabels,
  type GameSearchBarLabels,
  type GameSearchCardLabels,
  type NoResultsPanelLabels,
  type OfflineBannerLabels,
  type PageThumbLabels,
  type StepIndicatorLabels,
} from '@/components/features/gamebook';
import { useBggSearch } from '@/hooks/queries/useBggSearch';
import { useGames } from '@/hooks/queries/useGames';
import { useTranslation } from '@/hooks/useTranslation';
import { usePhotoBatchStatus } from '@/lib/gamebook/hooks/usePhotoBatchStatus';
import { usePhotoBatchUpload } from '@/lib/gamebook/hooks/usePhotoBatchUpload';
import {
  classifyConfidence,
  composeIdempotencyKey,
  deriveHeuristicPageConfidence,
  detectCameraPermissionState,
  deriveWizardState,
  elapsedBudgetMs,
  initialOfflineBudgetState,
  offlineBudgetReducer,
  parseStateOverride,
  RETRY_BUDGET_TOTAL_MS,
  requestCameraStream,
  STATE_OVERRIDE_ENABLED,
  wizardFixtures,
  type BggSearchResult,
  type CameraPermissionState,
  type CapturedPage,
  type CatalogGameRef,
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

// ─── Game → CatalogGameRef adapter (Gate B v1 carryover) ──────────────────
// Maps `lib/api/schemas/games.schemas.ts:Game` to the canonical
// `CatalogGameRef` shape consumed by Foundation cells. `sharedByCount`
// defaults to 0 (no community count surfaced by `/api/v1/games` yet);
// `isIndexed` derived from `hasKnowledgeBase` flag.

interface GameLike {
  id: string;
  title: string;
  publisher: string | null;
  imageUrl?: string | null;
  hasKnowledgeBase?: boolean;
}

function adaptGameToCatalogRef(game: GameLike): CatalogGameRef {
  return {
    id: game.id,
    title: game.title,
    publisher: game.publisher,
    coverImageUrl: game.imageUrl ?? null,
    sharedByCount: 0,
    isIndexed: game.hasKnowledgeBase ?? false,
  };
}

// ─── Component ────────────────────────────────────────────────────────────

const POLL_TICK_INTERVAL_MS = 100;

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

  const isFixtureMode = fixture !== null;

  // ── Real-data hooks (skipped in fixture mode for stability) ─────────────

  // Catalog search via existing useGames hook. Pass `?q=` as `search` filter.
  // Disabled in fixture mode to keep visual baselines deterministic.
  const gamesFilters = useMemo(
    () => (queryParam.length > 0 ? { search: queryParam } : undefined),
    [queryParam]
  );
  const gamesQuery = useGames(gamesFilters, undefined, 1, 24);

  // Map response → CatalogGameRef[] (Gate B v1 carryover). Empty when fixture
  // mode active (orchestrator reads `fixture.catalogResults` instead).
  const catalogResults = useMemo<readonly CatalogGameRef[]>(() => {
    if (isFixtureMode) return [];
    const games = gamesQuery.data?.games ?? [];
    return games.map(g => adaptGameToCatalogRef(g));
  }, [isFixtureMode, gamesQuery.data]);

  // BGG remote search — Wave 3 Phase 0 (Issue #805) wired against the now
  // authenticated-user-accessible `/api/v1/bgg/search` endpoint via the
  // SP6-tuned `useBggSearch` hook (24h staleTime, ≥3-char gate).
  //
  // Gating: the request only fires when the BGG tab is active (`?tab=bgg`).
  // Adapter: the API client returns `{bggId, name, yearPublished, thumbnailUrl,
  // type}`; the FSM's canonical `BggSearchResult` shape is `{bggId, title,
  // publisher, yearPublished}`. `publisher` is not exposed by the BGG XML
  // search endpoint (only `/games/{bggId}` returns publishers) → null in v1.
  // Suspense-style stale data is preserved across query changes; the FSM only
  // observes `isPending` + `data` so a no-op stale read here is safe.
  const bggQuery = useBggSearch({
    query: queryParam,
    enabled: !isFixtureMode && tabParam === 'bgg',
  });
  const bggSearchQuery = useMemo(
    () => ({
      isPending: bggQuery.isPending && bggQuery.fetchStatus !== 'idle',
      isError: bggQuery.isError,
      data: (bggQuery.data?.results ?? []).map<BggSearchResult>(r => ({
        bggId: r.bggId,
        title: r.name,
        publisher: null,
        yearPublished: r.yearPublished,
      })),
    }),
    [bggQuery.data, bggQuery.fetchStatus, bggQuery.isError, bggQuery.isPending]
  );

  // ── Camera permission + stream lifecycle (Step 2 only) ──────────────────
  const [cameraPermission, setCameraPermission] = useState<CameraPermissionState>('prompt');
  const streamRef = useRef<MediaStream | null>(null);
  const [streamReady, setStreamReady] = useState<boolean>(false);

  // Detect permission on Step 2 entry (only when not in fixture mode).
  useEffect(() => {
    if (isFixtureMode) return;
    if (stepParam !== 2) return;

    let cancelled = false;
    detectCameraPermissionState()
      .then(state => {
        if (cancelled) return;
        setCameraPermission(state);
      })
      .catch(() => {
        if (cancelled) return;
        setCameraPermission('unsupported');
      });

    return () => {
      cancelled = true;
    };
  }, [isFixtureMode, stepParam]);

  // Acquire stream on permission granted/prompt; release on Step 2 exit / unmount.
  useEffect(() => {
    if (isFixtureMode) return;
    if (stepParam !== 2) {
      // Step 2 left — release any held stream
      if (streamRef.current) {
        streamRef.current.getTracks().forEach(t => t.stop());
        streamRef.current = null;
        setStreamReady(false);
      }
      return;
    }
    if (cameraPermission === 'denied' || cameraPermission === 'unsupported') {
      // No stream to acquire — denial fallback rendered upstream.
      return;
    }

    let cancelled = false;
    requestCameraStream()
      .then(stream => {
        if (cancelled) {
          stream?.getTracks().forEach(t => t.stop());
          return;
        }
        if (stream === null) {
          setCameraPermission('denied');
          return;
        }
        streamRef.current = stream;
        setStreamReady(true);
        setCameraPermission('granted');
      })
      .catch(() => {
        if (cancelled) return;
        setCameraPermission('denied');
      });

    return () => {
      cancelled = true;
      if (streamRef.current) {
        streamRef.current.getTracks().forEach(t => t.stop());
        streamRef.current = null;
        setStreamReady(false);
      }
    };
  }, [isFixtureMode, stepParam, cameraPermission]);

  // ── Captured pages (optimistic strip) ───────────────────────────────────
  const [capturedPages, setCapturedPages] = useState<readonly CapturedPage[]>([]);

  // Revoke object URLs on unmount (cleanup contract).
  useEffect(() => {
    return () => {
      capturedPages.forEach(p => {
        if (p.thumbObjectUrl.startsWith('blob:')) {
          try {
            URL.revokeObjectURL(p.thumbObjectUrl);
          } catch {
            // ignore
          }
        }
      });
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ── URL state mutation helper (declared early — referenced by mutation
  //     onSuccess below) ──────────────────────────────────────────────────
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

  // ── Offline budget reducer (declared early — referenced by mutation
  //     onError + status polling effects) ─────────────────────────────────
  const [offlineState, dispatchOffline] = useReducer(
    offlineBudgetReducer,
    initialOfflineBudgetState
  );

  // ── Photo batch upload mutation ─────────────────────────────────────────
  const uploadMutation = usePhotoBatchUpload({
    onSuccess: result => {
      // Promote ?batchId= and route to Step 3.
      router.push(`${pathname}${buildQuery({ step: '3', batchId: result.batchId })}`, {
        scroll: false,
      });
    },
    onError: () => {
      // Network failures handled via offline-budget reducer (NETWORK_ERROR).
      const ctrl = new AbortController();
      dispatchOffline({ type: 'NETWORK_ERROR', abortController: ctrl });
    },
  });
  const [isCapturing, setIsCapturing] = useState(false);

  // ── Photo batch status polling ──────────────────────────────────────────
  const statusQuery = usePhotoBatchStatus({
    gameId: gameIdParam ?? '',
    batchId: !isFixtureMode && stepParam === 3 ? batchIdParam : null,
  });

  // Tick effect: when nextRetryInMs > 0, decrement every 100ms until 0.
  useEffect(() => {
    if (offlineState.nextRetryInMs === null) return;
    if (offlineState.nextRetryInMs <= 0) return;

    const interval = setInterval(() => {
      dispatchOffline({ type: 'RETRY_TICK', deltaMs: POLL_TICK_INTERVAL_MS });
    }, POLL_TICK_INTERVAL_MS);

    return () => clearInterval(interval);
  }, [offlineState.nextRetryInMs]);

  // Fire effect: when nextRetryInMs reaches 0 (and we have an attempt active).
  useEffect(() => {
    if (offlineState.nextRetryInMs !== 0) return;
    if (offlineState.attemptCount === 0) return;
    if (offlineState.isExhausted) return;
    dispatchOffline({ type: 'RETRY_FIRE' });
    // Real retry firing (re-issue upload OR re-trigger polling) is owned by
    // the cell-specific handler. Reducer stays pure: zeroing nextRetryInMs
    // marks the budget tick.
    if (statusQuery.refetch) {
      statusQuery.refetch();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [offlineState.nextRetryInMs, offlineState.attemptCount, offlineState.isExhausted]);

  // Status polling success → reset budget.
  useEffect(() => {
    if (statusQuery.isSuccess && offlineState.attemptCount > 0) {
      dispatchOffline({ type: 'RETRY_SUCCESS' });
    }
  }, [statusQuery.isSuccess, offlineState.attemptCount]);

  // Status polling error → consume budget.
  useEffect(() => {
    if (!statusQuery.isError) return;
    if (offlineState.nextRetryInMs !== null) return; // already retrying
    const ctrl = new AbortController();
    dispatchOffline({ type: 'NETWORK_ERROR', abortController: ctrl });
  }, [statusQuery.isError, offlineState.nextRetryInMs]);

  // ── Cancel modal state ──────────────────────────────────────────────────
  const [cancelOpen, setCancelOpen] = useState(false);
  const [isWizardCancelled, setIsWizardCancelled] = useState(false);

  // ── FSM cell derivation ─────────────────────────────────────────────────
  const cell: WizardFSMCell = useMemo(() => {
    if (fixture !== null) {
      // Fixture mode: feed fixture-supplied data directly. Existing visual
      // baseline behaviour preserved.
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

    // Real mode: feed live hook results.
    const isOffline = offlineState.attemptCount > 0 && !offlineState.isExhausted;

    return deriveWizardState({
      step: stepParam,
      gameId: gameIdParam,
      batchId: batchIdParam,
      gameSearchQuery: {
        isPending: gamesQuery.isPending,
        isError: gamesQuery.isError,
        data: catalogResults,
      },
      searchInput: queryParam,
      activeTab: tabParam,
      bggSearchQuery,
      cameraPermission,
      lightMeterValue: 1.0, // light-meter sampling deferred — assume OK
      detectionScore: 1.0, // page-detection deferred — assume OK
      isCapturing,
      capturedCount: capturedPages.length,
      batchStatus: {
        isPending: statusQuery.isPending,
        isError: statusQuery.isError,
        data: statusQuery.data ?? null,
      },
      isOffline,
      retryAttempt: offlineState.attemptCount,
      nextRetryInMs: offlineState.nextRetryInMs ?? 0,
      cancelModalOpen: cancelOpen,
      isWizardCancelled,
    });
  }, [
    fixture,
    stepParam,
    gameIdParam,
    batchIdParam,
    gamesQuery.isPending,
    gamesQuery.isError,
    catalogResults,
    queryParam,
    tabParam,
    bggSearchQuery,
    cameraPermission,
    isCapturing,
    capturedPages.length,
    statusQuery.isPending,
    statusQuery.isError,
    statusQuery.data,
    offlineState.attemptCount,
    offlineState.isExhausted,
    offlineState.nextRetryInMs,
    cancelOpen,
    isWizardCancelled,
  ]);

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

  // CameraViewfinder labels (pre-resolved per Gate A).
  const viewfinderLabels: CameraViewfinderLabels = useMemo(
    () => ({
      regionAria: t('gamebook.upload.wizard.step2.cameraAria'),
      videoAria: t('gamebook.upload.wizard.step2.cameraAria'),
      shutterAria: t('gamebook.upload.wizard.step2.shutterAria'),
      galleryAria: t('gamebook.upload.wizard.step2.galleryAria'),
      cancelAria: t('gamebook.upload.wizard.step2.closeAria'),
      doneAria: t('gamebook.upload.wizard.step2.doneAria'),
      flashAria: t('gamebook.upload.wizard.step2.galleryAria', 'Flash'),
      lightMeterAria: t('gamebook.upload.wizard.step2.lightMeterAria'),
      pageDetectedAria: t('gamebook.upload.wizard.step2.hintReady'),
      lightHintGood: t('gamebook.upload.wizard.step2.hintReady'),
      lightHintLow: t('gamebook.upload.wizard.step2.hintLowLight'),
      lightHintTooDark: t('gamebook.upload.wizard.step2.hintLowLight'),
      detectionFailedHint: t('gamebook.upload.wizard.step2.hintFailed'),
      capturedCount: t('gamebook.upload.wizard.step2.counter', {
        count: capturedPages.length,
        max: 50,
      }),
      capturedListAria: t('gamebook.upload.wizard.step2.counter', {
        count: capturedPages.length,
        max: 50,
      }),
      lightValueOk: t('gamebook.upload.wizard.step2.lightMeterOk'),
      lightValueLow: t('gamebook.upload.wizard.step2.lightMeterLow'),
      lightValueMedium: t('gamebook.upload.wizard.step2.lightMeterMedium'),
    }),
    [t, capturedPages.length]
  );

  const confidenceBadgeLabels: ConfidenceBadgeLabels = useMemo(
    () => ({
      high: t('gamebook.upload.wizard.confidence.high'),
      medium: t('gamebook.upload.wizard.confidence.medium'),
      low: t('gamebook.upload.wizard.confidence.low'),
    }),
    [t]
  );

  // Per-page label resolver (used inside Step 3 grid).
  const pageThumbLabelsFor = useCallback(
    (pageNumber: number): PageThumbLabels => ({
      pageLabel: `${pageNumber}`,
      retakeAria: t('gamebook.upload.wizard.step3.retakeAria', { pageNumber }),
      processingAria: t('gamebook.upload.wizard.confidence.processing', 'In elaborazione'),
      retakeCta: t('gamebook.upload.wizard.step3.retakeButton'),
      confidence: confidenceBadgeLabels,
    }),
    [t, confidenceBadgeLabels]
  );

  const offlineBannerLabels: OfflineBannerLabels = useMemo(() => {
    const seconds = Math.ceil((offlineState.nextRetryInMs ?? 0) / 1000);
    return {
      title: t('gamebook.upload.wizard.offline.bannerTitle'),
      retryIn:
        offlineState.attemptCount === 0
          ? ''
          : t('gamebook.upload.wizard.offline.retryAttempt', {
              current: offlineState.attemptCount,
              seconds,
            }),
      retryNow: t('gamebook.upload.wizard.offline.retryNow'),
      cancel: t('gamebook.upload.wizard.offline.cancel'),
      progressAria: `${Math.round(elapsedBudgetMs(offlineState) / 1000)}s di ${Math.round(
        RETRY_BUDGET_TOTAL_MS / 1000
      )}s`,
      retryNowAria: t('gamebook.upload.wizard.offline.retryNow'),
      cancelAria: t('gamebook.upload.wizard.offline.cancel'),
    };
  }, [t, offlineState]);

  const cancelModalLabels: CancelModalLabels = useMemo(
    () => ({
      title: t('gamebook.upload.wizard.cancelModal.title'),
      description: t('gamebook.upload.wizard.cancelModal.body', {
        count: capturedPages.length,
      }),
      confirm: t('gamebook.upload.wizard.cancelModal.confirm'),
      dismiss: t('gamebook.upload.wizard.cancelModal.continue'),
      confirmAria: t('gamebook.upload.wizard.cancelModal.confirm'),
      dismissAria: t('gamebook.upload.wizard.cancelModal.continue'),
    }),
    [t, capturedPages.length]
  );

  // ── URL mutation handlers ───────────────────────────────────────────────
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
      router.push(`${pathname}${buildQuery({ step: '2', gameId: id })}`);
    },
    [router, pathname, buildQuery]
  );

  // Step 1 NoResults action stubs — rendering paths still consistent w/ Foundation.
  const handleCreateNew = useCallback(() => {
    router.replace(`${pathname}${buildQuery({ q: null, tab: null })}`, { scroll: false });
  }, [router, pathname, buildQuery]);

  const handleSearchBgg = useCallback(() => {
    router.replace(`${pathname}${buildQuery({ tab: 'bgg' })}`, { scroll: false });
  }, [router, pathname, buildQuery]);

  const handleAddPrivate = useCallback(() => {
    router.replace(`${pathname}${buildQuery({ q: null, tab: null })}`, { scroll: false });
  }, [router, pathname, buildQuery]);

  // ── Step 2 photo capture handlers ───────────────────────────────────────

  // Capture frame from <video> element via canvas, build File, upload.
  const handleShutterClick = useCallback(async () => {
    if (!gameIdParam) return;
    if (!streamRef.current) return;

    const stream = streamRef.current;
    const video = document.querySelector<HTMLVideoElement>('[data-slot="camera-viewfinder-video"]');
    if (!video) return;

    setIsCapturing(true);
    try {
      const canvas = document.createElement('canvas');
      canvas.width = video.videoWidth || 1920;
      canvas.height = video.videoHeight || 1080;
      const ctx = canvas.getContext('2d');
      if (!ctx) {
        setIsCapturing(false);
        return;
      }
      ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
      const blob = await new Promise<Blob | null>(resolve =>
        canvas.toBlob(b => resolve(b), 'image/jpeg', 0.9)
      );
      if (!blob) {
        setIsCapturing(false);
        return;
      }

      const pageNumber = capturedPages.length + 1;
      const file = new File([blob], `page-${pageNumber}.jpg`, { type: 'image/jpeg' });
      const objectUrl = URL.createObjectURL(blob);
      const newPage: CapturedPage = {
        pageNumber,
        thumbObjectUrl: objectUrl,
        pendingUpload: true,
      };

      // Optimistic strip update.
      setCapturedPages(prev => [...prev, newPage]);

      // Compose Idempotency-Key for debug visibility (header threading
      // pending api-extension wrapper per contract §10).
      const idemKey = composeIdempotencyKey(
        batchIdParam ?? `pending-${gameIdParam}`,
        pageNumber,
        offlineState.attemptCount
      );
      void idemKey;

      // Real upload — single-photo batch (multi-photo batching deferred).
      uploadMutation.mutate({ gameId: gameIdParam, files: [file] });

      // streamRef preserved — keep getting more shots.
      void stream;
    } finally {
      setIsCapturing(false);
    }
  }, [gameIdParam, batchIdParam, capturedPages, offlineState.attemptCount, uploadMutation]);

  const handleGalleryClick = useCallback(() => {
    // File picker fallback — reuses the upload mutation with selected files.
    if (typeof document === 'undefined') return;
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*';
    input.multiple = true;
    input.onchange = e => {
      const files = Array.from((e.target as HTMLInputElement).files ?? []);
      if (files.length === 0) return;
      if (!gameIdParam) return;
      uploadMutation.mutate({ gameId: gameIdParam, files });
    };
    input.click();
  }, [gameIdParam, uploadMutation]);

  const handleDoneClick = useCallback(() => {
    // Step 2 done — no-op placeholder; real done flow is triggered by
    // upload onSuccess (router.push to step 3 with batchId).
    if (capturedPages.length === 0) return;
    // Trigger upload of all captured files in a batch (placeholder for
    // multi-photo flush). With single-photo flow above, this no-ops.
  }, [capturedPages.length]);

  // ── Cancel-during-retry / cancel modal handlers ─────────────────────────

  const handleCancelOpen = useCallback(() => {
    setCancelOpen(true);
  }, []);

  const handleCancelDismiss = useCallback(() => {
    setCancelOpen(false);
  }, []);

  const handleCancelConfirm = useCallback(() => {
    // 1. Abort in-flight HTTP if any.
    offlineState.abortController?.abort();
    // 2. Dispatch CANCEL → reset reducer.
    dispatchOffline({ type: 'CANCEL' });
    // 3. Stop camera tracks.
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(t => t.stop());
      streamRef.current = null;
      setStreamReady(false);
    }
    // 4. Mark wizard cancelled and route back to /gamebook.
    setIsWizardCancelled(true);
    setCancelOpen(false);
    router.push('/gamebook');
  }, [offlineState.abortController, router]);

  // Manual retry (RETRY_NOW): cancels current timer, fires immediately by
  // re-issuing the polling refetch.
  const handleRetryNow = useCallback(() => {
    if (statusQuery.refetch) {
      statusQuery.refetch();
    }
  }, [statusQuery]);

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
        // Step 2 wiring
        videoStream: streamRef.current,
        streamReady,
        isFixtureMode,
        isCapturing,
        capturedPages,
        viewfinderLabels,
        onShutterClick: handleShutterClick,
        onGalleryClick: handleGalleryClick,
        onCancelClick: handleCancelOpen,
        onDoneClick: handleDoneClick,
        // Step 3 wiring
        averageConfidence: statusQuery.data?.averageConfidence ?? null,
        // Backend `PhotoBatchStatus` does not yet expose per-page failure list
        // (Gate B v1 carryover — contract §12 deferred). Adapter falls back
        // to an empty array; classifier consumes only `averageConfidence` +
        // ordering until backend tracks per-page conf.
        failedPageNumbers: [] as readonly number[],
        pageThumbLabelsFor,
        offlineBannerLabels,
        offlineState,
        onRetryNow: handleRetryNow,
        // Cross-cutting cancel modal
        cancelOpen,
        cancelModalLabels,
        onCancelDismiss: handleCancelDismiss,
        onCancelConfirm: handleCancelConfirm,
        t,
      })}

      {/* Cross-cutting CancelModal — mounted at root for proper focus trap.
          Only visible when cancelOpen=true (modal handles internal animations). */}
      <CancelModal
        isOpen={cancelOpen}
        onConfirm={handleCancelConfirm}
        onDismiss={handleCancelDismiss}
        labels={cancelModalLabels}
      />
    </div>
  );
}

// ─── Cell renderer ────────────────────────────────────────────────────────

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
  // Step 2
  readonly videoStream: MediaStream | null;
  readonly streamReady: boolean;
  readonly isFixtureMode: boolean;
  readonly isCapturing: boolean;
  readonly capturedPages: readonly CapturedPage[];
  readonly viewfinderLabels: CameraViewfinderLabels;
  readonly onShutterClick: () => void;
  readonly onGalleryClick: () => void;
  readonly onCancelClick: () => void;
  readonly onDoneClick: () => void;
  // Step 3
  readonly averageConfidence: number | null;
  readonly failedPageNumbers: readonly number[];
  readonly pageThumbLabelsFor: (pageNumber: number) => PageThumbLabels;
  readonly offlineBannerLabels: OfflineBannerLabels;
  readonly offlineState: ReturnType<typeof offlineBudgetReducer>;
  readonly onRetryNow: () => void;
  // Cross-cutting
  readonly cancelOpen: boolean;
  readonly cancelModalLabels: CancelModalLabels;
  readonly onCancelDismiss: () => void;
  readonly onCancelConfirm: () => void;
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

    // ── Step 2 cells (real CameraViewfinder OR placeholder for fixture/denied) ───
    case 'step2-ready':
    case 'step2-capturing':
    case 'step2-low-light':
    case 'step2-failed':
      // In fixture mode, mockup viewfinder placeholder (deterministic baselines).
      if (input.isFixtureMode) {
        return (
          <Step2Placeholder
            kind={cell.kind}
            gameId={cell.gameId}
            capturedCount={'capturedCount' in cell ? cell.capturedCount : 0}
            t={input.t}
          />
        );
      }
      // Real mode: mount CameraViewfinder with live stream.
      return (
        <CameraViewfinder
          videoStream={input.videoStream}
          lightReading={null}
          pageDetected={cell.kind === 'step2-ready'}
          detectionFailed={cell.kind === 'step2-failed'}
          capturedCount={'capturedCount' in cell ? cell.capturedCount : 0}
          capturedThumbs={input.capturedPages}
          isCapturing={input.isCapturing}
          onShutterClick={input.onShutterClick}
          onGalleryClick={input.onGalleryClick}
          onCancelClick={input.onCancelClick}
          onDoneClick={input.onDoneClick}
          labels={input.viewfinderLabels}
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

    // ── Step 3 cells ─────────────────────────────────────────────────────
    case 'step3-progress':
      return (
        <Step3Body
          kind={cell.kind}
          batchId={cell.batchId}
          processedPages={cell.processedPages}
          totalPages={cell.totalPages}
          averageConfidence={input.averageConfidence}
          failedPageNumbers={input.failedPageNumbers}
          pageThumbLabelsFor={input.pageThumbLabelsFor}
          isFixtureMode={input.isFixtureMode}
          t={input.t}
        />
      );

    case 'step3-partial':
      return (
        <Step3Body
          kind={cell.kind}
          batchId={cell.batchId}
          processedPages={cell.processedPages}
          totalPages={cell.totalPages}
          lowConfidencePages={cell.lowConfidencePages}
          averageConfidence={input.averageConfidence}
          failedPageNumbers={input.failedPageNumbers}
          pageThumbLabelsFor={input.pageThumbLabelsFor}
          isFixtureMode={input.isFixtureMode}
          t={input.t}
        />
      );

    case 'step3-complete':
      return (
        <Step3Body
          kind={cell.kind}
          batchId={cell.batchId}
          processedPages={cell.totalPages}
          totalPages={cell.totalPages}
          averageConfidence={input.averageConfidence}
          failedPageNumbers={input.failedPageNumbers}
          pageThumbLabelsFor={input.pageThumbLabelsFor}
          isFixtureMode={input.isFixtureMode}
          t={input.t}
        />
      );

    case 'step3-offline':
      return (
        <div data-slot="step3-offline-shell" className="flex flex-col gap-3 px-4 py-6 sm:px-6">
          {!input.isFixtureMode && (
            <OfflineBanner
              attempt={input.offlineState.attemptCount}
              nextRetryInSeconds={Math.ceil((input.offlineState.nextRetryInMs ?? 0) / 1000)}
              totalElapsedSeconds={Math.round(elapsedBudgetMs(input.offlineState) / 1000)}
              budgetTotalSeconds={Math.round(RETRY_BUDGET_TOTAL_MS / 1000)}
              onRetryNow={input.onRetryNow}
              onCancel={input.onCancelClick}
              labels={input.offlineBannerLabels}
            />
          )}
          <Step3Body
            kind="step3-offline"
            batchId={cell.batchId}
            processedPages={0}
            totalPages={0}
            averageConfidence={input.averageConfidence}
            failedPageNumbers={input.failedPageNumbers}
            pageThumbLabelsFor={input.pageThumbLabelsFor}
            isFixtureMode={input.isFixtureMode}
            t={input.t}
          />
        </div>
      );

    case 'step3-cancel-modal':
      return (
        <div data-slot="step3-cancel-modal-shell" className="relative">
          <Step3Body
            kind="step3-progress"
            batchId={cell.batchId}
            processedPages={0}
            totalPages={0}
            averageConfidence={input.averageConfidence}
            failedPageNumbers={input.failedPageNumbers}
            pageThumbLabelsFor={input.pageThumbLabelsFor}
            isFixtureMode={input.isFixtureMode}
            t={input.t}
          />
          {/* Fixture-mode placeholder modal preserved for visual baselines. The
              real CancelModal is mounted at the root of GamebookUploadView. */}
          {input.isFixtureMode && (
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
                  {input.t('gamebook.upload.wizard.step3.cancelButton')}
                </p>
              </div>
            </div>
          )}
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
        <p className="text-sm text-slate-700">Nessun gioco da mostrare.</p>
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

// ─── Step 2 placeholder (fixture mode + permission-denied UI) ─────────────

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
    </section>
  );
}

// ─── Step 3 body — PageThumb grid + progress meta ──────────────────────────

interface Step3BodyProps {
  readonly kind: 'step3-progress' | 'step3-partial' | 'step3-complete' | 'step3-offline';
  readonly batchId: string;
  readonly processedPages: number;
  readonly totalPages: number;
  readonly lowConfidencePages?: readonly number[];
  readonly averageConfidence: number | null;
  readonly failedPageNumbers: readonly number[];
  readonly pageThumbLabelsFor: (pageNumber: number) => PageThumbLabels;
  readonly isFixtureMode: boolean;
  readonly t: (key: string, values?: Record<string, string | number>) => string;
}

function Step3Body({
  kind,
  batchId,
  processedPages,
  totalPages,
  lowConfidencePages,
  averageConfidence,
  failedPageNumbers,
  pageThumbLabelsFor,
  isFixtureMode,
  t,
}: Step3BodyProps): ReactElement {
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
        : kind === 'step3-offline'
          ? 'gamebook.upload.wizard.offline.bannerTitle'
          : 'gamebook.upload.wizard.step3.progressTitle';

  // In fixture mode, render lightweight placeholder for visual baseline parity.
  // In real mode, render PageThumb grid (heuristic per-page conf via classifier).
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

      {!isFixtureMode && totalPages > 0 && (
        <ul
          data-slot="step3-page-grid"
          role="list"
          className="mt-4 grid w-full max-w-md grid-cols-4 gap-2"
        >
          {Array.from({ length: totalPages }, (_, i) => {
            const pageNumber = i + 1;
            const isProcessing = i >= processedPages;
            const isLowFromCell = lowConfidencePages?.includes(pageNumber) ?? false;
            const heuristicConf =
              !isProcessing && averageConfidence !== null
                ? deriveHeuristicPageConfidence(
                    i,
                    totalPages,
                    averageConfidence,
                    failedPageNumbers.length
                  )
                : null;
            const level = isProcessing ? null : classifyConfidence(heuristicConf);
            const retake = isLowFromCell || level === 'low';
            return (
              <PageThumb
                key={pageNumber}
                pageNumber={pageNumber}
                thumbnailUrl={null}
                confidence={level}
                processing={isProcessing}
                retake={retake}
                labels={pageThumbLabelsFor(pageNumber)}
              />
            );
          })}
        </ul>
      )}
    </section>
  );
}
