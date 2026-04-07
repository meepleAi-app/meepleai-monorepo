'use client';

import { entityHsl, entityIcon } from '../tokens';
import type { MeepleEntityType, MeepleCardVariant } from '../types';

interface CoverProps {
  entity: MeepleEntityType;
  variant: MeepleCardVariant;
  imageUrl?: string;
  alt?: string;
}

const aspectRatioClass: Record<MeepleCardVariant, string> = {
  grid: 'aspect-[7/10]',
  list: 'aspect-square',
  compact: 'aspect-square',
  featured: 'aspect-video',
  hero: 'aspect-video',
};

export function Cover({ entity, variant, imageUrl, alt }: CoverProps) {
  const gradientColor = entityHsl(entity, 0.15);

  return (
    <div className={`relative overflow-hidden ${aspectRatioClass[variant]}`}>
      {imageUrl ? (
        <img
          src={imageUrl}
          alt={alt ?? ''}
          className="h-full w-full object-cover transition-transform duration-500 ease-out group-hover:scale-[1.06]"
          loading="lazy"
        />
      ) : (
        <div
          className="flex h-full w-full items-center justify-center text-5xl opacity-50"
          style={{ background: entityHsl(entity, 0.08) }}
        >
          {entityIcon[entity]}
        </div>
      )}
      {/* Shimmer overlay */}
      <div
        className="pointer-events-none absolute inset-0 -translate-x-full transition-none group-hover:animate-[shimmer_0.8s_ease-out_forwards]"
        style={{
          background: 'linear-gradient(105deg, transparent 40%, rgba(255,255,255,0.25) 50%, transparent 60%)',
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
