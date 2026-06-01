'use client';

import { useCallback, useEffect, useMemo, useRef, useState, type ReactElement } from 'react';

import { AbortButton } from '@/components/features/gamebook/AbortButton';
import { BookPicker } from '@/components/features/gamebook/BookPicker';
import { LangBadge } from '@/components/features/gamebook/LangBadge';
import { LangOverrideModal } from '@/components/features/gamebook/LangOverrideModal';
import { LoadingSkeleton } from '@/components/features/gamebook/LoadingSkeleton';
import { ReaderModeToggle } from '@/components/features/gamebook/ReaderModeToggle';
import { SegmentPicker } from '@/components/features/gamebook/SegmentPicker';
import {
  deriveUiStep,
  isAbortableStep,
  LABELS,
} from '@/components/features/gamebook/TranslateViewer.steps';
import { TranslationPane } from '@/components/features/gamebook/TranslationPane';
import { useGameBooks } from '@/hooks/useGameBooks';
import { trackEvent } from '@/lib/analytics/track-event';
import { GameBookRole, hasRole, type GameRef } from '@/lib/api/gamebook';
import type { GamebookPhotoArtifact, GamebookSegment } from '@/lib/api/gamebook-photos';
import { usePhotoUpload } from '@/lib/gamebook/hooks/usePhotoUpload';
import { useReaderMode } from '@/lib/gamebook/hooks/useReaderMode';
import { useSegmentPhoto } from '@/lib/gamebook/hooks/useSegmentPhoto';
import { useTranslateSegmentSSE } from '@/lib/gamebook/hooks/useTranslateSegmentSSE';
import type { SourceLangCode } from '@/lib/gamebook/lang-codes';
import { getLangTier, type LangTier } from '@/lib/gamebook/lang-tier';

export interface TranslateViewerProps {
  campaignId: string;
  /**
   * GameRef of the campaign being translated. Required so the viewer can
   * fetch the list of GameBooks (multi-book generalization, Phase E).
   *
   * For routes that don't yet thread a typed `GameRef`, callers can pass
   * `{ id: gameId, kind: 0 /* Shared *\/ }` as a sensible default — the BE
   * endpoint validates kind discrimination.
   */
  gameRef: GameRef;
}

export type Phase =
  | 'idle'
  | 'uploading'
  | 'segmenting'
  | 'segments_ready'
  | 'translating'
  | 'translated';

type ModalMode = 'auto-low' | 'auto-medium-tap' | 'manual';
type ModalState = { open: boolean; mode: ModalMode; preselect?: SourceLangCode };

/**
 * Compute effective tier for badge rendering.
 * DEC-FE-10: when detected lang is null with high confidence (out-of-allowlist),
 * fall back to 'none' (un-actionable detection).
 */
function effectiveTier(
  lang: SourceLangCode | null | undefined,
  conf: number | null | undefined
): LangTier {
  const rawTier = getLangTier(conf);
  if (rawTier === 'high' && (lang === null || lang === undefined)) return 'none';
  return rawTier;
}

export function TranslateViewer({ campaignId, gameRef }: TranslateViewerProps): ReactElement {
  const [phase, setPhase] = useState<Phase>('idle');
  const [artifact, setArtifact] = useState<GamebookPhotoArtifact | null>(null);
  const [activeSegment, setActiveSegment] = useState<GamebookSegment | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const { isReaderMode, toggle: toggleReaderMode } = useReaderMode();

  // C4/C5 (multi-book generalization 2026-05-19): book selection replaces the
  // legacy Storybook/Encounter `pageType` dropdown.
  const { data: books } = useGameBooks(gameRef);
  const narrativeBooks = useMemo(
    () =>
      (books ?? [])
        .filter(b => hasRole(b.roles, GameBookRole.Narrative))
        .map(b => ({ id: b.id, displayName: b.displayName, roles: b.roles })),
    [books]
  );

  // Auto-select when only 1 narrative book exists; user picks otherwise.
  const [selectedBookId, setSelectedBookId] = useState<string | undefined>(undefined);
  const effectiveBookId =
    selectedBookId ?? (narrativeBooks.length === 1 ? narrativeBooks[0].id : undefined);

  const upload = usePhotoUpload(campaignId);
  const segment = useSegmentPhoto(campaignId);
  const sse = useTranslateSegmentSSE();

  // T5 — DEC-4: Hard timeout state. Merged into errorMessage below.
  const [timeoutError, setTimeoutError] = useState<string | null>(null);

  // DEC-1: Derive user-facing UI step from internal FSM phase + SSE signals.
  const uiStep = deriveUiStep(phase, sse);

  // #1559 — Lang detection state (DEC-FE-5/6/7/8/10/11)
  const [confirmedLang, setConfirmedLang] = useState<SourceLangCode | null>(null);
  const [autoLowFiredForArtifact, setAutoLowFiredForArtifact] = useState<string | null>(null);
  const [analyticsDetectedFiredForArtifact, setAnalyticsDetectedFiredForArtifact] = useState<
    string | null
  >(null);
  const [modalState, setModalState] = useState<ModalState>({ open: false, mode: 'manual' });

  // Reset lang state on artifact change (DEC-FE-11)
  useEffect(() => {
    setConfirmedLang(null);
    setAutoLowFiredForArtifact(null);
    setAnalyticsDetectedFiredForArtifact(null);
  }, [artifact?.id]);

  const tier = effectiveTier(sse.detectedSourceLang, sse.langDetectionConfidence);
  const effectiveLang: SourceLangCode | null = confirmedLang ?? sse.detectedSourceLang ?? null;
  const isOverride = confirmedLang !== null && confirmedLang !== sse.detectedSourceLang;
  // Tier shown in UI: after confirmedLang, badge transitions to 'high' info-tier (override case)
  const renderedTier: LangTier = confirmedLang ? 'high' : tier;
  const segmentBlockedByLang = (tier === 'medium' || tier === 'low') && !confirmedLang;

  // Emit analytics: lang_detected fires once per artifact when sse.isComplete with lang fields present
  useEffect(() => {
    if (!sse.isComplete || !artifact?.id) return;
    if (analyticsDetectedFiredForArtifact === artifact.id) return;
    // Only emit when BE actually sent the fields (DEC-FE-12 vs legacy)
    if (sse.detectedSourceLang === undefined && sse.langDetectionConfidence === undefined) return;
    trackEvent('translate.lang_detected', {
      photoId: artifact.id,
      lang: sse.detectedSourceLang ?? null,
      confidence: sse.langDetectionConfidence ?? null,
      tier,
    });
    setAnalyticsDetectedFiredForArtifact(artifact.id);
  }, [
    sse.isComplete,
    sse.detectedSourceLang,
    sse.langDetectionConfidence,
    artifact?.id,
    tier,
    analyticsDetectedFiredForArtifact,
  ]);

  // Auto-open modal for tier=low first time per artifact (DEC-FE-5)
  useEffect(() => {
    if (!sse.isComplete || !artifact?.id) return;
    if (autoLowFiredForArtifact === artifact.id) return;
    if (tier !== 'low') return;
    if (confirmedLang) return;
    setModalState({ open: true, mode: 'auto-low', preselect: undefined });
    setAutoLowFiredForArtifact(artifact.id);
    trackEvent('translate.lang_modal_opened', {
      photoId: artifact.id,
      tier,
      mode: 'auto-low',
    });
  }, [sse.isComplete, tier, artifact?.id, autoLowFiredForArtifact, confirmedLang]);

  const handleFile = async (file: File) => {
    if (!effectiveBookId) {
      // Defensive: button should be disabled when no book selectable.
      return;
    }
    setTimeoutError(null); // Clear timeout error on new operation start.
    setPhase('uploading');
    setArtifact(null);
    setActiveSegment(null);
    try {
      const uploaded = await upload.mutateAsync({ file, gameBookId: effectiveBookId });
      setPhase('segmenting');
      const segmented = await segment.mutateAsync({ photoId: uploaded.id });
      setArtifact(segmented);
      setPhase('segments_ready');
    } catch {
      // mutation errors surface in their own state — viewer shows below
      setPhase('idle');
    }
  };

  // T4 — DEC-3: Abort handler. Rolls back FSM per phase:
  //   uploading   → idle          (photo discarded, camera available)
  //   segmenting  → idle          (photo + OCR discarded, camera available)
  //   translating → segments_ready (artifact preserved, user can pick another segment)
  const handleAbort = useCallback(() => {
    sse.stop();
    setTimeoutError(null);
    setPhase(prev => (prev === 'translating' ? 'segments_ready' : 'idle'));
  }, [sse]);

  // T5 — DEC-4: Hard timeout 20s. Soft target 17s (Aaron CORE §1b lines 122-123 — no UI feedback).
  const HARD_TIMEOUT_MS = 20_000;

  useEffect(() => {
    if (phase !== 'uploading' && phase !== 'segmenting' && phase !== 'translating') return;
    const timerId = window.setTimeout(() => {
      // M2 review fix: skip rollback if SSE already completed in the same tick (race guard)
      if (sse.isComplete) return;
      sse.stop();
      setTimeoutError(LABELS.timeoutError);
      setPhase(prev => (prev === 'translating' ? 'segments_ready' : 'idle'));
    }, HARD_TIMEOUT_MS);
    return () => window.clearTimeout(timerId);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- sse.stop is stable (useCallback); deliberate phase-only arm
  }, [phase]);

  const handlePickSegment = (paragraphNumber: number) => {
    if (!artifact || !effectiveBookId) return;
    if (segmentBlockedByLang) return; // defensive — picker should be disabled
    const seg = artifact.segments.find(s => s.paragraphNumber === paragraphNumber);
    if (!seg) return;
    setActiveSegment(seg);
    setPhase('translating');
    sse.start(
      campaignId,
      artifact.id,
      paragraphNumber,
      effectiveBookId,
      confirmedLang ?? undefined
    );
  };

  // Auto-flip to translated when SSE completes
  if (phase === 'translating' && sse.isComplete) {
    setPhase('translated');
  }

  const handleBadgeTap = () => {
    if (!artifact?.id) return;
    const mode: ModalMode = tier === 'medium' ? 'auto-medium-tap' : 'manual';
    setModalState({
      open: true,
      mode,
      preselect: confirmedLang ?? sse.detectedSourceLang ?? undefined,
    });
    trackEvent('translate.lang_modal_opened', {
      photoId: artifact.id,
      tier,
      mode,
    });
  };

  const handleModalOpenChange = (open: boolean) => {
    if (!open && artifact?.id) {
      trackEvent('translate.lang_modal_dismissed', {
        photoId: artifact.id,
        tier,
      });
    }
    setModalState(prev => ({ ...prev, open }));
  };

  const handleModalConfirm = (chosen: SourceLangCode) => {
    if (!artifact?.id) return;
    const previous = confirmedLang ?? sse.detectedSourceLang ?? null;
    setConfirmedLang(chosen);
    setModalState(prev => ({ ...prev, open: false }));
    if (chosen !== previous) {
      trackEvent('translate.lang_overridden', {
        photoId: artifact.id,
        fromLang: previous,
        toLang: chosen,
      });
      // Re-translate if a segment is already active (DEC-FE-7)
      if (activeSegment && effectiveBookId) {
        setPhase('translating');
        sse.start(campaignId, artifact.id, activeSegment.paragraphNumber, effectiveBookId, chosen);
      }
    }
  };

  // Modal dismissable: true everywhere per locked Opt B (dismissable + SegmentPicker block)
  const modalDismissable = true;

  const errorMessage =
    upload.error?.message ?? segment.error?.message ?? sse.error ?? timeoutError ?? undefined;

  const isBusy = phase === 'uploading' || phase === 'segmenting' || phase === 'translating';
  const cameraDisabled = isBusy || !effectiveBookId;
  const segmentPickerDisabled = phase === 'translating' || segmentBlockedByLang;

  // Show badge after segments_ready (post-OCR)
  const showBadge =
    (phase === 'segments_ready' || phase === 'translating' || phase === 'translated') &&
    artifact !== null &&
    renderedTier !== 'none';

  return (
    <div className="grid gap-4 px-4 py-6 sm:px-6" data-reader-mode={String(isReaderMode)}>
      <header className="flex flex-wrap items-center gap-3">
        <div className="flex flex-1 items-center justify-between gap-2">
          <h1 className="text-xl font-semibold text-[var(--c-game)]">Traduci pagina libro game</h1>
          <ReaderModeToggle isReaderMode={isReaderMode} onToggle={toggleReaderMode} />
        </div>
        {showBadge && (
          <LangBadge
            lang={effectiveLang}
            confidence={sse.langDetectionConfidence ?? null}
            tier={renderedTier}
            onTap={handleBadgeTap}
            isOverride={isOverride}
          />
        )}
      </header>

      {narrativeBooks.length > 1 && (
        <section
          className="rounded-lg border border-[var(--c-game)]/20 bg-background p-4 grid gap-2"
          data-testid="translate-viewer-book-picker-section"
        >
          <p className="text-sm text-muted-foreground">Da quale libro proviene questa pagina?</p>
          <BookPicker
            books={narrativeBooks}
            value={effectiveBookId ?? ''}
            onChange={setSelectedBookId}
          />
        </section>
      )}

      {books !== undefined && narrativeBooks.length === 0 && (
        <p
          className="text-sm text-destructive"
          role="alert"
          data-testid="translate-viewer-no-narrative-books"
        >
          Questo gioco non ha libri narrativi disponibili per photo-translate.
        </p>
      )}

      <section className="rounded-lg border border-[var(--c-game)]/20 bg-background p-4 grid gap-3">
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          capture="environment"
          aria-label="Seleziona foto da tradurre"
          onChange={e => {
            const f = e.target.files?.[0];
            if (f) void handleFile(f);
          }}
          className="hidden"
          data-testid="photo-input"
        />
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={cameraDisabled}
          className="rounded-md bg-[var(--c-agent)] px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
          data-testid="open-camera-button"
        >
          {phase === 'idle' || phase === 'translated' ? 'Scatta o scegli foto' : 'In corso…'}
        </button>
        {uiStep && <LoadingSkeleton uiStep={uiStep} />}
        {isAbortableStep(uiStep) && <AbortButton onClick={handleAbort} />}
        {errorMessage && (
          <p className="text-sm text-destructive" role="alert" data-testid="translate-viewer-error">
            {errorMessage}
          </p>
        )}
        {segmentBlockedByLang && (
          <p
            className="text-sm text-muted-foreground"
            role="status"
            data-testid="translate-viewer-lang-block-hint"
          >
            Conferma la sorgente per tradurre.
          </p>
        )}
      </section>

      {artifact &&
        (phase === 'segments_ready' || phase === 'translating' || phase === 'translated') && (
          <SegmentPicker
            segments={artifact.segments}
            onPick={handlePickSegment}
            disabled={segmentPickerDisabled}
          />
        )}

      {activeSegment && (phase === 'translating' || phase === 'translated') && (
        <TranslationPane
          partialText={sse.partialText}
          isComplete={sse.isComplete}
          appliedTerms={sse.appliedTerms}
          sourceTextEn={activeSegment.sourceText}
          error={sse.error}
        />
      )}

      <LangOverrideModal
        open={modalState.open}
        onOpenChange={handleModalOpenChange}
        preselect={modalState.preselect}
        dismissable={modalDismissable}
        onConfirm={handleModalConfirm}
      />
    </div>
  );
}
