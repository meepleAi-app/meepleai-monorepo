/**
 * CitationChip — chip cliccabile per una citazione (page + section).
 *
 * Pure component (no fetch, no router). Apre il modal preview via onClick.
 * Spec: docs/superpowers/specs/2026-05-09-game-chat-tab-v1-g5-design.md §3.2
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

import { citedPage } from '@/lib/citations/citedPage';

export interface CitationChipProps {
  /** `null` when the citation has no page (#4819): the chip names the section alone. */
  readonly pageNumber: number | null;
  readonly sectionTitle: string;
  readonly snippet?: string;
  readonly onClick: () => void;
  readonly className?: string;
}

export function CitationChip({
  pageNumber,
  sectionTitle,
  snippet,
  onClick,
  className,
}: CitationChipProps): ReactElement {
  const page = citedPage(pageNumber);

  return (
    <button
      type="button"
      onClick={onClick}
      title={snippet}
      data-slot="citation-chip"
      className={clsx(
        'inline-flex items-center gap-1 rounded-full px-3 py-1',
        'bg-[hsl(var(--c-kb)/0.12)] text-[hsl(var(--c-kb))]',
        'font-mono text-xs font-semibold',
        'border border-transparent transition-colors',
        'hover:bg-[hsl(var(--c-kb)/0.2)] hover:border-[hsl(var(--c-kb)/0.3)]',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-kb))]',
        className
      )}
    >
      <span aria-hidden="true">📖</span>
      {page !== null && (
        <>
          <span>p. {page}</span>
          <span aria-hidden="true">—</span>
        </>
      )}
      <span>{sectionTitle}</span>
    </button>
  );
}
