/**
 * GameCard - Game Display Component
 * Issue #3286 - User Dashboard Layout System
 * Updated: Issue #4047 - Integrate MeepleCard system
 *
 * Features:
 * - Uses MeepleCard with entity="game"
 * - Quick actions on hover (Chat, Avvia Sessione, Condividi)
 * - Info button navigation
 * - Grid/List view modes
 * - Rating badge with color coding
 * - Favorite heart indicator
 * - Ownership status badge
 * - PDF and Active Chat badges
 * - Hover animations
 */

'use client';

import { memo } from 'react';

import { Bot, Gamepad2, Calendar, MapPin } from 'lucide-react';

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { useEntityActions } from '@/hooks/use-entity-actions';

import type { ViewMode } from './DashboardSection';

// ============================================================================
// Types
// ============================================================================

export interface GameData {
  id: string;
  name: string;
  imageUrl?: string;
  rating?: number;
  playCount?: number;
  lastPlayedAt?: Date;
  isFavorite?: boolean;
  ownershipStatus?: 'OWNED' | 'LENT_OUT';
  location?: string;
  hasPdf?: boolean;
  hasActiveChat?: boolean;
}

export interface GameCardProps {
  data: GameData;
  viewMode: ViewMode;
  onAskAI?: () => void;
  onClick?: () => void;
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

function formatLastPlayed(date?: Date): string {
  if (!date) return 'Mai giocato';

  const now = new Date();
  const diff = now.getTime() - date.getTime();
  const days = Math.floor(diff / (1000 * 60 * 60 * 24));

  if (days === 0) return 'Oggi';
  if (days === 1) return 'Ieri';
  if (days < 7) return `${days} giorni fa`;
  if (days < 30) return `${Math.floor(days / 7)} settimane fa`;
  if (days < 365) return `${Math.floor(days / 30)} mesi fa`;
  return `${Math.floor(days / 365)} anni fa`;
}

// ============================================================================
// GameCard Component
// ============================================================================

export const GameCard = memo(function GameCard({
  data,
  viewMode,
  onAskAI,
  onClick,
  className,
}: GameCardProps) {
  const { name, imageUrl, rating, playCount, lastPlayedAt, isFavorite, ownershipStatus, location, hasPdf, hasActiveChat } = data;

  // Issue #4047: Generate entity-specific quick actions
  const entityActions = useEntityActions({ entity: 'game', id: data.id });

  // Build quick actions with AI button if callback provided
  const quickActions = onAskAI
    ? [
        ...entityActions.quickActions,
        {
          icon: Bot,
          label: 'Chiedi all\'AI',
          onClick: onAskAI,
        },
      ]
    : entityActions.quickActions;

  // Build metadata
  const metadata: MeepleCardMetadata[] = [
    { icon: Gamepad2, value: `${playCount ?? 0} partite` },
    { icon: Calendar, value: formatLastPlayed(lastPlayedAt) },
  ];

  if (location) {
    metadata.push({ icon: MapPin, value: location });
  }

  // Build badges based on status
  const badges: string[] = [];
  if (ownershipStatus === 'LENT_OUT') badges.push('Prestato');
  if (hasPdf) badges.push('📄 PDF');
  if (hasActiveChat) badges.push('💬 Chat');
  if (isFavorite) badges.push('❤️ Preferito');

  const badge = badges.length > 0 ? badges.join(' · ') : undefined;

  return (
    <MeepleCard
      entity="game"
      variant={viewMode === 'grid' ? 'grid' : 'list'}
      title={name}
      imageUrl={imageUrl}
      rating={rating}
      ratingMax={10}
      metadata={metadata}
      badge={badge}
      onClick={onClick}
      className={className}
      // Issue #4047: Quick actions + Info button
      entityQuickActions={quickActions}
      showInfoButton
      infoHref={`/games/${data.id}`}
      infoTooltip="Vai al dettaglio"
      data-testid={`dashboard-game-card-${data.id}`}
    />
  );
});

export default GameCard;