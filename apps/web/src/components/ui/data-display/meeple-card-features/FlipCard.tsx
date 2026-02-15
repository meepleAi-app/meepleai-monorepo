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
  /** Data to display on the back */
  flipData: MeepleCardFlipData;
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
}: {
  flipData: MeepleCardFlipData;
  variant: MeepleCardVariant;
  detailHref?: string;
}) {
  // eslint-disable-next-line security/detect-object-injection
  const config = variantConfigs[variant];
  const isCompact = variant === 'compact' || variant === 'list';

  return (
    <div className={cn(
      'flex h-full flex-col overflow-y-auto',
      isCompact ? 'p-3 pl-5' : 'p-6 pl-8',
    )}>
      {/* Header */}
      <div className={cn('flex items-center justify-between', isCompact ? 'mb-2' : 'mb-4')}>
        <h2 className={cn(
          'font-quicksand font-bold text-[#2D2A26]',
          isCompact ? 'text-sm' : 'text-xl',
        )}>
          Informazioni
        </h2>
      </div>

      {/* Description */}
      {flipData.description && (
        <div className={isCompact ? 'mb-2' : 'mb-4'}>
          <h3 className={cn(
            'mb-1.5 flex items-center gap-2 font-quicksand font-semibold uppercase tracking-wider text-[#9C958A]',
            isCompact ? 'text-xs' : 'text-sm',
          )}>
            Descrizione
          </h3>
          <p className={cn(
            'font-nunito leading-relaxed text-[#6B665C]',
            isCompact ? 'text-xs' : 'text-sm',
            config.descriptionLines,
          )}>
            {flipData.description}
          </p>
        </div>
      )}

      {/* Categories */}
      {config.maxCategories > 0 && flipData.categories && flipData.categories.length > 0 && (
        <div className={isCompact ? 'mb-2' : 'mb-4'}>
          <h3 className={cn(
            'mb-1.5 flex items-center gap-2 font-quicksand font-semibold uppercase tracking-wider text-[#9C958A]',
            isCompact ? 'text-xs' : 'text-sm',
          )}>
            <Tag className="h-3.5 w-3.5" />
            Categorie
          </h3>
          <div className="flex flex-wrap gap-1.5">
            {flipData.categories.slice(0, config.maxCategories).map((cat) => (
              <span
                key={cat.id}
                className="rounded-lg bg-[hsla(262,83%,62%,0.1)] px-2.5 py-1 font-nunito text-xs font-medium text-[hsl(262,83%,62%)]"
              >
                {cat.name}
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Mechanics */}
      {config.maxMechanics > 0 && flipData.mechanics && flipData.mechanics.length > 0 && (
        <div className={isCompact ? 'mb-2' : 'mb-4'}>
          <h3 className={cn(
            'mb-1.5 flex items-center gap-2 font-quicksand font-semibold uppercase tracking-wider text-[#9C958A]',
            isCompact ? 'text-xs' : 'text-sm',
          )}>
            <Cog className="h-3.5 w-3.5" />
            Meccaniche
          </h3>
          <div className="flex flex-wrap gap-1.5">
            {flipData.mechanics.slice(0, config.maxMechanics).map((mech) => (
              <span
                key={mech.id}
                className="rounded-lg bg-[hsla(25,95%,38%,0.1)] px-2.5 py-1 font-nunito text-xs font-medium text-[hsl(25,95%,38%)]"
              >
                {mech.name}
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Designers */}
      {config.showDesigners && flipData.designers && flipData.designers.length > 0 && (
        <div className={isCompact ? 'mb-2' : 'mb-4'}>
          <h3 className={cn(
            'mb-1.5 flex items-center gap-2 font-quicksand font-semibold uppercase tracking-wider text-[#9C958A]',
            isCompact ? 'text-xs' : 'text-sm',
          )}>
            <User className="h-3.5 w-3.5" />
            Designer
          </h3>
          <p className={cn('font-nunito text-[#6B665C]', isCompact ? 'text-xs' : 'text-sm')}>
            {flipData.designers.map((d) => d.name).join(', ')}
          </p>
        </div>
      )}

      {/* Publishers */}
      {config.showPublishers && flipData.publishers && flipData.publishers.length > 0 && (
        <div className={isCompact ? 'mb-2' : 'mb-4'}>
          <h3 className={cn(
            'mb-1.5 flex items-center gap-2 font-quicksand font-semibold uppercase tracking-wider text-[#9C958A]',
            isCompact ? 'text-xs' : 'text-sm',
          )}>
            <Paintbrush className="h-3.5 w-3.5" />
            Editori
          </h3>
          <p className={cn('font-nunito text-[#6B665C]', isCompact ? 'text-xs' : 'text-sm')}>
            {flipData.publishers.map((p) => p.name).join(', ')}
          </p>
        </div>
      )}

      {/* Min Age */}
      {config.showMinAge && flipData.minAge && (
        <div className="mt-auto">
          <span className="rounded-lg bg-[rgba(45,42,38,0.04)] px-3 py-1.5 font-nunito text-sm text-[#6B665C]">
            Età minima: {flipData.minAge}+
          </span>
        </div>
      )}

      {/* Detail page link */}
      {detailHref && (
        <div className="mt-auto pt-3">
          <Link
            href={detailHref}
            className={cn(
              'inline-flex items-center gap-1.5 font-nunito font-medium',
              'text-[hsl(262,83%,62%)] hover:text-[hsl(262,83%,52%)]',
              'transition-colors duration-200',
              isCompact ? 'text-xs' : 'text-sm',
            )}
            onClick={(e) => e.stopPropagation()}
          >
            <ExternalLink className={cn(isCompact ? 'h-3 w-3' : 'h-3.5 w-3.5')} />
            Vai alla pagina
          </Link>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Main FlipCard Component
// ============================================================================

export function FlipCard({
  children,
  flipData,
  variant = 'grid',
  isFlipped: controlledFlipped,
  onFlip,
  flipTrigger = 'card',
  detailHref,
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
    [handleFlip],
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
  const isCardMode = flipTrigger === 'card';

  return (
    <div
      className={cn(
        isCardMode && 'cursor-pointer',
        isHero && 'w-full max-w-[420px]',
        className,
      )}
      style={{
        perspective: '2000px',
      }}
      {...(isCardMode ? {
        role: 'button' as const,
        tabIndex: 0,
        'aria-label': `Premi Invio o clicca per ${flipped ? 'vedere il fronte' : 'vedere i dettagli sul retro'}`,
        onClick: handleFlip,
        onKeyDown: handleKeyDown,
      } : {})}
      data-testid="meeple-card-flip-container"
    >
      <motion.div
        className="relative w-full"
        style={{
          transformStyle: 'preserve-3d',
          ...(isHero ? { aspectRatio: '3 / 4' } : {}),
          ...(isRowBased && containerHeight ? { minHeight: containerHeight } : {}),
        }}
        animate={{ rotateY: flipped ? 180 : 0 }}
        transition={{ duration: 0.8, ease: [0.4, 0, 0.2, 1] }}
      >
        {/* Front */}
        <div
          ref={frontRef}
          className={cn(
            'relative',
            isHero || isRowBased ? 'absolute inset-0' : 'relative',
            isHero && '[&>*]:h-full [&>*]:w-full',
          )}
          style={{
            backfaceVisibility: 'hidden',
            WebkitBackfaceVisibility: 'hidden',
          }}
          data-testid="meeple-card-front"
        >
          {children}
          {/* Flip button overlay (button mode only) */}
          {!isCardMode && (
            <button
              className={cn(
                'absolute bottom-3 right-3 z-30',
                'flex h-8 w-8 items-center justify-center',
                'rounded-full bg-white/70 backdrop-blur-sm',
                'border border-border/30',
                'text-muted-foreground hover:text-foreground',
                'shadow-sm hover:shadow-md',
                'transition-all duration-200 hover:scale-110',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              )}
              onClick={(e) => {
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
            isRowBased ? 'rounded-xl' : 'rounded-3xl',
            'border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9]',
            'flex flex-col',
          )}
          style={{
            backfaceVisibility: 'hidden',
            WebkitBackfaceVisibility: 'hidden',
            transform: 'rotateY(180deg)',
            boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)',
          }}
          data-testid="meeple-card-back"
        >
          {/* Purple left border indicator for back */}
          <div className="absolute bottom-0 left-0 top-0 w-[5px] rounded-l-3xl bg-gradient-to-b from-[hsl(262,83%,62%)] to-[hsl(262,83%,72%)]" />

          <BackContent
            flipData={flipData}
            variant={variant}
            detailHref={detailHref}
          />
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
