'use client';

import { useEffect, type ReactElement } from 'react';

import { useTranslation } from '@/hooks/useTranslation';
import { useGamebookGlossary } from '@/lib/gamebook/hooks/useGamebookGlossary';

export interface GlossaryLookupModalProps {
  /** Campaign whose glossary is looked up. */
  readonly campaignId: string;
  /** Whether the modal is visible. */
  readonly isOpen: boolean;
  /** Called when the user dismisses the modal (Escape, scrim, close button). */
  readonly onClose: () => void;
}

/**
 * #2750 C11 — read-only glossary look-up.
 *
 * Lists the campaign's EN → IT glossary terms so a player can look them up in
 * context (e.g. from the encounter cheatsheet's "Glossario" affordance). This is
 * deliberately distinct from {@link GlossaryEditorModal}, which edits a single
 * entry: the look-up is read-only and shows the whole glossary at once.
 */
export function GlossaryLookupModal({
  campaignId,
  isOpen,
  onClose,
}: GlossaryLookupModalProps): ReactElement | null {
  const { t } = useTranslation();
  const { data: entries, isLoading, isError } = useGamebookGlossary(campaignId);

  useEffect(() => {
    if (!isOpen) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const isEmpty = !isLoading && !isError && (entries?.length ?? 0) === 0;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={t('gamebook.glossaryLookup.title')}
      className="fixed inset-0 z-50 flex items-end justify-center sm:items-center"
    >
      <div
        data-testid="glossary-lookup-scrim"
        role="presentation"
        onClick={onClose}
        className="absolute inset-0 bg-[rgba(0,0,0,0.45)]"
      />
      <div className="relative z-10 flex max-h-[80vh] w-full max-w-md flex-col overflow-hidden rounded-t-2xl border border-border bg-card shadow-xl sm:rounded-2xl">
        <header className="flex items-center justify-between gap-3 border-b border-border px-4 py-3">
          <div className="flex flex-col">
            <h2 className="text-base font-semibold text-foreground">
              {t('gamebook.glossaryLookup.title')}
            </h2>
            <span className="text-xs text-muted-foreground">
              {t('gamebook.glossaryLookup.subtitle')}
            </span>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('gamebook.glossaryLookup.close')}
            className="rounded-md p-1.5 text-muted-foreground hover:bg-muted"
          >
            <span aria-hidden="true">✕</span>
          </button>
        </header>

        <div className="overflow-y-auto px-4 py-3">
          {isLoading && (
            <p
              className="text-sm text-muted-foreground"
              role="status"
              data-testid="glossary-lookup-loading"
            >
              {t('gamebook.glossaryLookup.loading')}
            </p>
          )}
          {isError && (
            <p
              className="text-sm text-destructive"
              role="alert"
              data-testid="glossary-lookup-error"
            >
              {t('gamebook.glossaryLookup.error')}
            </p>
          )}
          {isEmpty && (
            <p className="text-sm text-muted-foreground" data-testid="glossary-lookup-empty">
              {t('gamebook.glossaryLookup.empty')}
            </p>
          )}
          {!isLoading && !isError && entries && entries.length > 0 && (
            <ul className="divide-y divide-border" data-testid="glossary-lookup-list">
              {entries.map(entry => (
                <li key={entry.id} className="flex items-baseline gap-2 py-2">
                  <span className="font-medium text-foreground">{entry.termEn}</span>
                  <span aria-hidden="true" className="text-muted-foreground">
                    →
                  </span>
                  <span className="text-foreground">{entry.termIt}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
