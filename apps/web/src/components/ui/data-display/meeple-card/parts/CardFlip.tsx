'use client';

/**
 * CardFlip - FlipCard wrapper with back-content rendering
 *
 * Wraps card content with the existing FlipCard component when flippable,
 * handling session-specific and game-specific back content.
 *
 * @module components/ui/data-display/meeple-card/parts/CardFlip
 */

import React from 'react';

import { FlipCard } from '../../meeple-card-features/FlipCard';
import { GameBackContent } from '../../meeple-card-features/GameBackContent';
import { SessionBackContent } from '../../meeple-card-features/SessionBackContent';
import { entityColors } from '../../meeple-card-styles';

import type {
  MeepleCardFlipData,
  MeepleCardVariant,
  MeepleEntityType,
  SessionStatus,
  SessionPlayerInfo,
  SessionBackData,
  GameBackData,
  GameBackActions,
} from '../types';

export interface CardFlipProps {
  /** Card content (front side) */
  children: React.ReactNode;
  /** Entity type */
  entity: MeepleEntityType;
  /** Card layout variant */
  variant?: MeepleCardVariant;
  /** Entity color (HSL) */
  entityColor: string;
  /** Card title */
  title: string;
  /** Card subtitle */
  subtitle?: string;
  /** Whether flip is enabled */
  flippable?: boolean;
  /** Flip data for default back content */
  flipData?: MeepleCardFlipData;
  /** Controlled flip state */
  isFlipped?: boolean;
  /** Flip state change handler */
  onFlip?: (flipped: boolean) => void;
  /** Flip trigger mode */
  flipTrigger?: 'card' | 'button';
  /** Link to detail page (shown on back) */
  detailHref?: string;
  /** Session status (for session back content) */
  sessionStatus?: SessionStatus;
  /** Session players (for session back content) */
  sessionPlayers?: SessionPlayerInfo[];
  /** Session back data */
  sessionBackData?: SessionBackData;
  /** Game back data */
  gameBackData?: GameBackData;
  /** Game back action handlers */
  gameBackActions?: GameBackActions;
}

/**
 * Wraps card content with FlipCard when flippable is true and
 * back content data is available. Otherwise renders children directly.
 */
export function CardFlipWrapper({
  children,
  entity,
  variant,
  entityColor,
  title,
  subtitle,
  flippable,
  flipData,
  isFlipped,
  onFlip,
  flipTrigger,
  detailHref,
  sessionStatus,
  sessionPlayers,
  sessionBackData,
  gameBackData,
  gameBackActions,
}: CardFlipProps) {
  if (!flippable || (!flipData && !sessionBackData && !gameBackData)) {
    return <>{children}</>;
  }

  // Session entities use custom back content
  const sessionBack =
    entity === 'session' && sessionStatus && sessionPlayers && sessionBackData ? (
      <SessionBackContent
        status={sessionStatus}
        players={sessionPlayers}
        backData={sessionBackData}
        entityColor={entityColor}
        title={title}
        detailHref={detailHref}
      />
    ) : undefined;

  // Game entities use GameBackContent
  const gameBack =
    entity === 'game' && gameBackData ? (
      <GameBackContent
        data={gameBackData}
        actions={gameBackActions}
        entityColor={entityColor}
        title={title}
        subtitle={subtitle}
        detailHref={detailHref}
      />
    ) : undefined;

  return (
    <FlipCard
      flipData={flipData}
      customBackContent={sessionBack ?? gameBack}
      variant={variant}
      isFlipped={isFlipped}
      onFlip={onFlip}
      flipTrigger={flipTrigger}
      detailHref={detailHref}
      entityColor={entityColor}
      // eslint-disable-next-line security/detect-object-injection
      entityName={entityColors[entity].name}
      title={title}
    >
      {children}
    </FlipCard>
  );
}
