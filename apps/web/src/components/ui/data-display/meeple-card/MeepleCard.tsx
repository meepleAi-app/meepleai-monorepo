'use client';

/**
 * MeepleCard - Universal Card Component Router
 *
 * Routes to the correct variant renderer based on the `variant` prop,
 * and wraps with FlipCard / HoverPreview when those features are enabled.
 *
 * @module components/ui/data-display/meeple-card/MeepleCard
 * @see Issue #3326 - Core MeepleCard Component Implementation
 */

import React from 'react';

import { FlipCard } from '../meeple-card-features/FlipCard';
import { GameBackContent } from '../meeple-card-features/GameBackContent';
import { HoverPreview } from '../meeple-card-features/HoverPreview';
import { SessionBackContent } from '../meeple-card-features/SessionBackContent';
import { entityColors } from '../meeple-card-styles';
import {
  MeepleCardGrid,
  MeepleCardList,
  MeepleCardCompact,
  MeepleCardFeatured,
  MeepleCardHero,
  MeepleCardExpanded,
} from './variants';

import type { MeepleCardProps } from './types';

/**
 * MeepleCard - Universal card component for MeepleAI entities
 *
 * Renders a polymorphic card supporting games, players, collections, events,
 * and custom entity types with multiple layout variants.
 *
 * Performance: React.memo wrapper prevents unnecessary re-renders in grid/list views.
 */
export const MeepleCard = React.memo(function MeepleCard(props: MeepleCardProps) {
  const {
    variant = 'grid',
    loading = false,
    // Flip feature props
    flippable,
    flipData,
    isFlipped,
    onFlip,
    flipTrigger,
    detailHref,
    // Hover preview props
    showPreview,
    previewData,
    onFetchPreview,
    // Common props needed for wrapping
    id,
    entity,
    customColor,
    title,
    subtitle,
    // Session back content
    sessionStatus,
    sessionPlayers,
    sessionBackData,
    // Game back content
    gameBackData,
    gameBackActions,
  } = props;

  // eslint-disable-next-line security/detect-object-injection
  const color = customColor || entityColors[entity].hsl;

  // Route to the correct variant renderer
  let cardContent: React.ReactElement;
  switch (variant) {
    case 'list':
      cardContent = <MeepleCardList {...props} />;
      break;
    case 'compact':
      cardContent = <MeepleCardCompact {...props} />;
      break;
    case 'featured':
      cardContent = <MeepleCardFeatured {...props} />;
      break;
    case 'hero':
      cardContent = <MeepleCardHero {...props} />;
      break;
    case 'expanded':
      cardContent = <MeepleCardExpanded {...props} />;
      break;
    case 'grid':
    default:
      cardContent = <MeepleCardGrid {...props} />;
      break;
  }

  // Feature: Wrap with FlipCard if enabled (takes priority over HoverPreview)
  // Session entities can flip with sessionBackData even without flipData
  // Game entities can flip with gameBackData even without flipData
  if (flippable && (flipData || sessionBackData || gameBackData)) {
    // Session entities use custom back content (Issue #4752)
    const sessionBack =
      entity === 'session' && sessionStatus && sessionPlayers && sessionBackData ? (
        <SessionBackContent
          status={sessionStatus}
          players={sessionPlayers}
          backData={sessionBackData}
          entityColor={color}
          title={title}
          detailHref={detailHref}
        />
      ) : undefined;

    // Game entities use GameBackContent for stats + KB preview + actions
    const gameBack =
      entity === 'game' && gameBackData ? (
        <GameBackContent
          data={gameBackData}
          actions={gameBackActions}
          entityColor={color}
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
        entityColor={color}
        // eslint-disable-next-line security/detect-object-injection
        entityName={entityColors[entity].name}
        title={title}
      >
        {cardContent}
      </FlipCard>
    );
  }

  // Feature: Wrap with HoverPreview if enabled
  if (showPreview && onFetchPreview && id) {
    return (
      <HoverPreview
        gameId={id}
        previewData={previewData}
        onFetchPreview={onFetchPreview}
        delay={500}
        disabled={loading}
      >
        {cardContent}
      </HoverPreview>
    );
  }

  return cardContent;
});
