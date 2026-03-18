'use client';

import React from 'react';

import { Crown, Loader2 } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { cn } from '@/lib/utils';

import type { Participant } from './types';

interface MeepleParticipantCardProps {
  participant: Participant;
  variant?: 'compact' | 'full';
}

/**
 * MeepleParticipantCard - Session scoreboard player card built on MeepleCard.
 *
 * Maps ParticipantCard semantics to MeepleCard entity="player":
 * - compact variant  -> MeepleCard variant="compact"
 * - full variant     -> MeepleCard variant="grid"
 * - rank emoji (top 3) -> badge prop
 * - owner crown      -> tags prop
 * - score            -> metadata array
 * - typing indicator -> subtitle
 * - avatar gradient  -> customColor (HSL extracted from hex)
 */
export function MeepleParticipantCard({
  participant,
  variant = 'full',
}: MeepleParticipantCardProps) {
  const rankEmojis: Record<number, string> = {
    1: '\u{1F947}',
    2: '\u{1F948}',
    3: '\u{1F949}',
  };

  // Build badge from rank
  const badge =
    participant.rank && participant.rank <= 3
      ? rankEmojis[participant.rank]
      : participant.rank
        ? `#${participant.rank}`
        : undefined;

  // Build subtitle from typing state
  const subtitle = participant.isTyping ? 'Sta scrivendo...' : undefined;

  // Build metadata with score
  const metadata = [
    {
      label: `${participant.totalScore.toLocaleString()} pts`,
    },
  ];

  // Build tags for owner crown
  const tags: Array<{ label: string; color: string; icon: string }> = [];
  if (participant.isOwner) {
    tags.push({ label: 'Owner', color: '#f59e0b', icon: 'crown' });
  }

  // Convert hex avatar color to HSL for customColor
  const customColor = hexToHsl(participant.avatarColor);

  const meepleVariant = variant === 'compact' ? 'compact' : 'grid';

  return (
    <div className="relative">
      <MeepleCard
        entity="player"
        variant={meepleVariant}
        title={participant.displayName}
        subtitle={subtitle}
        badge={badge}
        metadata={metadata}
        customColor={customColor}
        className={cn(
          // Current user highlight
          participant.isCurrentUser && 'ring-2 ring-amber-500/50 shadow-md shadow-amber-900/10',
          // Typing pulse effect
          participant.isTyping && 'animate-pulse'
        )}
        data-testid={`participant-card-${participant.id}`}
      />

      {/* Owner crown overlay (positioned on the card avatar area) */}
      {participant.isOwner && (
        <div
          className={cn(
            'absolute flex items-center justify-center rounded-full bg-gradient-to-br from-amber-400 to-amber-600 shadow-lg z-10',
            meepleVariant === 'compact' ? 'top-1 left-7 h-4 w-4' : 'top-3 left-12 h-5 w-5'
          )}
        >
          <Crown
            className={cn(
              'text-white drop-shadow',
              meepleVariant === 'compact' ? 'h-2.5 w-2.5' : 'h-3 w-3'
            )}
          />
        </div>
      )}

      {/* Typing indicator for compact variant (inline spinner) */}
      {participant.isTyping && meepleVariant === 'compact' && (
        <div className="absolute top-1/2 right-3 -translate-y-1/2">
          <Loader2 className="h-3.5 w-3.5 animate-spin text-amber-600 dark:text-amber-400" />
        </div>
      )}
    </div>
  );
}

/**
 * Convert a hex color string to HSL string format for MeepleCard customColor.
 * Returns format: "H S% L%" (e.g., "262 83% 58%").
 */
function hexToHsl(hex: string): string {
  // Strip # if present
  const clean = hex.replace(/^#/, '');

  // Parse RGB
  let r: number, g: number, b: number;
  if (clean.length === 3) {
    r = parseInt(clean[0] + clean[0], 16) / 255;
    g = parseInt(clean[1] + clean[1], 16) / 255;
    b = parseInt(clean[2] + clean[2], 16) / 255;
  } else {
    r = parseInt(clean.substring(0, 2), 16) / 255;
    g = parseInt(clean.substring(2, 4), 16) / 255;
    b = parseInt(clean.substring(4, 6), 16) / 255;
  }

  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const l = (max + min) / 2;

  if (max === min) {
    return `0 0% ${Math.round(l * 100)}%`;
  }

  const d = max - min;
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

  let h: number;
  if (max === r) {
    h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
  } else if (max === g) {
    h = ((b - r) / d + 2) / 6;
  } else {
    h = ((r - g) / d + 4) / 6;
  }

  return `${Math.round(h * 360)} ${Math.round(s * 100)}% ${Math.round(l * 100)}%`;
}

export type { MeepleParticipantCardProps };
