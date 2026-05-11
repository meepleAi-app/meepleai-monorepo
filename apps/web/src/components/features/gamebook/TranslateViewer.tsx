'use client';

import { useRef, useState, type ReactElement } from 'react';

import { SegmentPicker } from '@/components/features/gamebook/SegmentPicker';
import { TranslationPane } from '@/components/features/gamebook/TranslationPane';
import type { GamebookPhotoArtifact, GamebookSegment } from '@/lib/api/gamebook-photos';
import { usePhotoUpload } from '@/lib/gamebook/hooks/usePhotoUpload';
import { useSegmentPhoto } from '@/lib/gamebook/hooks/useSegmentPhoto';
import { useTranslateSegmentSSE } from '@/lib/gamebook/hooks/useTranslateSegmentSSE';


export interface TranslateViewerProps {
  campaignId: string;
}

type Phase = 'idle' | 'uploading' | 'segmenting' | 'segments_ready' | 'translating' | 'translated';

export function TranslateViewer({ campaignId }: TranslateViewerProps): ReactElement {
  const [phase, setPhase] = useState<Phase>('idle');
  const [pageType, setPageType] = useState<'Storybook' | 'Encounter'>('Storybook');
  const [artifact, setArtifact] = useState<GamebookPhotoArtifact | null>(null);
  const [activeSegment, setActiveSegment] = useState<GamebookSegment | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const upload = usePhotoUpload(campaignId);
  const segment = useSegmentPhoto(campaignId);
  const sse = useTranslateSegmentSSE();

  const handleFile = async (file: File) => {
    setPhase('uploading');
    setArtifact(null);
    setActiveSegment(null);
    try {
      const uploaded = await upload.mutateAsync({ file, pageType });
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
    if (!artifact) return;
    const seg = artifact.segments.find(s => s.paragraphNumber === paragraphNumber);
    if (!seg) return;
    setActiveSegment(seg);
    setPhase('translating');
    sse.start(campaignId, artifact.id, paragraphNumber);
  };

  // Auto-flip to translated when SSE completes
  if (phase === 'translating' && sse.isComplete) {
    setPhase('translated');
  }

  const errorMessage = upload.error?.message ?? segment.error?.message ?? sse.error;

  return (
    <div className="grid gap-4 px-4 py-6 sm:px-6">
      <header className="flex flex-wrap items-center gap-3">
        <h1 className="text-xl font-semibold text-[var(--c-game)]">Traduci pagina libro game</h1>
        <select
          value={pageType}
          onChange={e => setPageType(e.target.value as 'Storybook' | 'Encounter')}
          disabled={phase !== 'idle'}
          className="rounded-md border border-input bg-background px-2 py-1 text-sm"
          data-testid="page-type-select"
        >
          <option value="Storybook">Storybook</option>
          <option value="Encounter">Encounter Book</option>
        </select>
      </header>

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
          disabled={phase === 'uploading' || phase === 'segmenting' || phase === 'translating'}
          className="rounded-md bg-[var(--c-agent)] px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
          data-testid="open-camera-button"
        >
          {phase === 'idle' || phase === 'translated' ? '📸 Scatta o scegli foto' : '⏳ In corso…'}
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
