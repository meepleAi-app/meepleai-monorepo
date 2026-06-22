/**
 * GameDetailHero - v2 Wave C.1 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (GameHero).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #573)
 *
 * Pure presentational component (mirror Wave B.3 LibraryHeroDesktop):
 *   - All i18n strings injected via `labels` (page-client resolves upfront).
 *   - Variant `'own'` (in library) vs `'community'` (not in library) controls
 *     CTA bar.
 *   - 240px desktop / 180px mobile cover with gradient overlay.
 *   - Designer/year/players/duration/complexity/rating meta line.
 *   - WCAG 2.1 AA: focus-visible rings + sufficient text contrast over the
 *     gradient (text-white over rgba(0,0,0,.6) overlay yields > 7:1).
 *
 * AC: T A M V
 */

'use client';

import type { ReactElement } from 'react';

import clsx from 'clsx';

export type GameDetailHeroVariant = 'own' | 'community';

export interface GameDetailHeroLabels {
  readonly back: string;
  readonly backAriaLabel: string;
  readonly ownedBadge: string;
  readonly communityBadge: string;
  readonly ctaPlay: string;
  readonly ctaEdit: string;
  readonly ctaShare: string;
  readonly ctaShareAriaLabel: string;
  readonly ctaAddToLibrary: string;
  readonly ctaSimilar: string;
  readonly favoriteAriaLabel: string;
}

export interface GameDetailHeroMeta {
  readonly designer: string | null;
  readonly year: number | null;
  readonly players: string | null;
  readonly duration: string | null;
  readonly complexity: string | null;
  readonly rating: string | null;
}

export interface GameDetailHeroProps {
  readonly title: string;
  readonly imageUrl: string | null;
  readonly variant: GameDetailHeroVariant;
  readonly meta: GameDetailHeroMeta;
  readonly isFavorite: boolean;
  readonly labels: GameDetailHeroLabels;
  readonly onBack?: () => void;
  readonly onPlay?: () => void;
  readonly onEdit?: () => void;
  readonly onShare?: () => void;
  readonly onAddToLibrary?: () => void;
  readonly onSimilar?: () => void;
  readonly className?: string;
}

export function GameDetailHero(props: GameDetailHeroProps): ReactElement {
  const {
    title,
    imageUrl,
    variant,
    meta,
    isFavorite,
    labels,
    onBack,
    onPlay,
    onEdit,
    onShare,
    onAddToLibrary,
    onSimilar,
    className,
  } = props;

  // Compose meta line from non-null parts.
  const metaParts = [
    meta.designer,
    meta.year != null ? String(meta.year) : null,
    meta.players,
    meta.duration,
    meta.complexity,
    meta.rating,
  ].filter((v): v is string => v != null && v !== '');

  return (
    <section
      data-slot="game-detail-hero"
      data-variant={variant}
      aria-label={title}
      className={clsx('relative bg-background', className)}
    >
      {/* Cover image */}
      <div
        className="relative h-[180px] w-full overflow-hidden bg-gradient-to-br from-amber-500/30 via-rose-500/30 to-violet-500/30 sm:h-[240px]"
        data-slot="game-detail-hero-cover"
      >
        {imageUrl ? (
          /* eslint-disable-next-line @next/next/no-img-element */
          <img
            src={imageUrl}
            alt=""
            aria-hidden="true"
            className="h-full w-full object-cover"
            data-dynamic
          />
        ) : (
          <div
            aria-hidden="true"
            className="flex h-full w-full items-center justify-center text-[90px] sm:text-[130px]"
            style={{ filter: 'drop-shadow(0 6px 18px rgba(0,0,0,.4))', opacity: 0.92 }}
          >
            🎲
          </div>
        )}

        {/* Gradient overlay */}
        <div
          aria-hidden="true"
          className="absolute inset-0"
          style={{ background: 'linear-gradient(180deg, transparent 0%, rgba(0,0,0,.6) 100%)' }}
        />

        {/* Title overlay */}
        {/* eslint-disable-next-line local/no-hardcoded-color-utility -- text-white justified: overlay background is dark (rgba(0,0,0,.6)), inherited by children */}
        <div className="absolute inset-x-0 bottom-0 px-4 pb-4 text-white sm:px-8 sm:pb-5">
          <div className="mb-2 flex flex-wrap items-center gap-2">
            {/* eslint-disable-next-line local/no-hardcoded-color-utility -- text-white justified: bg-card/20 declared on same className */}
            <span className="inline-flex items-center gap-1.5 rounded-full bg-card/20 px-2.5 py-1 font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-white backdrop-blur-md">
              <span aria-hidden="true">🎲</span>
              <span>Game</span>
            </span>
            {/* eslint-disable-next-line local/no-hardcoded-color-utility -- text-white justified: bg-card/20 declared on same className */}
            <span className="rounded-full bg-card/20 px-2.5 py-1 font-mono text-[9px] font-extrabold uppercase tracking-[0.08em] text-white backdrop-blur-md">
              {variant === 'own' ? labels.ownedBadge : labels.communityBadge}
            </span>
            {isFavorite ? (
              <span
                aria-label={labels.favoriteAriaLabel}
                role="img"
                className="rounded-full bg-card/20 px-2 py-1 text-xs backdrop-blur-md"
              >
                ★
              </span>
            ) : null}
          </div>
          <h1
            // eslint-disable-next-line local/no-hardcoded-color-utility -- text-white justified: overlay background is dark (rgba(0,0,0,.6))
            className="mb-1.5 font-display text-[28px] font-extrabold leading-[1.05] tracking-[-0.02em] text-white sm:text-[40px]"
            style={{ textShadow: '0 2px 12px rgba(0,0,0,.3)' }}
          >
            {title}
          </h1>
          {metaParts.length > 0 ? (
            <div
              data-slot="game-detail-hero-meta"
              // eslint-disable-next-line local/no-hardcoded-color-utility -- text-white/95 justified: overlay background is dark (rgba(0,0,0,.6))
              className="flex flex-wrap items-center gap-x-2.5 gap-y-1 font-mono text-[11px] font-semibold text-white/95 sm:text-[12px]"
            >
              {metaParts.map((part, idx) => (
                <span key={idx} className="inline-flex items-center gap-2">
                  {idx > 0 ? <span aria-hidden="true">·</span> : null}
                  <span>{part}</span>
                </span>
              ))}
            </div>
          ) : null}
        </div>
      </div>

      {/* Action bar */}
      <div
        data-slot="game-detail-hero-actions"
        className="flex flex-wrap items-center gap-2 border-b border-border bg-background px-4 py-3 sm:flex-nowrap sm:px-8 sm:py-3.5"
      >
        {onBack ? (
          <button
            type="button"
            onClick={onBack}
            aria-label={labels.backAriaLabel}
            data-slot="game-detail-back"
            className="inline-flex items-center gap-1.5 rounded-md border border-border bg-transparent px-3 py-2 font-display text-[13px] font-bold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <span aria-hidden="true">←</span>
            <span className="hidden sm:inline">{labels.back}</span>
          </button>
        ) : null}

        {variant === 'own' ? (
          <>
            {onPlay ? (
              <button
                type="button"
                onClick={onPlay}
                data-slot="game-detail-cta-play"
                className="inline-flex items-center gap-1.5 rounded-md border-none bg-emerald-700 px-4 py-2.5 font-display text-[13px] font-extrabold text-white shadow-md transition-shadow hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <span aria-hidden="true">🎯</span>
                <span>{labels.ctaPlay}</span>
              </button>
            ) : null}
            {onEdit ? (
              <button
                type="button"
                onClick={onEdit}
                data-slot="game-detail-cta-edit"
                className="inline-flex items-center gap-1.5 rounded-md border border-border bg-transparent px-3 py-2.5 font-display text-[13px] font-bold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <span aria-hidden="true">✎</span>
                <span>{labels.ctaEdit}</span>
              </button>
            ) : null}
          </>
        ) : (
          <>
            {onAddToLibrary ? (
              <button
                type="button"
                onClick={onAddToLibrary}
                data-slot="game-detail-cta-add"
                className="inline-flex items-center gap-1.5 rounded-md border-none bg-amber-700 px-4 py-2.5 font-display text-[13px] font-extrabold text-white shadow-md transition-shadow hover:shadow-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                {labels.ctaAddToLibrary}
              </button>
            ) : null}
            {onSimilar ? (
              <button
                type="button"
                onClick={onSimilar}
                data-slot="game-detail-cta-similar"
                className="inline-flex items-center gap-1.5 rounded-md border border-border bg-transparent px-3 py-2.5 font-display text-[13px] font-bold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <span aria-hidden="true">👀</span>
                <span>{labels.ctaSimilar}</span>
              </button>
            ) : null}
          </>
        )}

        {onShare ? (
          <button
            type="button"
            onClick={onShare}
            aria-label={labels.ctaShareAriaLabel}
            data-slot="game-detail-cta-share"
            className="ml-auto inline-flex items-center gap-1 rounded-md border border-border bg-transparent px-3 py-2.5 font-display text-[13px] font-bold text-foreground transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <span aria-hidden="true">↗</span>
            <span className="hidden sm:inline">{labels.ctaShare}</span>
          </button>
        ) : null}
      </div>
    </section>
  );
}
