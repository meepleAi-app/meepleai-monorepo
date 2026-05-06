/**
 * PhotosGallery — Wave D.3 v2 component (Issue #756).
 *
 * Responsive photo grid (4-col desktop / 2-col mobile) of session snapshots.
 * Each photo lazy-loads its image and exposes a click handler so the
 * orchestrator can mount a lightbox dialog (out-of-scope D.3 — the prop is
 * provided but defaults to a no-op).
 *
 * Mockup mapping:
 *   - admin-mockups/design_files/sp4-session-summary.jsx (PhotoGallery)
 *
 * Contract reference: docs/frontend/contracts/sessions-id-summary-hooks.md §5.8.
 *
 * MeepleCard divergence (Gate C): 1:1 image tile grid. MeepleCard's avatar
 * + title composition cannot be reduced to a single full-bleed image tile.
 * DIVERGE.
 *
 * A11y:
 *   - Each photo is a `<button>` (not `<img>` alone) so keyboard activation
 *     works for future lightbox.
 *   - `<img alt={caption ?? fallback}>` per contract — orchestrator passes
 *     captionFallback for snapshots without explicit caption.
 *   - Empty state is a `<div role="status">` with placeholder copy.
 *
 * Pure component: orchestrator resolves all i18n strings.
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { SessionSnapshotDto } from '@/lib/api/clients/sessionSnapshotsClient';

export interface PhotosGalleryLabels {
  readonly title: string;
  readonly emptyTitle: string;
  readonly emptyDescription: string;
  /** Fallback alt text for snapshots whose `caption` is null. */
  readonly photoAltFallback: string;
  /** Optional CTA label for adding photos to an empty session. */
  readonly addPhotoCta?: string;
}

export interface PhotosGalleryProps {
  readonly snapshots: readonly SessionSnapshotDto[];
  /** Optional click handler — receives the snapshot. Out-of-scope lightbox. */
  readonly onPhotoClick?: (snapshot: SessionSnapshotDto) => void;
  readonly labels: PhotosGalleryLabels;
  readonly className?: string;
}

export function PhotosGallery({
  snapshots,
  onPhotoClick,
  labels,
  className,
}: PhotosGalleryProps): ReactElement {
  const isEmpty = snapshots.length === 0;

  if (isEmpty) {
    return (
      <section
        data-slot="photos-gallery"
        data-empty="true"
        className={clsx(
          'flex flex-col items-center gap-2 rounded-lg border border-dashed border-border bg-card px-4 py-8 text-center',
          className
        )}
        role="status"
      >
        <span aria-hidden="true" className="text-3xl">
          📷
        </span>
        <h3 className="font-display text-sm font-extrabold text-foreground">{labels.emptyTitle}</h3>
        <p className="text-xs text-muted-foreground">{labels.emptyDescription}</p>
      </section>
    );
  }

  return (
    <section
      data-slot="photos-gallery"
      data-empty={undefined}
      className={clsx('flex flex-col gap-2', className)}
    >
      <h3 className="font-display text-base font-extrabold text-foreground">
        <span aria-hidden="true" className="mr-1.5">
          📷
        </span>
        {labels.title}
      </h3>
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4" data-slot="photos-gallery-grid">
        {snapshots.map(s => {
          const firstImage = s.images[0];
          const downloadUrl = firstImage?.downloadUrl ?? null;
          const altText = s.caption ?? labels.photoAltFallback;
          return (
            <button
              key={s.id}
              type="button"
              data-slot="photo-item"
              data-snapshot-id={s.id}
              onClick={() => onPhotoClick?.(s)}
              disabled={!onPhotoClick}
              className={clsx(
                'group relative aspect-square overflow-hidden rounded-md border border-border bg-muted',
                onPhotoClick
                  ? 'cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
                  : 'cursor-default'
              )}
              aria-label={altText}
            >
              {downloadUrl ? (
                // eslint-disable-next-line @next/next/no-img-element -- thumbnail tile; orchestrator may lift to <Image> when lightbox lands
                <img
                  src={downloadUrl}
                  alt={altText}
                  loading="lazy"
                  className="h-full w-full object-cover transition-transform group-hover:scale-105"
                />
              ) : (
                <div
                  aria-hidden="true"
                  className="flex h-full w-full items-center justify-center bg-gradient-to-br from-[hsla(240,60%,55%,0.18)] to-[hsla(142,70%,31%,0.1)] text-3xl"
                >
                  📷
                </div>
              )}
              {s.caption && (
                <span
                  className={clsx(
                    'absolute bottom-1.5 left-1.5 max-w-[calc(100%-1rem)] truncate rounded-full px-2 py-0.5',
                    'font-mono text-[9px] font-extrabold text-white',
                    'bg-black/45'
                  )}
                >
                  {s.caption}
                </span>
              )}
            </button>
          );
        })}
      </div>
    </section>
  );
}
