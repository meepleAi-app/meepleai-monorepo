/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 primitive — see token-bridge-map.md for migration plan. */
'use client';

import { useState } from 'react';

import { shouldUsePlaceholder } from '@/lib/games/cover-utils';

import { GameCoverPlaceholder } from '../parts/GameCoverPlaceholder';
import { ManaPips } from '../parts/ManaPips';
import { entityHsl, entityIcon } from '../tokens';

import type { MeepleCardProps } from '../types';

export function HeroCard(props: MeepleCardProps) {
  const {
    entity,
    title,
    id,
    subtitle,
    imageUrl,
    rating,
    ratingMax,
    badge,
    metadata,
    manaPips,
    headingLevel,
    showEntityLabel,
    entityLabel,
    onClick,
    className = '',
  } = props;
  const testId = props['data-testid'];

  // #1822: reject BGG runtime URLs and degrade to placeholder on load failure.
  const [imgFailed, setImgFailed] = useState(false);
  const usePlaceholder = imgFailed || shouldUsePlaceholder(imageUrl);
  const showRichPlaceholder = usePlaceholder && entity === 'game' && !!id;

  return (
    <div
      className={`group relative min-h-[320px] cursor-pointer overflow-hidden rounded-3xl shadow-[var(--mc-shadow-lg)] transition-transform duration-300 hover:scale-[1.01] ${className}`}
      onClick={onClick}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      data-entity={entity}
      data-testid={testId}
    >
      {usePlaceholder ? (
        showRichPlaceholder ? (
          <div className="absolute inset-0">
            <GameCoverPlaceholder gameId={id ?? ''} title={title} />
          </div>
        ) : (
          <div
            className="absolute inset-0 flex items-center justify-center text-8xl opacity-30"
            style={{ background: entityHsl(entity, 0.15) }}
            aria-hidden="true"
          >
            {entityIcon[entity]}
          </div>
        )
      ) : (
        <img
          src={imageUrl}
          alt={title}
          className="absolute inset-0 h-full w-full object-cover transition-transform duration-500 group-hover:scale-[1.03]"
          onError={() => setImgFailed(true)}
        />
      )}
      <div
        className="absolute inset-0"
        style={{
          background: `linear-gradient(to top, ${entityHsl(entity, 0.85)} 0%, ${entityHsl(entity, 0.4)} 40%, transparent 80%)`,
        }}
      />
      <div className="relative flex h-full min-h-[320px] flex-col justify-end p-6">
        {badge && (
          <span
            className="absolute right-6 top-6 rounded-full border border-border bg-card/10 px-3 py-1 text-[11px] font-semibold uppercase tracking-wide text-white backdrop-blur-sm"
            data-slot="badge"
          >
            {badge}
          </span>
        )}
        {/* Entity label pill (mockup parity — sp3-shared-game-detail.jsx GameHero):
            small uppercase chip rendered ABOVE the title so the reader scans
            "what kind of thing am I looking at" before the title itself. */}
        {showEntityLabel && entityLabel && (
          <span
            data-testid="hero-card-entity-label"
            className="mb-2 inline-flex w-fit items-center gap-1.5 rounded-full bg-white/15 px-2.5 py-0.5 font-[var(--font-jetbrains)] text-[10px] font-bold uppercase tracking-[0.08em] text-white backdrop-blur-sm"
          >
            <span aria-hidden="true">{entityIcon[entity]}</span>
            <span>{entityLabel}</span>
          </span>
        )}
        {(() => {
          const HeadingTag = `h${headingLevel ?? 3}` as 'h2' | 'h3' | 'h4';
          return (
            <HeadingTag className="font-[var(--font-quicksand)] text-2xl font-bold leading-tight text-white drop-shadow-md">
              {title}
            </HeadingTag>
          );
        })()}
        {subtitle && <p className="mt-1 text-sm text-white/80">{subtitle}</p>}
        {rating !== undefined && (
          <div className="mt-2 flex items-center gap-1 text-amber-300">
            <span>★</span>
            <span className="text-sm font-bold text-white">{rating.toFixed(1)}</span>
            {ratingMax && <span className="text-xs text-white/70">/ {ratingMax}</span>}
          </div>
        )}
        {metadata && metadata.length > 0 && (
          <div
            className="mt-2 flex flex-wrap items-center gap-x-2 gap-y-1 font-[var(--font-jetbrains)] text-[11px] font-medium uppercase tracking-wider text-white/75"
            data-testid="hero-card-metadata"
          >
            {metadata.map((entry, idx) => (
              <span key={`${entry.label}-${idx}`} className="flex items-center gap-2">
                {idx > 0 && <span aria-hidden="true" className="text-white/40">·</span>}
                <span>{entry.label}</span>
              </span>
            ))}
          </div>
        )}
        {manaPips && (
          <div className="mt-3">
            <ManaPips pips={manaPips} size="lg" />
          </div>
        )}
      </div>
    </div>
  );
}
