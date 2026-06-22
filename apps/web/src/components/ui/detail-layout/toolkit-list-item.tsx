/**
 * ToolkitListItem — published toolkit row for /shared-games/[id] V2.
 *
 * Wave A.4 (Issue #603). Mirrors mockup `sp3-shared-game-detail.jsx`
 * ToolkitPublicListItem (lines 398-494).
 *
 * Visual contract:
 *  - 44px icon flex-shrink-0 with toolkit-tinted bg + emoji
 *  - Title (display 600) + optional badges (official / Top10) inline
 *  - Mono meta line: author • version • tools • updated
 *  - Description (line-clamp 2)
 *  - Footer right-aligned: outlined "Anteprima" button toolkit-color
 *
 * Backend DTO `PublishedToolkitPreviewDto` exposes only Id/Name/OwnerId/OwnerName/
 * LastUpdatedAt today; mockup-only fields (rating, ratingCount, installs, version,
 * toolsCount) are deferred — frontend renders sensible defaults / hides them
 * conditionally per spec §3.3 "fields without entity backing".
 */

import type { JSX } from 'react';

import clsx from 'clsx';

import { entityHsl } from '@/components/ui/data-display/meeple-card/tokens';

export interface ToolkitListItemLabels {
  /** "by" connector for author. */
  readonly authorPrefix: string;
  /** "Last updated" prefix for date. */
  readonly updatedPrefix: string;
  /** Outlined preview button label, e.g. "Anteprima". */
  readonly previewLabel: string;
  /** Aria label fragment for the preview button: takes the toolkit name. */
  readonly previewAriaLabel: (name: string) => string;
}

export interface ToolkitListItemProps {
  readonly id: string;
  readonly name: string;
  readonly ownerName: string;
  readonly lastUpdatedAt: string;
  /** Optional preview href; when omitted the button renders disabled. */
  readonly previewHref?: string;
  readonly description?: string;
  readonly labels: ToolkitListItemLabels;
  readonly className?: string;
}

export function ToolkitListItem({
  id,
  name,
  ownerName,
  lastUpdatedAt,
  previewHref,
  description,
  labels,
  className,
}: ToolkitListItemProps): JSX.Element {
  const previewable = Boolean(previewHref);
  return (
    <article
      data-slot="shared-game-detail-toolkit-item"
      data-toolkit-id={id}
      className={clsx(
        'flex items-start gap-3 rounded-lg border border-border bg-card p-3.5',
        'transition-[transform,box-shadow,border-color] duration-150',
        'hover:-translate-y-0.5 hover:shadow-md',
        className
      )}
      style={{
        borderColor: entityHsl('toolkit', 0.18),
      }}
    >
      {/* Icon */}
      <span
        aria-hidden="true"
        className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg text-[22px]"
        style={{
          backgroundColor: entityHsl('toolkit', 0.12),
          color: entityHsl('toolkit'),
        }}
      >
        🧰
      </span>

      {/* Body */}
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <h3 className="m-0 font-display text-[15px] font-semibold leading-tight text-foreground">
          {name}
        </h3>

        <p className="m-0 font-mono text-[10px] uppercase tracking-[0.06em] text-[hsl(var(--text-muted))]">
          {labels.authorPrefix} {ownerName} · {labels.updatedPrefix}{' '}
          <time dateTime={lastUpdatedAt} data-dynamic="datetime">
            {new Date(lastUpdatedAt).toLocaleDateString()}
          </time>
        </p>

        {description ? (
          <p className="m-0 line-clamp-2 text-sm text-foreground/80">{description}</p>
        ) : null}
      </div>

      {/* CTA */}
      <div className="flex shrink-0 items-center self-center">
        {previewable ? (
          <a
            href={previewHref}
            aria-label={labels.previewAriaLabel(name)}
            className={clsx(
              'inline-flex items-center gap-1 rounded-full border px-3 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-[0.06em] no-underline',
              'transition-[background-color,color,transform] duration-150',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2'
            )}
            style={{
              borderColor: entityHsl('toolkit', 0.5),
              color: entityHsl('toolkit'),
            }}
          >
            {labels.previewLabel}
          </a>
        ) : (
          <span
            aria-disabled="true"
            className="inline-flex cursor-not-allowed items-center gap-1 rounded-full border px-3 py-1.5 font-mono text-[11px] font-semibold uppercase tracking-[0.06em] opacity-50"
            style={{
              borderColor: entityHsl('toolkit', 0.3),
              color: entityHsl('toolkit'),
            }}
          >
            {labels.previewLabel}
          </span>
        )}
      </div>
    </article>
  );
}
