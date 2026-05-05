'use client';

import { useEffect, useMemo, useRef, useState } from 'react';

import { useTranslation } from '@/hooks/useTranslation';

interface TranslationViewerProps {
  originalText?: string;
  translatedText?: string;
  pageNumber: number;
  isLoading: boolean;
  onRetry?: () => void;
  defaultDistanceReading?: boolean;
}

/**
 * Mobile-first paragraph viewer for Libro Game AI Assistant.
 *
 * Features:
 * - Displays translated text by default; toggle to original (AC-3.5b-1)
 * - Loading skeleton while fetching (AC-3.5b-2)
 * - ARIA: aria-pressed on toggle buttons, aria-live polite on content (AC-3.5b-3)
 * - Empty state + retry CTA when no text available (AC-3.5b-6)
 * - Default font ≥ 18px (text-lg) for vision accessibility §4.2 (AC-3.5b-5)
 * - "Distance reading" toggle bumps to 24px (text-2xl) (AC-3.5b-5)
 * - Auto-scrolls to top of article on pageNumber change (AC-3.5b-7)
 * - Toggle resets to translated view on page change (AC-3.5b-7 UX)
 */
export function TranslationViewer({
  originalText,
  translatedText,
  pageNumber,
  isLoading,
  onRetry,
  defaultDistanceReading = false,
}: TranslationViewerProps) {
  const { t } = useTranslation();
  const [showOriginal, setShowOriginal] = useState(false);
  const [distanceReading, setDistanceReading] = useState(defaultDistanceReading);
  const articleRef = useRef<HTMLElement>(null);

  // AC-3.5b-7: Scroll to top of article when page changes
  useEffect(() => {
    articleRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [pageNumber]);

  // AC-3.5b-7: Reset original/translated toggle on page change (clean UX)
  useEffect(() => {
    setShowOriginal(false);
  }, [pageNumber]);

  const displayText = useMemo(
    () => (showOriginal ? originalText : translatedText),
    [showOriginal, originalText, translatedText]
  );

  // AC-3.5b-2: Loading skeleton
  if (isLoading) {
    return (
      <div role="status" aria-label={t('gamebook.play.loading', 'Caricamento...')}>
        <div className="mb-2 h-4 w-3/4 animate-pulse rounded bg-gray-200" />
        <div className="mb-2 h-4 w-1/2 animate-pulse rounded bg-gray-200" />
        <div className="h-4 w-2/3 animate-pulse rounded bg-gray-200" />
      </div>
    );
  }

  // AC-3.5b-6: Empty state when both texts are absent
  if (!translatedText && !originalText) {
    return (
      <div className="py-8 text-center" role="alert">
        <p className="mb-4 text-base text-muted-foreground">
          {t('gamebook.play.translationUnavailable', 'Traduzione non disponibile')}
        </p>
        {onRetry && (
          <button
            type="button"
            className="rounded-md bg-primary px-4 py-2 text-base text-primary-foreground"
            onClick={onRetry}
          >
            {t('gamebook.play.retry', 'Riprova')}
          </button>
        )}
      </div>
    );
  }

  // AC-3.5b-5: Font size — text-lg (18px) default; text-2xl (24px) in distance mode
  const fontSizeClass = distanceReading ? 'text-2xl leading-relaxed' : 'text-lg leading-relaxed';

  return (
    <article
      ref={articleRef}
      aria-label={t('gamebook.play.pageContent', { num: pageNumber })}
      data-slot="translation-viewer"
    >
      <header className="mb-3 flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          {t('gamebook.play.pageNumber', { num: pageNumber })}
        </p>
        {/* AC-3.5b-5: Distance reading toggle (aria-pressed reflects current state) */}
        <button
          type="button"
          className="text-xs text-primary underline"
          onClick={() => setDistanceReading(v => !v)}
          aria-pressed={distanceReading}
          aria-label={t('gamebook.play.toggleDistanceReading', 'Modalità lettura distante')}
        >
          {distanceReading
            ? t('gamebook.play.normalReading', '🔍 Lettura normale')
            : t('gamebook.play.distanceReading', '🔭 Lettura distante')}
        </button>
      </header>

      {/* AC-3.5b-1 + AC-3.5b-3: Text area with aria-live for dynamic updates */}
      <p className={fontSizeClass} aria-live="polite" data-distance-mode={distanceReading}>
        {displayText}
      </p>

      {/* AC-3.5b-3: Toggle only shown when both texts are available */}
      {originalText && translatedText && (
        <button
          type="button"
          className="mt-4 text-sm text-primary underline"
          onClick={() => setShowOriginal(v => !v)}
          aria-pressed={showOriginal}
        >
          {showOriginal
            ? t('gamebook.play.showTranslation', '🌐 Mostra traduzione')
            : t('gamebook.play.showOriginal', '📖 Mostra originale')}
        </button>
      )}
    </article>
  );
}
