/* eslint-disable local/no-hardcoded-color-utility -- text-white on cover overlay (gradient 0→60% black + image). Same exemption pattern as game-hero-section.tsx and AppTopBar BrandMark icon. Will be re-evaluated in DS-15 finalization audit. */

'use client';

import Image from 'next/image';

import { cn } from '@/lib/utils';

/**
 * GameHero — Hero v2 per /library/[gameId] (e futura /shared-games/[id]).
 *
 * Issue #2100 — Milestone 1 di Epic #2096 (game-detail sp3 rebuild).
 *
 * Pattern mockup `admin-mockups/design_files/sp4-game-detail.jsx GameHero`
 * (linee 166-300):
 *
 *   - Cover fixed-height 240px desktop / 180px compact
 *   - Background = image (BGG cover) o fallback gradient entity-game
 *   - Emoji giant centrato (130px / 90px compact) come fallback visuale quando
 *     l'immagine non carica + drop-shadow per leggibilità
 *   - Gradient overlay `linear-gradient(180deg, transparent 0%, rgba(0,0,0,.6) 100%)`
 *   - Title overlay bottom-anchor: badge row (entity + variant) + h1 Quicksand 4xl
 *     + meta row (designer · anno · giocatori · durata · complessità)
 *   - Variant-aware: `'own'` (in libreria) vs `'community'` (catalogo)
 *
 * Action bar (CTA "Gioca ora"/"Aggiungi a libreria") è deferred a Milestone
 * successiva (#2096 M2+) per mantenere M1 atomico.
 */

export type GameHeroVariant = 'own' | 'community';

export interface GameHeroMetaItem {
  readonly label: string;
}

export interface GameHeroProps {
  /** Game title rendered as h1. */
  readonly title: string;
  /** Optional cover image (BGG, custom upload, etc). */
  readonly imageUrl?: string;
  /**
   * Optional emoji shown as decorative glyph (large, drop-shadowed).
   * Fallback `🎲` when omitted.
   */
  readonly emoji?: string;
  /** Variant — `'own'` = in user library; `'community'` = public catalog. */
  readonly variant: GameHeroVariant;
  /**
   * Optional meta row items rendered as scannable identity strip
   * (designer · anno · durata · giocatori · complessità). Already built by
   * `GameDetailDesktop` for parity with previous MeepleCard hero.
   */
  readonly metadata?: ReadonlyArray<GameHeroMetaItem>;
  /** Compact mode for mobile layouts (180px height instead of 240px). */
  readonly compact?: boolean;
  /** Loading skeleton state. */
  readonly loading?: boolean;
  /** Error state. */
  readonly error?: boolean;
  /** Wrapper className override. */
  readonly className?: string;
}

const VARIANT_BADGE: Record<GameHeroVariant, { label: string; icon: string }> = {
  own: { label: 'In libreria', icon: '✓' },
  community: { label: 'Community', icon: '🌐' },
};

/**
 * Loading skeleton — shimmer cover + 2 placeholder lines.
 */
function GameHeroSkeleton({ compact }: { compact: boolean }) {
  return (
    <div data-slot="game-hero-skeleton" data-compact={compact} className="relative w-full bg-card">
      <div className={cn('w-full animate-pulse bg-muted', compact ? 'h-[180px]' : 'h-[240px]')} />
      <div className={cn('flex flex-col gap-2', compact ? 'p-4' : 'px-8 py-5')}>
        <div className="h-7 w-1/2 animate-pulse rounded bg-muted" />
        <div className="h-3.5 w-3/4 animate-pulse rounded bg-muted" />
      </div>
    </div>
  );
}

/**
 * Error state — minimal error surface preserving hero footprint.
 */
function GameHeroError({ compact }: { compact: boolean }) {
  return (
    <div
      role="alert"
      data-slot="game-hero-error"
      className={cn(
        'flex w-full items-center justify-center bg-destructive/10 text-sm text-destructive',
        compact ? 'h-[180px]' : 'h-[240px]'
      )}
    >
      Impossibile caricare il gioco.
    </div>
  );
}

export function GameHero({
  title,
  imageUrl,
  emoji = '🎲',
  variant,
  metadata,
  compact = false,
  loading = false,
  error = false,
  className,
}: GameHeroProps) {
  if (loading) return <GameHeroSkeleton compact={compact} />;
  if (error) return <GameHeroError compact={compact} />;

  const badge = VARIANT_BADGE[variant];

  return (
    <section
      data-slot="game-hero"
      data-testid="game-detail-hero-card"
      data-variant={variant}
      data-compact={compact}
      className={cn('relative w-full overflow-hidden', className)}
    >
      {/* Cover layer */}
      <div
        className={cn(
          'relative flex w-full items-center justify-center overflow-hidden',
          compact ? 'h-[180px]' : 'h-[240px]',
          // Fallback gradient when imageUrl missing/failed — entity-game tokens
          !imageUrl &&
            'bg-[linear-gradient(135deg,hsl(var(--c-game)/0.85),hsl(var(--c-event)/0.85))]'
        )}
      >
        {/* Cover image background (object-fit cover) */}
        {imageUrl && (
          <Image
            src={imageUrl}
            alt=""
            fill
            priority
            sizes="100vw"
            className="object-cover"
            aria-hidden="true"
          />
        )}

        {/* Decorative emoji glyph (visible only when no image — fallback proof) */}
        {!imageUrl && (
          <span
            aria-hidden="true"
            className={cn(
              'relative select-none opacity-90 drop-shadow-[0_6px_18px_rgba(0,0,0,0.4)]',
              compact ? 'text-[90px]' : 'text-[130px]'
            )}
          >
            {emoji}
          </span>
        )}

        {/* Gradient overlay 0 → 60% black (improves title contrast) */}
        <div
          aria-hidden="true"
          className="absolute inset-0 bg-[linear-gradient(180deg,transparent_0%,rgba(0,0,0,0.6)_100%)]"
        />

        {/* Title overlay bottom-anchor */}
        <div
          className={cn(
            'absolute inset-x-0 bottom-0 text-white',
            compact ? 'px-4 py-3.5' : 'px-8 py-5'
          )}
        >
          {/* Badge row: entity pill + variant pill */}
          <div className="mb-2 flex items-center gap-1.5">
            <span className="inline-flex items-center gap-1 rounded-full bg-white/15 px-2 py-0.5 font-[var(--font-jetbrains)] text-[9px] font-extrabold uppercase tracking-[0.08em] text-white backdrop-blur-md">
              <span aria-hidden="true">🎲</span>Game
            </span>
            <span
              data-slot="game-hero-variant-pill"
              className="inline-flex items-center gap-1 rounded-full bg-white/15 px-2 py-0.5 font-[var(--font-jetbrains)] text-[9px] font-extrabold uppercase tracking-[0.08em] text-white backdrop-blur-md"
            >
              <span aria-hidden="true">{badge.icon}</span>
              {badge.label}
            </span>
          </div>

          {/* h1 title — Quicksand 4xl, drop shadow per legibility on any cover */}
          <h1
            data-slot="game-hero-title"
            className={cn(
              'm-0 mb-1.5 font-quicksand font-extrabold tracking-tight text-white',
              '[text-shadow:0_2px_12px_rgba(0,0,0,0.3)]',
              compact ? 'text-2xl leading-tight' : 'text-[40px] leading-[1.05]'
            )}
          >
            {title}
          </h1>

          {/* Meta row — designer · anno · durata · giocatori · complessità */}
          {metadata && metadata.length > 0 && (
            <div
              data-slot="game-hero-meta"
              className={cn(
                'flex flex-wrap items-center gap-x-2.5 gap-y-1 font-[var(--font-jetbrains)] font-semibold text-white/90',
                compact ? 'text-[11px]' : 'text-xs'
              )}
            >
              {metadata.map((item, idx) => (
                <span key={`${item.label}-${idx}`} className="contents">
                  {idx > 0 && (
                    <span aria-hidden="true" className="text-white/60">
                      ·
                    </span>
                  )}
                  <span>{item.label}</span>
                </span>
              ))}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
