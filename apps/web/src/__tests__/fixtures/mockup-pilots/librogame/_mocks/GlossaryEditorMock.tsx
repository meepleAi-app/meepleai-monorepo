/**
 * Template K mocks for GlossaryEditorModal forward-refactor states.
 *
 * These sub-screens exist in the mockup spec (librogame-runthrough-glossary-editor.html
 * states 06 + 07) but are NOT implemented in the real GlossaryEditorModal component
 * as of DS-17 Phase D-2 (2026-06-22).
 *
 * GlossaryEditorBulkImportMock — state-06-bulk-import-card:
 *   Inline card that appears when OCR detects ≥3 new glossary-unregistered words in
 *   a paragraph. Title "N nuove parole rilevate" + chip preview + 2 CTAs:
 *   "Aggiungi tutte" and "Rivedi" (→ multi-select modal).
 *   Not implemented: requires OCR post-processing pipeline to emit `newTerms[]`
 *   alongside the translation result (spec sezione 1c, GlossaryEntry.contexts[]).
 *
 * GlossaryEditorVariantsMock — state-07-variant-expansion:
 *   Glossary index row with contexts.length > 1. Click expands to show N variants
 *   per book, each with book badge, definition, first_seen link, and delete button.
 *   Not implemented: requires `GlossaryEntry.contexts[]` schema migration (backend
 *   single-context today; multi-context is a planned extension per mockup data model).
 *
 * Replace these mocks with the real components when they are implemented.
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-glossary-editor.html
 *   - state-06 · bulk-import-card (≥3 nuove parole)
 *   - state-07 · variant-expansion (contexts.length > 1)
 *
 * Refs: DS-17 Phase D-2, umbrella #2063, sub-issue #2174.
 */

import type { ReactElement } from 'react';

// ---------------------------------------------------------------------------
// Frame05 — Bulk Import Card mock
// ---------------------------------------------------------------------------

export interface GlossaryEditorBulkImportMockProps {
  /** Number of new unregistered terms detected. */
  readonly newTermCount?: number;
  /** Preview chips of the detected terms. */
  readonly previewTerms?: readonly string[];
}

export function GlossaryEditorBulkImportMock({
  newTermCount = 4,
  previewTerms = ['ferrigni', 'runa', 'soglia', 'ardenel'],
}: GlossaryEditorBulkImportMockProps): ReactElement {
  return (
    <div
      data-slot="glossary-editor-bulk-import-mock"
      data-testid="glossary-editor-bulk-import"
      className="relative min-h-[480px] bg-background p-4"
      data-theme="light"
    >
      {/* Modal shell (same chrome as real GlossaryEditorModal) */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Aggiungi parole al glossario"
        data-testid="glossary-editor-dialog-mobile"
        className="mx-auto max-w-sm rounded-xl border border-border bg-card p-5 shadow-lg"
      >
        <header className="mb-3 flex items-center justify-between">
          <h2 className="text-base font-semibold text-foreground">
            {newTermCount} nuove parole rilevate
          </h2>
          <button
            type="button"
            aria-label="Chiudi"
            className="text-muted-foreground hover:text-foreground"
          >
            ✕
          </button>
        </header>

        {/* Chip preview */}
        <div className="mb-4 flex flex-wrap gap-2">
          {previewTerms.map(term => (
            <span
              key={term}
              className="rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-muted-foreground"
            >
              {term}
            </span>
          ))}
        </div>

        {/* CTAs */}
        <div className="flex gap-2">
          <button
            type="button"
            className="flex-1 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground"
          >
            Aggiungi tutte
          </button>
          <button
            type="button"
            className="flex-1 rounded-lg border border-border px-4 py-2 text-sm font-medium text-foreground"
          >
            Rivedi
          </button>
        </div>

        {/* Forward-refactor notice */}
        <p className="mt-3 text-xs text-muted-foreground">
          <em>
            forward-refactor — bulk import card (≥3 nuove parole). Richiede{' '}
            <code>GlossaryEntry.contexts[]</code> + OCR post-processing pipeline.
          </em>
        </p>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Frame06 — Variant Expansion mock
// ---------------------------------------------------------------------------

export interface GlossaryVariant {
  readonly bookName: string;
  readonly definition: string | null;
  readonly firstSeenRef: string;
}

export interface GlossaryEditorVariantsMockProps {
  /** The base EN term being expanded. */
  readonly termEn?: string;
  /** Base (single-context) Italian translation. */
  readonly baseTermIt?: string;
  /** Per-book variant list. */
  readonly variants?: readonly GlossaryVariant[];
}

export function GlossaryEditorVariantsMock({
  termEn = 'sentinel',
  baseTermIt = 'soldato di guardia',
  variants = [
    { bookName: 'Press Start', definition: null, firstSeenRef: '§147' },
    {
      bookName: 'Rules Game',
      definition: 'punto di osservazione strategica',
      firstSeenRef: '§63',
    },
  ],
}: GlossaryEditorVariantsMockProps): ReactElement {
  return (
    <div
      data-slot="glossary-editor-variants-mock"
      data-testid="glossary-editor-variants"
      className="relative min-h-[480px] bg-background p-4"
      data-theme="light"
    >
      {/* Glossary index shell */}
      <div className="mx-auto max-w-sm rounded-xl border border-border bg-card shadow-lg">
        <header className="flex items-center justify-between border-b border-border px-4 py-3">
          <span className="text-sm font-semibold text-foreground">Glossario campagna</span>
        </header>

        {/* Expanded row */}
        <div
          className="border-b border-border"
          data-testid="glossary-variant-row"
          aria-expanded="true"
        >
          <button
            type="button"
            className="flex w-full items-center justify-between px-4 py-3 text-left"
            aria-expanded="true"
          >
            <span>
              <span className="text-sm font-medium text-foreground">{termEn}</span>
              <span className="ml-2 text-xs text-muted-foreground">→ {baseTermIt}</span>
            </span>
            {/* ChevronDown rotated to indicate expanded */}
            <span aria-hidden="true" className="rotate-180 text-muted-foreground">
              ▾
            </span>
          </button>

          {/* Variants list */}
          <ul className="divide-y divide-border border-t border-border">
            {variants.map((v, i) => (
              <li key={i} className="flex items-start gap-3 px-4 py-3">
                <div className="flex-1">
                  <span className="inline-flex items-center gap-1 rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
                    📖 {v.bookName}
                  </span>
                  <p className="mt-1 text-sm text-foreground">
                    {v.definition ?? (
                      <span className="italic text-muted-foreground">(usa base)</span>
                    )}
                  </p>
                  <a
                    href="#"
                    className="mt-0.5 text-xs text-muted-foreground underline-offset-2 hover:underline"
                  >
                    Vedi paragrafo originale {v.firstSeenRef}
                  </a>
                </div>
                <button
                  type="button"
                  aria-label={`Elimina variante ${v.bookName}`}
                  className="mt-0.5 text-muted-foreground hover:text-destructive"
                >
                  🗑
                </button>
              </li>
            ))}
          </ul>
        </div>

        {/* Forward-refactor notice */}
        <p className="px-4 py-3 text-xs text-muted-foreground">
          <em>
            forward-refactor — variant expansion (contexts.length &gt; 1). Richiede{' '}
            <code>GlossaryEntry.contexts[]</code> schema migration backend.
          </em>
        </p>
      </div>
    </div>
  );
}
