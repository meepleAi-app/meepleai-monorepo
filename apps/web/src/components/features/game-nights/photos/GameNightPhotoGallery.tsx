'use client';

import { useCallback, useState } from 'react';

import clsx from 'clsx';

import { Dialog, DialogContent, DialogTitle } from '@/components/ui/overlays/dialog';
import { useTranslation } from '@/hooks/useTranslation';
import type { GameNightPhotoDto } from '@/lib/api/schemas/game-nights.schemas';

export interface GameNightPhotoGalleryProps {
  photos: readonly GameNightPhotoDto[];
  /** When provided, renders an "Add photo" affordance. */
  onAddPhoto?: () => void;
  /** When provided, renders a per-photo delete affordance. */
  onDeletePhoto?: (photoId: string) => void;
  /** Gates the delete affordance per-photo (e.g. organizer or uploader). Defaults to all. */
  canDeletePhoto?: (photo: GameNightPhotoDto) => boolean;
  className?: string;
}

/**
 * Recap photo gallery for a game night summary (#2724). Thumbnail grid + lightbox.
 * Self-contained (i18n via useTranslation) so it drops into both the authenticated
 * and the public shared summary views.
 */
export function GameNightPhotoGallery({
  photos,
  onAddPhoto,
  onDeletePhoto,
  canDeletePhoto,
  className,
}: GameNightPhotoGalleryProps): React.JSX.Element {
  const { t } = useTranslation();
  const [openIndex, setOpenIndex] = useState<number | null>(null);

  const close = useCallback(() => setOpenIndex(null), []);
  const prev = useCallback(
    () => setOpenIndex(i => (i === null ? null : (i - 1 + photos.length) % photos.length)),
    [photos.length]
  );
  const next = useCallback(
    () => setOpenIndex(i => (i === null ? null : (i + 1) % photos.length)),
    [photos.length]
  );

  const fallbackAlt = t('gameNightDetail.photos.photoAltFallback');
  const active = openIndex !== null ? photos[openIndex] : null;

  const addButton = onAddPhoto ? (
    <button
      type="button"
      onClick={onAddPhoto}
      className="rounded-md border border-border bg-card px-3 py-1.5 text-sm font-bold text-foreground hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      + {t('gameNightDetail.photos.addCta')}
    </button>
  ) : null;

  if (photos.length === 0) {
    return (
      <section
        data-slot="game-night-photos"
        data-empty="true"
        className={clsx('flex flex-col gap-2', className)}
      >
        <div className="flex items-center justify-between">
          <h2 className="font-display text-base font-extrabold text-foreground">
            <span aria-hidden="true" className="mr-1.5">
              📷
            </span>
            {t('gameNightDetail.photos.title')}
          </h2>
          {addButton}
        </div>
        <div
          role="status"
          className="flex flex-col items-center gap-2 rounded-lg border border-dashed border-border bg-card px-4 py-8 text-center"
        >
          <span aria-hidden="true" className="text-3xl">
            📷
          </span>
          <p className="text-sm font-extrabold text-foreground">
            {t('gameNightDetail.photos.emptyTitle')}
          </p>
          <p className="text-xs text-muted-foreground">
            {t('gameNightDetail.photos.emptyDescription')}
          </p>
        </div>
      </section>
    );
  }

  return (
    <section data-slot="game-night-photos" className={clsx('flex flex-col gap-2', className)}>
      <div className="flex items-center justify-between">
        <h2 className="font-display text-base font-extrabold text-foreground">
          <span aria-hidden="true" className="mr-1.5">
            📷
          </span>
          {t('gameNightDetail.photos.title')}
        </h2>
        {addButton}
      </div>

      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        {photos.map((p, i) => {
          const alt = p.caption ?? fallbackAlt;
          return (
            <div key={p.id} className="group relative aspect-square">
              <button
                type="button"
                onClick={() => setOpenIndex(i)}
                aria-label={alt}
                className="h-full w-full overflow-hidden rounded-md border border-border bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                {/* eslint-disable-next-line @next/next/no-img-element -- thumbnail tile */}
                <img
                  src={p.thumbnailUrl ?? p.photoUrl}
                  alt=""
                  aria-hidden="true"
                  loading="lazy"
                  className="h-full w-full object-cover transition-transform group-hover:scale-105"
                />
              </button>
              {onDeletePhoto && (!canDeletePhoto || canDeletePhoto(p)) && (
                <button
                  type="button"
                  onClick={() => onDeletePhoto(p.id)}
                  aria-label={t('gameNightDetail.photos.deleteCta')}
                  className="absolute right-1 top-1 hidden h-6 w-6 items-center justify-center rounded-full bg-background/80 text-sm font-bold text-foreground shadow group-hover:flex focus-visible:flex focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  ×
                </button>
              )}
            </div>
          );
        })}
      </div>

      <Dialog open={active !== null} onOpenChange={o => !o && close()}>
        <DialogContent className="max-w-3xl">
          <DialogTitle className="sr-only">{active?.caption ?? fallbackAlt}</DialogTitle>
          {active && (
            <div className="flex flex-col gap-3">
              {/* eslint-disable-next-line @next/next/no-img-element -- full-res lightbox */}
              <img
                src={active.photoUrl}
                alt={active.caption ?? fallbackAlt}
                className="max-h-[70vh] w-full rounded-md object-contain"
              />
              {active.caption && (
                <p className="text-sm font-medium text-foreground">{active.caption}</p>
              )}
              {photos.length > 1 && (
                <div className="flex justify-between">
                  <button
                    type="button"
                    onClick={prev}
                    aria-label={t('gameNightDetail.photos.prev')}
                    className="rounded-md px-3 py-1.5 text-sm font-bold text-foreground hover:bg-muted"
                  >
                    ← {t('gameNightDetail.photos.prev')}
                  </button>
                  <button
                    type="button"
                    onClick={next}
                    aria-label={t('gameNightDetail.photos.next')}
                    className="rounded-md px-3 py-1.5 text-sm font-bold text-foreground hover:bg-muted"
                  >
                    {t('gameNightDetail.photos.next')} →
                  </button>
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </section>
  );
}
