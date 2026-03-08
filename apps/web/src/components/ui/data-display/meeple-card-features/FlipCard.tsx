/**
 * FlipCard - 3D Flip Animation Feature for MeepleCard
 * Epic #3820 - MeepleCard System
 *
 * Wraps MeepleCard content with a 3D flip animation using Framer Motion.
 * Shows detailed game info on the back side (description, categories, mechanics, etc.)
 * Supports controlled (isFlipped/onFlip) and uncontrolled modes.
 *
 * Back content adapts to variant:
 * - compact: description (2 lines)
 * - list: description (3 lines) + designer
 * - grid: description (4 lines) + categories (3) + mechanics (3) + designer
 * - featured: description (6 lines) + categories/mechanics (5) + designer + publishers
 * - hero: full content without truncation
 */

'use client';

import { useState, useCallback, useRef, useEffect } from 'react';

import { motion } from 'framer-motion';
import { ExternalLink, RotateCcw, Tag, Cog, User, Paintbrush } from 'lucide-react';
import Link from 'next/link';

import { useMediaQuery } from '@/lib/hooks/useMediaQuery';
import { cn } from '@/lib/utils';

import type { MeepleCardVariant } from '../meeple-card';

// ============================================================================
// Types
// ============================================================================

export interface MeepleCardFlipData {
  description?: string;
  categories?: Array<{ id: string; name: string }>;
  mechanics?: Array<{ id: string; name: string }>;
  designers?: Array<{ id: string; name: string }>;
  publishers?: Array<{ id: string; name: string }>;
  complexityRating?: number | null;
  minAge?: number;
}

export interface FlipCardProps {
  /** Front content (the normal MeepleCard content) */
  children: React.ReactNode;
  /** Data to display on the back (used by default BackContent; optional when customBackContent is provided) */
  flipData?: MeepleCardFlipData;
  /** Custom back content (overrides default BackContent when provided) */
  customBackContent?: React.ReactNode;
  /** Card variant - determines how much back content to show */
  variant?: MeepleCardVariant;
  /** Controlled mode: external flip state */
  isFlipped?: boolean;
  /** Controlled mode: flip state change handler */
  onFlip?: (flipped: boolean) => void;
  /** Flip trigger mode: 'card' = click anywhere flips, 'button' = dedicated flip button */
  flipTrigger?: 'card' | 'button';
  /** Link to entity detail page (shown on back) */
  detailHref?: string;
  /** Entity color HSL string for v2 styled back (e.g. "25 95% 45%") */
  entityColor?: string;
  /** Entity type name for back header (e.g. "Game") */
  entityName?: string;
  /** Card title for back header */
  title?: string;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Variant configuration for back content
// ============================================================================

interface VariantConfig {
  descriptionLines: string;
  maxCategories: number;
  maxMechanics: number;
  showDesigners: boolean;
  showPublishers: boolean;
  showMinAge: boolean;
}

const variantConfigs: Record<MeepleCardVariant, VariantConfig> = {
  compact: {
    descriptionLines: 'line-clamp-2',
    maxCategories: 0,
    maxMechanics: 0,
    showDesigners: false,
    showPublishers: false,
    showMinAge: false,
  },
  list: {
    descriptionLines: 'line-clamp-3',
    maxCategories: 0,
    maxMechanics: 0,
    showDesigners: true,
    showPublishers: false,
    showMinAge: false,
  },
  grid: {
    descriptionLines: 'line-clamp-4',
    maxCategories: 3,
    maxMechanics: 3,
    showDesigners: true,
    showPublishers: false,
    showMinAge: false,
  },
  featured: {
    descriptionLines: 'line-clamp-6',
    maxCategories: 5,
    maxMechanics: 5,
    showDesigners: true,
    showPublishers: true,
    showMinAge: false,
  },
  hero: {
    descriptionLines: '',
    maxCategories: Infinity,
    maxMechanics: Infinity,
    showDesigners: true,
    showPublishers: true,
    showMinAge: true,
  },
};

// ============================================================================
// BackContent subcomponent
// ============================================================================

function BackContent({
  flipData,
  variant = 'grid',
  detailHref,
  entityColor = '25 95% 45%',
  title,
}: {
  flipData: MeepleCardFlipData;
  variant: MeepleCardVariant;
  detailHref?: string;
  entityColor?: string;
  title?: string;
}) {
  // eslint-disable-next-line security/detect-object-injection
  const config = variantConfigs[variant];
  const isCompact = variant === 'compact' || variant === 'list';

  return (
    <div className={cn('flex h-full flex-col overflow-hidden')}>
      {/* Entity-colored header — v2 (Issue #4607) */}
      {!isCompact && (
        <div
          className="relative overflow-hidden px-5 pb-3 pt-5"
          style={{ backgroundColor: `hsl(${entityColor})` }}
        >
          {/* Diagonal stripe pattern overlay */}
          <div
            className="pointer-events-none absolute inset-0 opacity-[0.12]"
            style={{
              backgroundImage:
                'repeating-linear-gradient(45deg, transparent, transparent 10px, currentColor 10px, currentColor 11px)',
            }}
            aria-hidden="true"
          />
          <h2 className="relative z-[1] font-quicksand text-lg font-bold text-white">
            {title || 'Informazioni'}
          </h2>
        </div>
      )}

      {/* Content area */}
      <div
        className={cn('flex flex-1 flex-col overflow-y-auto', isCompact ? 'p-3 pl-5' : 'px-5 py-4')}
      >
        {/* Compact header fallback */}
        {isCompact && (
          <div className="mb-2">
            <h2 className="font-quicksand text-sm font-bold text-card-foreground">
              {title || 'Informazioni'}
            </h2>
          </div>
        )}

        {/* Description */}
        {flipData.description && (
          <div className={isCompact ? 'mb-2' : 'mb-3'}>
            <h3
              className={cn(
                'mb-1 flex items-center gap-2 font-quicksand font-semibold uppercase tracking-wider text-muted-foreground',
                isCompact ? 'text-xs' : 'text-xs'
              )}
            >
              Descrizione
            </h3>
            <p
              className={cn(
                'font-nunito leading-relaxed text-card-foreground/80',
                isCompact ? 'text-xs' : 'text-sm',
                config.descriptionLines
              )}
            >
              {flipData.description}
            </p>
          </div>
        )}

        {/* Categories */}
        {config.maxCategories > 0 && flipData.categories && flipData.categories.length > 0 && (
          <div className={isCompact ? 'mb-2' : 'mb-3'}>
            <h3
              className={cn(
                'mb-1 flex items-center gap-2 font-quicksand font-semibold uppercase tracking-wider text-muted-foreground',
                isCompact ? 'text-xs' : 'text-xs'
              )}
            >
              <Tag className="h-3 w-3" />
              Categorie
            </h3>
            <div className="flex flex-wrap gap-1.5">
              {flipData.categories.slice(0, config.maxCategories).map(cat => (
                <span
                  key={cat.id}
                  className="rounded-md px-2 py-0.5 font-nunito text-xs font-medium"
                  style={{
                    backgroundColor: `hsla(${entityColor}, 0.1)`,
                    color: `hsl(${entityColor})`,
                  }}
                >
                  {cat.name}
                </span>
              ))}
            </div>
          </div>
        )}

        {/* Mechanics */}
        {config.maxMechanics > 0 && flipData.mechanics && flipData.mechanics.length > 0 && (
          <div className={isCompact ? 'mb-2' : 'mb-3'}>
            <h3
              className={cn(
                'mb-1 flex items-center gap-2 font-quicksand font-semibold uppercase tracking-wider text-muted-foreground',
                isCompact ? 'text-xs' : 'text-xs'
              )}
            >
              <Cog className="h-3 w-3" />
              Meccaniche
            </h3>
            <div className="flex flex-wrap gap-1.5">
              {flipData.mechanics.slice(0, config.maxMechanics).map(mech => (
                <span
                  key={mech.id}
                  className="rounded-md bg-muted px-2 py-0.5 font-nunito text-xs font-medium text-muted-foreground"
                >
                  {mech.name}
                </span>
              ))}
            </div>
          </div>
        )}

        {/* Designers */}
        {config.showDesigners && flipData.designers && flipData.designers.length > 0 && (
          <div className={isCompact ? 'mb-2' : 'mb-3'}>
            <h3
              className={cn(
                'mb-1 flex items-center gap-2 font-quicksand font-semibold uppercase tracking-wider text-muted-foreground',
                isCompact ? 'text-xs' : 'text-xs'
              )}
            >
              <User className="h-3 w-3" />
              Designer
            </h3>
            <p
              className={cn(
                'font-nunito text-card-foreground/80',
                isCompact ? 'text-xs' : 'text-sm'
              )}
            >
              {flipData.designers.map(d => d.name).join(', ')}
            </p>
          </div>
        )}

        {/* Publishers */}
        {config.showPublishers && flipData.publishers && flipData.publishers.length > 0 && (
          <div className={isCompact ? 'mb-2' : 'mb-3'}>
            <h3
              className={cn(
                'mb-1 flex items-center gap-2 font-quicksand font-semibold uppercase tracking-wider text-muted-foreground',
                isCompact ? 'text-xs' : 'text-xs'
              )}
            >
              <Paintbrush className="h-3 w-3" />
              Editori
            </h3>
            <p
              className={cn(
                'font-nunito text-card-foreground/80',
                isCompact ? 'text-xs' : 'text-sm'
              )}
            >
              {flipData.publishers.map(p => p.name).join(', ')}
            </p>
          </div>
        )}

        {/* Complexity + Min Age row */}
        {(flipData.complexityRating || flipData.minAge) && !isCompact && (
          <div className="mb-3 flex items-center gap-3">
            {flipData.complexityRating && (
              <span className="rounded-md bg-muted px-2 py-1 font-nunito text-xs text-muted-foreground">
                Peso: {flipData.complexityRating.toFixed(2)} / 5
              </span>
            )}
            {config.showMinAge && flipData.minAge && (
              <span className="rounded-md bg-muted px-2 py-1 font-nunito text-xs text-muted-foreground">
                Età: {flipData.minAge}+
              </span>
            )}
          </div>
        )}

        {/* Detail page link */}
        {detailHref && (
          <div className="mt-auto pt-2">
            <Link
              href={detailHref}
              className={cn(
                'inline-flex items-center gap-1.5 font-nunito font-medium',
                'transition-colors duration-200',
                isCompact ? 'text-xs' : 'text-sm'
              )}
              style={{
                color: `hsl(${entityColor})`,
              }}
              onClick={e => e.stopPropagation()}
            >
              <ExternalLink className={cn(isCompact ? 'h-3 w-3' : 'h-3.5 w-3.5')} />
              Vai alla pagina
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}

// ============================================================================
// Main FlipCard Component
// ============================================================================

export function FlipCard({
  children,
  flipData,
  customBackContent,
  variant = 'grid',
  isFlipped: controlledFlipped,
  onFlip,
  flipTrigger = 'card',
  detailHref,
  entityColor = '25 95% 45%',
  entityName: _entityName,
  title,
  className,
}: FlipCardProps) {
  // Internal state for uncontrolled mode
  const [internalFlipped, setInternalFlipped] = useState(false);
  const frontRef = useRef<HTMLDivElement>(null);
  const [containerHeight, setContainerHeight] = useState<number | undefined>(undefined);

  // Determine if controlled or uncontrolled
  const isControlled = controlledFlipped !== undefined;
  const flipped = isControlled ? controlledFlipped : internalFlipped;

  const handleFlip = useCallback(() => {
    const newState = !flipped;
    if (isControlled) {
      onFlip?.(newState);
    } else {
      setInternalFlipped(newState);
      onFlip?.(newState);
    }
  }, [flipped, isControlled, onFlip]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        handleFlip();
      }
    },
    [handleFlip]
  );

  // For list/compact variants, measure front height and apply as minHeight
  const isRowBased = variant === 'list' || variant === 'compact';
  useEffect(() => {
    if (isRowBased && frontRef.current) {
      const measure = () => {
        const height = frontRef.current?.offsetHeight;
        if (height) setContainerHeight(height);
      };
      measure();
      // Re-measure on resize
      const observer = new ResizeObserver(measure);
      observer.observe(frontRef.current);
      return () => observer.disconnect();
    }
  }, [isRowBased]);

  // Hero variant: use aspect-ratio based container
  const isHero = variant === 'hero';

  // Issue #4841: Responsive flip trigger
  // Touch devices: always show flip button (no card-level click)
  // Desktop: click anywhere on card to flip (no button)
  const isTouchDevice = useMediaQuery('(pointer: coarse)');
  const isCardMode = isTouchDevice ? false : flipTrigger === 'card';

  return (
    <div
      className={cn(isCardMode && 'cursor-pointer', isHero && 'w-full max-w-[420px]', className)}
      style={{
        perspective: '1200px',
      }}
      {...(isCardMode
        ? {
            role: 'button' as const,
            tabIndex: 0,
            'aria-label': `Premi Invio o clicca per ${flipped ? 'vedere il fronte' : 'vedere i dettagli sul retro'}`,
            onClick: handleFlip,
            onKeyDown: handleKeyDown,
          }
        : {})}
      data-testid="meeple-card-flip-container"
    >
      <motion.div
        className="relative w-full"
        style={{
          transformStyle: 'preserve-3d',
          ...(isHero ? { aspectRatio: '3 / 4' } : {}),
          ...(isRowBased && containerHeight ? { minHeight: containerHeight } : {}),
        }}
        animate={{ rotateY: flipped ? 180 : 0, scale: flipped ? 1.02 : 1 }}
        transition={{ duration: 0.7, ease: [0.4, 0, 0.2, 1] }}
      >
        {/* Front */}
        <div
          ref={frontRef}
          className={cn(
            'relative',
            isHero || isRowBased ? 'absolute inset-0' : 'relative',
            isHero && '[&>*]:h-full [&>*]:w-full'
          )}
          style={{
            backfaceVisibility: 'hidden',
            WebkitBackfaceVisibility: 'hidden',
          }}
          data-testid="meeple-card-front"
        >
          {children}
          {/* Flip button overlay (touch/button mode only) */}
          {!isCardMode && (
            <button
              className={cn(
                'absolute bottom-3 right-3 z-30',
                // Mobile: WCAG 44px touch target; Desktop: compact 32px
                'flex h-11 w-11 md:h-8 md:w-8 items-center justify-center',
                'rounded-full bg-white/70 backdrop-blur-sm',
                'border border-border/30',
                'text-muted-foreground hover:text-foreground',
                'shadow-sm hover:shadow-md',
                'transition-all duration-200 hover:scale-110',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
              )}
              onClick={e => {
                e.stopPropagation();
                handleFlip();
              }}
              aria-label={flipped ? 'Mostra fronte carta' : 'Mostra dettagli carta'}
              data-testid="meeple-card-flip-button"
            >
              <RotateCcw className="h-4 w-4" />
            </button>
          )}
        </div>

        {/* Back */}
        <div
          className={cn(
            'absolute inset-0 overflow-hidden',
            isRowBased ? 'rounded-xl' : 'rounded-2xl',
            'border bg-card',
            'flex flex-col'
          )}
          style={{
            backfaceVisibility: 'hidden',
            WebkitBackfaceVisibility: 'hidden',
            transform: 'rotateY(180deg)',
            boxShadow: 'var(--shadow-warm-lg)',
            borderColor: `hsla(${entityColor}, 0.15)`,
          }}
          data-testid="meeple-card-back"
          {...(isCardMode ? { onClick: handleFlip } : {})}
        >
          {customBackContent ??
            (flipData ? (
              <BackContent
                flipData={flipData}
                variant={variant}
                detailHref={detailHref}
                entityColor={entityColor}
                title={title}
              />
            ) : null)}
          {/* Flip-back button on back face (touch/button mode only) */}
          {!isCardMode && (
            <button
              className={cn(
                'absolute bottom-3 right-3 z-30',
                // Mobile: WCAG 44px touch target; Desktop: compact 32px
                'flex h-11 w-11 md:h-8 md:w-8 items-center justify-center',
                'rounded-full bg-white/70 backdrop-blur-sm',
                'border border-border/30',
                'text-muted-foreground hover:text-foreground',
                'shadow-sm hover:shadow-md',
                'transition-all duration-200 hover:scale-110',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
              )}
              onClick={e => {
                e.stopPropagation();
                handleFlip();
              }}
              aria-label="Torna al fronte della carta"
              data-testid="meeple-card-flip-back-button"
            >
              <RotateCcw className="h-4 w-4" />
            </button>
          )}
        </div>
      </motion.div>

      {/* Flip hint (card mode, non-row variants only) */}
      {isCardMode && !isRowBased && (
        <p className="mt-3 text-center font-nunito text-sm text-[#9C958A]">
          Clicca per girare la carta
        </p>
      )}
    </div>
  );
}
