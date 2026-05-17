/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
/**
 * TranslateParagraphDemo — DEMO-ONLY component (Path 5a / Nanolith demo).
 *
 * Tracked in: <issue URL once filed>
 * Demo session: Nanolith one-shot, NOT for production.
 *
 * Architecture:
 *   Wraps useTranslateParagraph to provide a self-contained mobile-first UI
 *   for translating storybook paragraphs from English to Italian via RAG chat.
 *
 * Design notes:
 *   - Mobile-first layout; single-column on small screens.
 *   - Hardcoded Italian labels (no next-intl — demo only).
 *   - Text contrast: 700-shade tokens per Wave C.1 a11y lesson (≥ 4.5:1).
 *   - aria-live="polite" on output, aria-busy during streaming, role="alert"
 *     on error per WCAG 2.1 SC 4.1.3.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import {
  useTranslateParagraph,
  type ParagraphCitation,
} from '@/lib/gamebook/hooks/useTranslateParagraph';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface TranslateParagraphDemoProps {
  readonly gameId: string;
  readonly agentId: string;
  readonly className?: string;
}

// ---------------------------------------------------------------------------
// Internal sub-components
// ---------------------------------------------------------------------------

function CitationItem({ citation }: { citation: ParagraphCitation }): ReactElement {
  return (
    <li className="text-sm text-foreground">
      <span className="font-medium capitalize">{citation.docType}</span>
      {', p. '}
      {citation.pageNumber}
      {citation.snippet ? ` — "${citation.snippet}"` : ''}
    </li>
  );
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

/**
 * DEMO-ONLY UI for paragraph translation.
 *
 * Renders a form with paragraph ref + optional chapter inputs, a submit button,
 * a live-updating translation output area, a collapsible citations panel, and
 * an error state with retry.
 */
export function TranslateParagraphDemo({
  gameId,
  agentId,
  className,
}: TranslateParagraphDemoProps): ReactElement {
  const { translation, citations, isStreaming, isError, error, translate, reset } =
    useTranslateParagraph({ gameId, agentId });

  function handleSubmit(event: React.FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    const form = event.currentTarget;
    const rawRef = (form.elements.namedItem('paragraphRef') as HTMLInputElement).value.trim();
    const chapter = (form.elements.namedItem('chapterContext') as HTMLInputElement).value.trim();

    if (!rawRef) return;

    // Parse as number if fully numeric, otherwise keep as string
    const paragraphRef = /^\d+$/.test(rawRef) ? parseInt(rawRef, 10) : rawRef;
    translate({
      paragraphRef,
      chapterContext: chapter || undefined,
    });
  }

  return (
    <div
      data-slot="translate-paragraph-demo"
      className={clsx(
        'flex flex-col gap-6 rounded-2xl border border-border bg-card p-5 shadow-sm sm:p-6',
        className
      )}
    >
      {/* Header */}
      <div className="flex flex-col gap-1">
        <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
          DEMO — Traduzione Paragrafo
        </p>
        <h2 className="text-xl font-semibold text-foreground">Traduci paragrafo</h2>
        <p className="text-sm text-muted-foreground">
          Inserisci il numero del paragrafo per ottenere la traduzione italiana.
        </p>
      </div>

      {/* Form */}
      <form
        data-slot="translate-form"
        onSubmit={handleSubmit}
        className="flex flex-col gap-4"
        aria-label="Modulo traduzione paragrafo"
      >
        {/* Paragraph ref input */}
        <div className="flex flex-col gap-1.5">
          <label htmlFor="paragraphRef" className="text-sm font-medium text-foreground">
            Numero paragrafo
          </label>
          <input
            id="paragraphRef"
            name="paragraphRef"
            data-slot="paragraph-input"
            type="text"
            inputMode="numeric"
            placeholder="es. 42"
            required
            disabled={isStreaming}
            className={clsx(
              'h-10 w-full rounded-lg border border-border bg-card px-3 text-sm text-foreground',
              'placeholder:text-muted-foreground',
              'focus:outline-none focus:ring-2 focus:ring-slate-500 focus:border-border',
              'disabled:cursor-not-allowed disabled:opacity-50'
            )}
          />
        </div>

        {/* Chapter context input (optional) */}
        <div className="flex flex-col gap-1.5">
          <label htmlFor="chapterContext" className="text-sm font-medium text-foreground">
            Capitolo (opzionale)
          </label>
          <input
            id="chapterContext"
            name="chapterContext"
            data-slot="chapter-input"
            type="text"
            placeholder="es. Il Mercato"
            disabled={isStreaming}
            className={clsx(
              'h-10 w-full rounded-lg border border-border bg-card px-3 text-sm text-foreground',
              'placeholder:text-muted-foreground',
              'focus:outline-none focus:ring-2 focus:ring-slate-500 focus:border-border',
              'disabled:cursor-not-allowed disabled:opacity-50'
            )}
          />
        </div>

        {/* Submit */}
        <button
          type="submit"
          disabled={isStreaming}
          className={clsx(
            'h-10 rounded-full px-5 text-sm font-medium',
            'bg-card text-white shadow-sm transition-colors',
            'hover:bg-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500',
            'disabled:cursor-not-allowed disabled:opacity-50'
          )}
        >
          {isStreaming ? 'In corso...' : 'Traduci'}
        </button>
      </form>

      {/* Translation output */}
      {(translation || isStreaming) && (
        <div className="flex flex-col gap-2">
          <p className="text-xs font-medium uppercase tracking-wider text-muted-foreground">Traduzione</p>
          <div
            data-slot="translation-output"
            aria-live="polite"
            aria-busy={isStreaming}
            aria-label="Output traduzione"
            className={clsx(
              'min-h-[80px] rounded-xl border border-border bg-muted p-4',
              'text-sm leading-relaxed text-foreground',
              isStreaming && 'animate-pulse'
            )}
          >
            {translation || <span className="text-muted-foreground">Traduzione in corso...</span>}
          </div>
        </div>
      )}

      {/* Citations panel */}
      {citations.length > 0 && (
        <details className="rounded-xl border border-border bg-muted px-4 py-3">
          <summary className="cursor-pointer text-sm font-medium text-foreground">
            Fonti ({citations.length})
          </summary>
          <ul data-slot="translation-citations" className="mt-3 flex flex-col gap-1 pl-2">
            {citations.map((citation, i) => (
              <CitationItem
                key={`${citation.docType}-${citation.pageNumber}-${i}`}
                citation={citation}
              />
            ))}
          </ul>
        </details>
      )}

      {/* Error state */}
      {isError && (
        <div
          data-slot="translation-error"
          role="alert"
          className="flex flex-col gap-3 rounded-xl border border-red-200 bg-red-50 p-4"
        >
          <p className="text-sm font-medium text-red-700">
            {error ?? 'Si è verificato un errore durante la traduzione.'}
          </p>
          <button
            type="button"
            onClick={reset}
            className={clsx(
              'self-start rounded-full px-4 py-1.5 text-sm font-medium',
              'border border-red-300 text-red-700 transition-colors',
              'hover:bg-red-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-500'
            )}
          >
            Riprova
          </button>
        </div>
      )}
    </div>
  );
}
