'use client';

import { useMemo, type ReactElement } from 'react';

export interface TranslationPaneProps {
  partialText: string;
  isComplete: boolean;
  appliedTerms: ReadonlyArray<string>;
  sourceTextEn: string;
  error?: string;
}

export function TranslationPane({
  partialText,
  isComplete,
  appliedTerms,
  sourceTextEn,
  error,
}: TranslationPaneProps): ReactElement {
  const highlightTerms = useMemo(() => Array.from(new Set(appliedTerms)), [appliedTerms]);

  if (error) {
    return (
      <div
        className="rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive"
        role="alert"
      >
        Errore traduzione: {error}
      </div>
    );
  }

  return (
    <div className="grid gap-4">
      <section
        className="rounded-md border border-[var(--c-game)]/20 bg-background p-3"
        data-testid="translation-pane-it"
      >
        <div className="flex items-center justify-between mb-1">
          <span className="text-xs font-semibold uppercase tracking-wide text-[var(--c-game)]">
            Italiano
          </span>
          {isComplete && <span className="text-xs text-muted-foreground">&#10003; completata</span>}
        </div>
        <p className="text-sm whitespace-pre-wrap">
          {partialText || (isComplete ? '(traduzione vuota)' : 'Traduzione in corso…')}
        </p>
        {highlightTerms.length > 0 && (
          <p className="mt-2 text-xs text-muted-foreground" data-testid="translation-pane-terms">
            Termini glossario applicati: {highlightTerms.join(', ')}
          </p>
        )}
      </section>
      <details className="rounded-md border border-input bg-background p-3 text-sm">
        <summary className="cursor-pointer text-muted-foreground">Originale (EN)</summary>
        <p className="mt-2 whitespace-pre-wrap">{sourceTextEn}</p>
      </details>
    </div>
  );
}
