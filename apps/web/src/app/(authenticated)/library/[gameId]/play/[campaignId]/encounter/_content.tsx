'use client';

import { useMemo, type ReactElement } from 'react';

import { useRouter } from 'next/navigation';

import {
  EncounterCheatsheetView,
  type EncounterCheatsheetLabels,
  type EncounterStoryContext,
} from '@/components/features/gamebook';
import { useTranslation } from '@/hooks/useTranslation';
import { deriveEncounterStatus, mapEncounterError } from '@/lib/gamebook/encounter-fsm';
import { useEncounterParse } from '@/lib/gamebook/hooks/useEncounterParse';

/**
 * Orchestrator for the `/library/[gameId]/play/[campaignId]/encounter` route
 * (Issue #1484). Resolves i18n labels, drives the parse mutation, and maps its
 * state to the pure {@link EncounterCheatsheetView} FSM. State D (resolution)
 * is out of scope: `onResolve` navigates back to the play session.
 */
export function Content({
  gameId,
  campaignId,
  photoId,
  paragraphNumber,
  gameBookId,
  fromLabel,
  excerpt,
}: {
  gameId: string;
  campaignId: string;
  photoId: string;
  paragraphNumber: number;
  gameBookId: string;
  fromLabel: string | null;
  excerpt: string | null;
}): ReactElement {
  const { t } = useTranslation();
  const router = useRouter();
  const parse = useEncounterParse(campaignId, photoId);

  const paragraphMarker = paragraphNumber > 0 ? `§${paragraphNumber}` : '';

  const labels: EncounterCheatsheetLabels = useMemo(
    () => ({
      entryStoryMeta: t('gamebook.encounter.entryStoryMeta'),
      entryRefTitle: t('gamebook.encounter.entryRefTitle', { paragraph: paragraphMarker }),
      entryRefHint: t('gamebook.encounter.entryRefHint'),
      entryCta: t('gamebook.encounter.entryCta', { paragraph: paragraphMarker }),
      entryCtaHint: t('gamebook.encounter.entryCtaHint'),
      loadingTitle: t('gamebook.encounter.loadingTitle'),
      loadingHint: t('gamebook.encounter.loadingHint'),
      cancel: t('gamebook.encounter.cancel'),
      optionsTitle: t('gamebook.encounter.optionsTitle'),
      conditionsWin: t('gamebook.encounter.conditionsWin'),
      conditionsLoss: t('gamebook.encounter.conditionsLoss'),
      parseConfidence: t('gamebook.encounter.parseConfidence'),
      lowConfidenceHint: t('gamebook.encounter.lowConfidenceHint'),
      ephemeralNote: t('gamebook.encounter.ephemeralNote'),
      retake: t('gamebook.encounter.retake'),
      glossary: t('gamebook.encounter.glossary'),
      resolve: t('gamebook.encounter.resolve'),
      errorParseFailed: t('gamebook.encounter.errorParseFailed'),
      errorNotFound: t('gamebook.encounter.errorNotFound'),
      errorGeneric: t('gamebook.encounter.errorGeneric'),
      retry: t('gamebook.encounter.retry'),
      confidence: {
        high: t('gamebook.encounter.confidence.high'),
        medium: t('gamebook.encounter.confidence.medium'),
        low: t('gamebook.encounter.confidence.low'),
      },
    }),
    [t, paragraphMarker]
  );

  // `from` is expected as a bare integer ("147"); strip a leading § defensively
  // so a caller passing "§147" does not render "§§147".
  const storyContext: EncounterStoryContext | null = fromLabel
    ? { paragraphLabel: `§${fromLabel.replace(/^§/, '')}`, excerpt: excerpt ?? '' }
    : null;

  const status = deriveEncounterStatus(parse.status);
  const errorKind = parse.error ? mapEncounterError(parse.error) : undefined;

  return (
    <main className="min-h-[calc(100vh-var(--app-topbar-height,64px))]">
      <EncounterCheatsheetView
        status={status}
        cheatsheet={parse.data ?? null}
        errorKind={errorKind}
        storyContext={storyContext}
        labels={labels}
        onParse={() => parse.mutate({ paragraphNumber, gameBookId })}
        onResolve={() => router.push(`/library/${gameId}/play/${campaignId}`)}
        onCancel={() => parse.reset()}
      />
    </main>
  );
}
