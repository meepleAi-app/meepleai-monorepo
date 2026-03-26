/**
 * ShelfCard Component
 *
 * A 140px wide vetrina card for the library browse experience.
 * Features MtG-inspired overlay slots (mechanicIcon, stateLabel), mana pips,
 * cover image/gradient, and optional in-library badge or add button.
 *
 * Distinct from MeepleCard — optimised for horizontal shelf layouts.
 */

'use client';

import React from 'react';

import { Plus, Check } from 'lucide-react';

import { entityColors } from '@/components/ui/data-display/meeple-card-styles';
import { coverOverlayStyles } from '@/components/ui/data-display/meeple-card-styles';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type ManaPipType = 'agent' | 'kb' | 'session' | 'chatSession';

export interface ManaPip {
  type: ManaPipType;
  active: boolean;
}

export interface ShelfCardProps {
  /** Primary title (game name, entity name) */
  title: string;
  /** Secondary line (publisher, category, etc.) */
  subtitle: string;
  /** Emoji or URL-safe single character shown in cover when no imageUrl */
  coverIcon?: string;
  /** CSS gradient string used as cover background when no imageUrl */
  coverGradient?: string;
  /** Full cover image URL — takes priority over coverGradient / coverIcon */
  imageUrl?: string;
  /** Mana pip indicators (agent, kb, session, chatSession) */
  manaPips?: ManaPip[];
  /** Whether this item is already in the user's library */
  inLibrary?: boolean;
  /** Called when the user clicks "Aggiungi" */
  onAdd?: () => void;
  /** Called when the card body is clicked */
  onClick?: () => void;
  /** MtG overlay: bottom-left slot — classification / mechanic icon */
  mechanicIcon?: React.ReactNode;
  /** MtG overlay: bottom-right slot — semantic state label */
  stateLabel?: { text: string; variant: 'success' | 'warning' | 'error' | 'info' };
  /** Additional CSS classes for the root element */
  className?: string;
  /** Optional test ID for the root element */
  'data-testid'?: string;
}

// ============================================================================
// Constants
// ============================================================================

/**
 * Map ManaPipType → HSL value string from the shared entityColors palette.
 * entityColors values are { hsl: string; name: string } objects.
 */
const PIP_COLORS: Record<ManaPipType, string> = {
  agent: entityColors.agent.hsl,
  kb: entityColors.kb.hsl,
  session: entityColors.session.hsl,
  chatSession: entityColors.chatSession.hsl,
};

const PIP_LABELS: Record<ManaPipType, string> = {
  agent: 'Agent',
  kb: 'KB',
  session: 'Session',
  chatSession: 'Chat',
};

// ============================================================================
// Component
// ============================================================================

/**
 * ShelfCard — 140px vetrina card for horizontal library shelves.
 *
 * @example
 * ```tsx
 * <ShelfCard
 *   title="Catan"
 *   subtitle="Klaus Teuber"
 *   imageUrl="/catan.jpg"
 *   manaPips={[
 *     { type: 'kb', active: true },
 *     { type: 'agent', active: false },
 *   ]}
 *   inLibrary
 * />
 * ```
 */
export function ShelfCard({
  title,
  subtitle,
  coverIcon,
  coverGradient,
  imageUrl,
  manaPips,
  inLibrary,
  onAdd,
  onClick,
  mechanicIcon,
  stateLabel,
  className,
  'data-testid': dataTestId,
}: ShelfCardProps) {
  // ------------------------------------------------------------------
  // Cover background style
  // ------------------------------------------------------------------
  const coverStyle: React.CSSProperties = imageUrl
    ? { backgroundImage: `url(${imageUrl})`, backgroundSize: 'cover', backgroundPosition: 'center' }
    : coverGradient
      ? { background: coverGradient }
      : { background: 'linear-gradient(135deg, hsl(220 15% 20%), hsl(220 15% 30%))' };

  // ------------------------------------------------------------------
  // Render
  // ------------------------------------------------------------------
  return (
    <div
      className={cn(
        'flex flex-col rounded-xl overflow-hidden cursor-pointer',
        'w-[140px] flex-shrink-0',
        'bg-card border border-border/50',
        'transition-all duration-200 ease-out',
        'hover:-translate-y-1 hover:shadow-lg',
        '[contain:layout_paint]',
        className
      )}
      onClick={onClick}
      data-testid={dataTestId ?? 'shelf-card'}
      role={onClick ? 'button' : undefined}
      tabIndex={onClick ? 0 : undefined}
      onKeyDown={
        onClick
          ? e => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onClick();
              }
            }
          : undefined
      }
    >
      {/* ------------------------------------------------------------------ */}
      {/* Cover area (100px height)                                           */}
      {/* ------------------------------------------------------------------ */}
      <div className="relative h-[100px] overflow-hidden" style={coverStyle}>
        {/* Fallback cover icon when no image */}
        {!imageUrl && coverIcon && (
          <div className="absolute inset-0 flex items-center justify-center">
            <span className="text-3xl select-none" aria-hidden="true">
              {coverIcon}
            </span>
          </div>
        )}

        {/* MtG overlay bar: mechanicIcon (bottom-left) + stateLabel (bottom-right) */}
        {(mechanicIcon || stateLabel) && (
          <div className={coverOverlayStyles.container}>
            {/* Bottom-left: mechanic classification icon */}
            <div>
              {mechanicIcon && (
                <span className={coverOverlayStyles.subtypeIcon} data-testid="mechanic-icon">
                  {mechanicIcon}
                </span>
              )}
            </div>

            {/* Bottom-right: semantic state label */}
            <div>
              {stateLabel && (
                <span
                  className={cn(
                    coverOverlayStyles.stateLabel.base,
                    coverOverlayStyles.stateLabel[stateLabel.variant]
                  )}
                  data-testid="state-label"
                >
                  {stateLabel.text}
                </span>
              )}
            </div>
          </div>
        )}
      </div>

      {/* ------------------------------------------------------------------ */}
      {/* Info area                                                           */}
      {/* ------------------------------------------------------------------ */}
      <div className="flex flex-col gap-1 px-2 py-1.5 flex-1">
        {/* Title */}
        <p
          className="text-xs font-semibold text-foreground leading-tight line-clamp-2"
          title={title}
        >
          {title}
        </p>

        {/* Subtitle */}
        <p
          className="text-[10px] text-muted-foreground leading-tight line-clamp-1"
          title={subtitle}
        >
          {subtitle}
        </p>

        {/* Mana pips */}
        {manaPips && manaPips.length > 0 && (
          <div className="flex gap-1 flex-wrap mt-0.5" data-testid="mana-pips">
            {manaPips.map(pip => (
              <span
                key={pip.type}
                className={cn(
                  'inline-flex items-center justify-center',
                  'w-4 h-4 rounded-full text-[8px] font-bold text-white',
                  'transition-opacity duration-150',
                  pip.active ? 'opacity-100' : 'opacity-30'
                )}
                style={{ backgroundColor: `hsl(${PIP_COLORS[pip.type]})` }}
                title={PIP_LABELS[pip.type]}
                aria-label={`${PIP_LABELS[pip.type]}${pip.active ? ' (attivo)' : ' (inattivo)'}`}
                data-testid={`mana-pip-${pip.type}`}
              >
                {pip.type === 'agent'
                  ? 'A'
                  : pip.type === 'kb'
                    ? 'K'
                    : pip.type === 'session'
                      ? 'S'
                      : 'C'}
              </span>
            ))}
          </div>
        )}

        {/* Library status: badge or add button */}
        <div className="mt-auto pt-1">
          {inLibrary ? (
            <span
              className="inline-flex items-center gap-0.5 text-[9px] font-medium text-emerald-400"
              data-testid="in-library-badge"
            >
              <Check className="w-2.5 h-2.5" />
              In libreria
            </span>
          ) : onAdd ? (
            <button
              type="button"
              className={cn(
                'w-full flex items-center justify-center gap-1',
                'text-[9px] font-medium text-primary',
                'rounded px-1.5 py-0.5',
                'border border-primary/30 hover:border-primary/60 hover:bg-primary/10',
                'transition-colors duration-150'
              )}
              onClick={e => {
                e.stopPropagation();
                onAdd();
              }}
              data-testid="add-button"
            >
              <Plus className="w-2.5 h-2.5" />
              Aggiungi
            </button>
          ) : null}
        </div>
      </div>
    </div>
  );
}

export default ShelfCard;
