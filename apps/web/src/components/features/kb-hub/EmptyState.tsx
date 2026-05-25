/**
 * EmptyState — KB hub empty state (no PDF indexed yet).
 *
 * Pure presentational. Issue #1481.
 * Mapped from `admin-mockups/design_files/sp4-kb-hub.jsx` State 2.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface EmptyStateLabels {
  readonly title: string;
  readonly description: string; // supports {gameTitle} placeholder
  readonly ctaLabel: string;
  readonly supportedFormats: string;
}

export interface EmptyStateProps {
  readonly gameTitle: string;
  readonly labels: EmptyStateLabels;
  readonly onUpload: () => void;
  readonly className?: string;
}

export function EmptyState(props: EmptyStateProps): ReactElement {
  const { gameTitle, labels, onUpload, className } = props;
  const description = labels.description.replace('{gameTitle}', gameTitle);

  return (
    <section
      data-slot="kb-hub-empty-state"
      className={clsx(
        'rounded-2xl border border-dashed border-entity-kb/30 bg-card px-10 py-16 text-center shadow-sm',
        className
      )}
    >
      <div
        aria-hidden="true"
        className="mx-auto mb-5 inline-flex h-20 w-20 items-center justify-center rounded-full border-2 border-dashed border-entity-kb/25 bg-entity-kb/10 text-4xl"
      >
        📚
      </div>
      <h3 className="mb-2 font-display text-lg font-bold text-foreground">{labels.title}</h3>
      <p className="mx-auto mb-6 max-w-sm text-sm leading-relaxed text-muted-foreground">
        {description}
      </p>
      <button
        type="button"
        onClick={onUpload}
        data-slot="kb-hub-empty-upload-cta"
        className="inline-flex items-center gap-2 rounded-md bg-entity-kb px-6 py-3 font-display text-sm font-bold text-white shadow-md transition-colors hover:bg-entity-kb/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-entity-kb focus-visible:ring-offset-2"
      >
        <span aria-hidden="true">📤</span>
        {labels.ctaLabel}
      </button>
      <p className="mt-4 text-xs text-muted-foreground">{labels.supportedFormats}</p>
    </section>
  );
}
