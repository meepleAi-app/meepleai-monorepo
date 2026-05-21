'use client';

import { useMemo, useRef, useState, type ReactElement } from 'react';

import { BookPicker } from '@/components/features/gamebook/BookPicker';
import { SegmentPicker } from '@/components/features/gamebook/SegmentPicker';
import { TranslationPane } from '@/components/features/gamebook/TranslationPane';
import { useGameBooks } from '@/hooks/useGameBooks';
import { GameBookRole, hasRole, type GameRef } from '@/lib/api/gamebook';
import type { GamebookPhotoArtifact, GamebookSegment } from '@/lib/api/gamebook-photos';
import { usePhotoUpload } from '@/lib/gamebook/hooks/usePhotoUpload';
import { useSegmentPhoto } from '@/lib/gamebook/hooks/useSegmentPhoto';
import { useTranslateSegmentSSE } from '@/lib/gamebook/hooks/useTranslateSegmentSSE';

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

type Phase = 'idle' | 'uploading' | 'segmenting' | 'segments_ready' | 'translating' | 'translated';

export function TranslateViewer({ campaignId, gameRef }: TranslateViewerProps): ReactElement {
  const [phase, setPhase] = useState<Phase>('idle');
  const [artifact, setArtifact] = useState<GamebookPhotoArtifact | null>(null);
  const [activeSegment, setActiveSegment] = useState<GamebookSegment | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

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

  const handleFile = async (file: File) => {
    if (!effectiveBookId) {
      // Defensive: button should be disabled when no book selectable.
      return;
    }
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

  const handlePickSegment = (paragraphNumber: number) => {
    if (!artifact || !effectiveBookId) return;
    const seg = artifact.segments.find(s => s.paragraphNumber === paragraphNumber);
    if (!seg) return;
    setActiveSegment(seg);
    setPhase('translating');
    sse.start(campaignId, artifact.id, paragraphNumber, effectiveBookId);
  };

  // Auto-flip to translated when SSE completes
  if (phase === 'translating' && sse.isComplete) {
    setPhase('translated');
  }

  const errorMessage = upload.error?.message ?? segment.error?.message ?? sse.error;

  const isBusy = phase === 'uploading' || phase === 'segmenting' || phase === 'translating';
  const cameraDisabled = isBusy || !effectiveBookId;

  return (
    <div className="grid gap-4 px-4 py-6 sm:px-6">
      <header className="flex flex-wrap items-center gap-3">
        <h1 className="text-xl font-semibold text-[var(--c-game)]">Traduci pagina libro game</h1>
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
        {phase === 'uploading' && <p className="text-sm text-muted-foreground">Upload in corso…</p>}
        {phase === 'segmenting' && (
          <p className="text-sm text-muted-foreground">OCR in corso (estrazione paragrafi)…</p>
        )}
        {errorMessage && (
          <p className="text-sm text-destructive" role="alert" data-testid="translate-viewer-error">
            {errorMessage}
          </p>
        )}
      </section>

      {artifact &&
        (phase === 'segments_ready' || phase === 'translating' || phase === 'translated') && (
          <SegmentPicker
            segments={artifact.segments}
            onPick={handlePickSegment}
            disabled={phase === 'translating'}
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
    </div>
  );
}
