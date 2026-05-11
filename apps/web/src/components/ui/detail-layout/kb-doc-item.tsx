/**
 * KbDocItem — indexed knowledge-base document row for /shared-games/[id] V2.
 *
 * Wave A.4 (Issue #603). Mirrors mockup `sp3-shared-game-detail.jsx`
 * KBPublicDocItem (lines 579-630).
 *
 * Visual contract:
 *  - 36px icon kb-tinted (teal) with kind-specific emoji (PDF→📄, MD→📝, URL→🔗)
 *  - Mono title (semibold)
 *  - Mono badges row: kind, language, totalChunks (size proxy), indexedAt
 *  - Outlined "Apri ↗" button kb-color
 *
 * Backend `PublishedKbPreviewDto` exposes Id/Language/TotalChunks/IndexedAt.
 * The `kind` parameter is currently inferred to 'pdf' default (PdfDocument is
 * the dominant ingest source); future waves will surface the actual kind once
 * MIME/origin metadata is wired through.
 */

import type { JSX } from 'react';

import clsx from 'clsx';

import { entityHsl } from '@/components/ui/data-display/meeple-card/tokens';

export type KbDocKind = 'pdf' | 'md' | 'url';

const KIND_ICON: Record<KbDocKind, string> = {
  pdf: '📄',
  md: '📝',
  url: '🔗',
};

export interface KbDocItemLabels {
  /** "Indexed on" prefix for the indexedAt date. */
  readonly indexedPrefix: string;
  /** "chunks" suffix for totalChunks badge. */
  readonly chunksLabel: string;
  /** Outlined CTA label, e.g. "Apri ↗". */
  readonly openLabel: string;
  /** Aria label fragment for open button. */
  readonly openAriaLabel: (title: string) => string;
}

export interface KbDocItemProps {
  readonly id: string;
  readonly title: string;
  readonly kind?: KbDocKind;
  readonly language: string;
  readonly totalChunks: number;
  readonly indexedAt: string;
  readonly openHref?: string;
  readonly labels: KbDocItemLabels;
  readonly className?: string;
}

export function KbDocItem({
  id,
  title,
  kind = 'pdf',
  language,
  totalChunks,
  indexedAt,
  openHref,
  labels,
  className,
}: KbDocItemProps): JSX.Element {
  const openable = Boolean(openHref);
  return (
    <article
      data-slot="shared-game-detail-kb-item"
      data-kb-id={id}
      data-kb-kind={kind}
      className={clsx(
        'flex items-center gap-3 rounded-lg border border-border bg-card p-3',
        'transition-[transform,box-shadow,border-color] duration-150',
        'hover:-translate-y-0.5 hover:shadow-md',
        className
      )}
      style={{ borderColor: entityHsl('kb', 0.18) }}
    >
      {/* Icon */}
      <span
        aria-hidden="true"
        className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md text-[18px]"
        style={{
          backgroundColor: entityHsl('kb', 0.12),
          color: entityHsl('kb'),
        }}
      >
        {KIND_ICON[kind]}
      </span>

      {/* Body */}
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <h3 className="m-0 truncate font-mono text-[13px] font-semibold text-foreground">
          {title}
        </h3>
        <div className="flex flex-wrap items-center gap-1.5 font-mono text-[10px] uppercase tracking-[0.06em] text-[hsl(var(--text-muted))]">
          <span className="rounded-sm bg-muted px-1.5 py-0.5">{kind.toUpperCase()}</span>
          <span className="rounded-sm bg-muted px-1.5 py-0.5">{language.toUpperCase()}</span>
          <span className="rounded-sm bg-muted px-1.5 py-0.5" data-dynamic="number">
            {totalChunks} {labels.chunksLabel}
          </span>
          <span>
            {labels.indexedPrefix}{' '}
            <time dateTime={indexedAt} data-dynamic="datetime">
              {new Date(indexedAt).toLocaleDateString()}
            </time>
          </span>
        </div>
      </div>

      {/* CTA */}
      <div className="flex shrink-0 items-center">
        {openable ? (
          <a
            href={openHref}
            aria-label={labels.openAriaLabel(title)}
            className={clsx(
              'inline-flex items-center gap-1 rounded-full border px-3 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-[0.06em] no-underline',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2'
            )}
            style={{ borderColor: entityHsl('kb', 0.5), color: entityHsl('kb') }}
          >
            {labels.openLabel}
          </a>
        ) : (
          <span
            aria-disabled="true"
            className="inline-flex cursor-not-allowed items-center gap-1 rounded-full border px-3 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-[0.06em] opacity-50"
            style={{ borderColor: entityHsl('kb', 0.3), color: entityHsl('kb') }}
          >
            {labels.openLabel}
          </span>
        )}
      </div>
    </article>
  );
}
