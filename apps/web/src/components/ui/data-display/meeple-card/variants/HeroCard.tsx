'use client';

import { entityHsl, entityIcon } from '../tokens';
import type { MeepleCardProps } from '../types';

export function HeroCard(props: MeepleCardProps) {
  const { entity, title, subtitle, imageUrl, rating, ratingMax, onClick, className = '' } = props;

  return (
    <div
      className={`group relative min-h-[320px] cursor-pointer overflow-hidden rounded-3xl shadow-[var(--mc-shadow-lg)] transition-transform duration-300 hover:scale-[1.01] ${className}`}
      onClick={onClick}
    >
      {imageUrl ? (
        <img src={imageUrl} alt={title} className="absolute inset-0 h-full w-full object-cover transition-transform duration-500 group-hover:scale-[1.03]" />
      ) : (
        <div className="absolute inset-0 flex items-center justify-center text-8xl opacity-30" style={{ background: entityHsl(entity, 0.15) }}>
          {entityIcon[entity]}
        </div>
      )}
      <div className="absolute inset-0" style={{ background: `linear-gradient(to top, ${entityHsl(entity, 0.85)} 0%, ${entityHsl(entity, 0.4)} 40%, transparent 80%)` }} />
      <div className="relative flex h-full min-h-[320px] flex-col justify-end p-6">
        <h3 className="font-[var(--font-quicksand)] text-2xl font-bold leading-tight text-white drop-shadow-md">{title}</h3>
        {subtitle && <p className="mt-1 text-sm text-white/80">{subtitle}</p>}
        {rating !== undefined && (
          <div className="mt-2 flex items-center gap-1 text-amber-300">
            <span>★</span>
            <span className="text-sm font-bold text-white">{rating.toFixed(1)}</span>
            {ratingMax && <span className="text-xs text-white/60">/ {ratingMax}</span>}
          </div>
        )}
      </div>
    </div>
  );
}
