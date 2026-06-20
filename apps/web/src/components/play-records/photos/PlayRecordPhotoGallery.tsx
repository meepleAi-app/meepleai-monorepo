'use client';

import { useState, useCallback } from 'react';

import clsx from 'clsx';

import { Dialog, DialogContent, DialogTitle } from '@/components/ui/overlays/dialog';
import type { PlayRecordPhoto } from '@/lib/api/schemas/play-records.schemas';

export interface PlayRecordPhotoGalleryLabels {
  title: string;
  emptyTitle: string;
  emptyDescription: string;
  photoAltFallback: string;
  ocrResultTitle: string;
  prev: string;
  next: string;
}

export interface PlayRecordPhotoGalleryProps {
  photos: readonly PlayRecordPhoto[];
  labels: PlayRecordPhotoGalleryLabels;
  className?: string;
}

export function PlayRecordPhotoGallery({
  photos,
  labels,
  className,
}: PlayRecordPhotoGalleryProps): React.JSX.Element {
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

  if (photos.length === 0) {
    return (
      <section
        data-slot="play-record-photos"
        data-empty="true"
        role="status"
        className={clsx(
          'flex flex-col items-center gap-2 rounded-lg border border-dashed border-border bg-card px-4 py-8 text-center',
          className
        )}
      >
        <span aria-hidden="true" className="text-3xl">
          📷
        </span>
        <h2 className="font-display text-sm font-extrabold text-foreground">{labels.emptyTitle}</h2>
        <p className="text-xs text-muted-foreground">{labels.emptyDescription}</p>
      </section>
    );
  }

  const active = openIndex !== null ? photos[openIndex] : null;

  return (
    <section data-slot="play-record-photos" className={clsx('flex flex-col gap-2', className)}>
      <h2 className="font-display text-base font-extrabold text-foreground">
        <span aria-hidden="true" className="mr-1.5">
          📷
        </span>
        {labels.title}
      </h2>
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        {photos.map((p, i) => {
          const alt = p.caption ?? labels.photoAltFallback;
          return (
            <button
              key={p.id}
              type="button"
              onClick={() => setOpenIndex(i)}
              aria-label={alt}
              className="group relative aspect-square overflow-hidden rounded-md border border-border bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {/* eslint-disable-next-line @next/next/no-img-element -- thumbnail tile */}
              <img
                src={p.thumbnailUrl ?? p.url}
                alt=""
                aria-hidden="true"
                loading="lazy"
                className="h-full w-full object-cover transition-transform group-hover:scale-105"
              />
            </button>
          );
        })}
      </div>

      <Dialog open={active !== null} onOpenChange={o => !o && close()}>
        <DialogContent className="max-w-3xl">
          <DialogTitle className="sr-only">
            {active?.caption ?? labels.photoAltFallback}
          </DialogTitle>
          {active && (
            <div className="flex flex-col gap-3">
              {/* eslint-disable-next-line @next/next/no-img-element -- full-res lightbox */}
              <img
                src={active.url}
                alt={active.caption ?? labels.photoAltFallback}
                className="max-h-[70vh] w-full rounded-md object-contain"
              />
              {active.caption && (
                <p className="text-sm font-medium text-foreground">{active.caption}</p>
              )}
              {active.ocrText && (
                <p className="rounded-md border border-border bg-muted px-3 py-2 text-xs text-muted-foreground">
                  <span className="font-semibold">{labels.ocrResultTitle}:</span> {active.ocrText}
                </p>
              )}
              {photos.length > 1 && (
                <div className="flex justify-between">
                  <button
                    type="button"
                    onClick={prev}
                    aria-label={labels.prev}
                    className="rounded-md px-3 py-1.5 text-sm font-bold text-foreground hover:bg-muted"
                  >
                    ← {labels.prev}
                  </button>
                  <button
                    type="button"
                    onClick={next}
                    aria-label={labels.next}
                    className="rounded-md px-3 py-1.5 text-sm font-bold text-foreground hover:bg-muted"
                  >
                    {labels.next} →
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
