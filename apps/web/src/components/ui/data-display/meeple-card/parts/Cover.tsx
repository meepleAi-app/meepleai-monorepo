'use client';

import { useState, useEffect } from 'react';

import { shouldUsePlaceholder } from '@/lib/games/cover-utils';

import { entityHsl, entityIcon } from '../tokens';

import type { MeepleEntityType, MeepleCardVariant } from '../types';

interface CoverProps {
  entity: MeepleEntityType;
  variant: MeepleCardVariant;
  imageUrl?: string;
  alt?: string;
  /**
   * Stable id kept for backwards compatibility with existing consumers.
   * Post-#1856 (DEC-2) the emoji-band fallback no longer uses GameCoverPlaceholder,
   * so gameId is unused inside this component — retained to keep the consumer API
   * stable.
   */
  gameId?: string;
  /**
   * UTF-8 emoji rendered in the squat-band fallback when `imageUrl` is absent or
   * blocked. Falls back to `entityIcon[entity]`. See #1856 DEC-2/DEC-3.
   */
  coverEmoji?: string;
}

const aspectRatioClass: Record<MeepleCardVariant, string> = {
  grid: 'aspect-[7/10]',
  list: 'aspect-square',
  compact: 'aspect-square',
  featured: 'aspect-video',
  hero: 'aspect-video',
  focus: 'aspect-[7/10]',
};

export function Cover({ entity, variant, imageUrl, alt, coverEmoji }: CoverProps) {
  const gradientColor = entityHsl(entity, 0.15);
  const bandGradient = `linear-gradient(135deg, ${entityHsl(entity, 0.35)} 0%, ${entityHsl(entity, 0.55)} 100%)`;

  // #1822: refuse to render BGG-hosted URLs at runtime (rate-limit + ToS).
  // `onError` flips this to true so the next render switches to emoji-band.
  const [hasImgError, setHasImgError] = useState(false);

  // Reset hasImgError when imageUrl changes (e.g., consumer updates to a new URL).
  // This ensures the component recovers from a broken URL to a valid one.
  useEffect(() => {
    setHasImgError(false);
  }, [imageUrl]);

  const usePlaceholder = hasImgError || shouldUsePlaceholder(imageUrl);

  const emoji = coverEmoji ?? entityIcon[entity];

  return (
    <div className={`relative overflow-hidden ${aspectRatioClass[variant]}`}>
      {usePlaceholder ? (
        <div
          data-slot="cover-emoji-band"
          className="flex h-[100px] w-full items-center justify-center"
          style={{ background: bandGradient }}
          aria-hidden="true"
        >
          <span className="text-[38px]" style={{ filter: 'drop-shadow(0 2px 6px rgba(0,0,0,.3))' }}>
            {emoji}
          </span>
        </div>
      ) : (
        <img
          src={imageUrl}
          alt={alt ?? ''}
          className="h-full w-full object-cover transition-transform duration-500 ease-out group-hover:scale-[1.06]"
          loading="lazy"
          onError={() => setHasImgError(true)}
        />
      )}
      {/* Shimmer overlay */}
      <div
        className="pointer-events-none absolute inset-0 -translate-x-full transition-none group-hover:animate-[shimmer_0.8s_ease-out_forwards]"
        style={{
          background:
            'linear-gradient(105deg, transparent 40%, rgba(255,255,255,0.25) 50%, transparent 60%)',
        }}
      />
      {/* Entity gradient overlay */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background: `linear-gradient(to top, ${gradientColor}, transparent 60%)`,
        }}
      />
    </div>
  );
}
