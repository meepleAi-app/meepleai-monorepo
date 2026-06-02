'use client';

import { useState } from 'react';

import { shouldUsePlaceholder } from '@/lib/games/cover-utils';

import { entityHsl, entityIcon } from '../tokens';
import { GameCoverPlaceholder } from './GameCoverPlaceholder';

import type { MeepleEntityType, MeepleCardVariant } from '../types';

interface CoverProps {
  entity: MeepleEntityType;
  variant: MeepleCardVariant;
  imageUrl?: string;
  alt?: string;
  /**
   * Stable id used by {@link GameCoverPlaceholder} to derive a deterministic
   * background hue. Only consumed when `entity === 'game'` and the image
   * cannot be rendered. Optional — when missing the cover falls back to the
   * legacy entity icon.
   */
  gameId?: string;
}

const aspectRatioClass: Record<MeepleCardVariant, string> = {
  grid: 'aspect-[7/10]',
  list: 'aspect-square',
  compact: 'aspect-square',
  featured: 'aspect-video',
  hero: 'aspect-video',
  focus: 'aspect-[7/10]',
};

export function Cover({ entity, variant, imageUrl, alt, gameId }: CoverProps) {
  const gradientColor = entityHsl(entity, 0.15);

  // #1822: refuse to render BGG-hosted URLs at runtime (rate-limit + ToS).
  // Also detects the runtime fall-through when an otherwise-allowed URL fails
  // to load — `onError` flips this to true, the next render switches to the
  // placeholder without leaving a broken icon.
  const [hasImgError, setHasImgError] = useState(false);
  const usePlaceholder = hasImgError || shouldUsePlaceholder(imageUrl);

  // Pick the placeholder flavor:
  //  - game cards with a known id+title → rich GameCoverPlaceholder (hue + initials)
  //  - everything else → legacy entity-icon swatch
  const showRichPlaceholder = usePlaceholder && entity === 'game' && !!gameId;

  return (
    <div className={`relative overflow-hidden ${aspectRatioClass[variant]}`}>
      {usePlaceholder ? (
        showRichPlaceholder ? (
          <GameCoverPlaceholder gameId={gameId ?? ''} title={alt ?? ''} />
        ) : (
          <div
            className="flex h-full w-full items-center justify-center text-5xl opacity-50"
            style={{ background: entityHsl(entity, 0.08) }}
            aria-hidden="true"
          >
            {entityIcon[entity]}
          </div>
        )
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
