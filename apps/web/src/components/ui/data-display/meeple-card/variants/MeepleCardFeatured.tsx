'use client';

/**
 * MeepleCardFeatured - Featured variant renderer
 *
 * 400px width, 16:9 cover, action buttons, hover translateY(-6px).
 * Similar to grid but wider with more content space.
 *
 * @module components/ui/data-display/meeple-card/variants/MeepleCardFeatured
 */

import React, { useState } from 'react';

import { cn } from '@/lib/utils';

import { EntityLinkPreviewRow } from '../../entity-link/entity-link-preview-row';
import { ExtraMeepleCardDrawer } from '../../extra-meeple-card/ExtraMeepleCardDrawer';
import { AgentModelInfo } from '../../meeple-card-features/AgentModelInfo';
import { AgentStatsDisplay } from '../../meeple-card-features/AgentStatsDisplay';
import { AgentStatusBadge } from '../../meeple-card-features/AgentStatusBadge';
import { BulkSelectCheckbox } from '../../meeple-card-features/BulkSelectCheckbox';
import { CardAgentAction } from '../../meeple-card-features/CardAgentAction';
import { CardNavigationFooter } from '../../meeple-card-features/CardNavigationFooter';
import { ChatAgentInfo } from '../../meeple-card-features/ChatAgentInfo';
import { ChatGameContext } from '../../meeple-card-features/ChatGameContext';
import { ChatStatsDisplay } from '../../meeple-card-features/ChatStatsDisplay';
import { ChatStatusBadge } from '../../meeple-card-features/ChatStatusBadge';
import { ChatUnreadBadge } from '../../meeple-card-features/ChatUnreadBadge';
import { DocumentStatusBadge } from '../../meeple-card-features/DocumentStatusBadge';
import { SessionActionButtons } from '../../meeple-card-features/SessionActionButtons';
import { SessionScoreTable } from '../../meeple-card-features/SessionScoreTable';
import { SessionStatusBadge } from '../../meeple-card-features/SessionStatusBadge';
import { SessionTurnSequence } from '../../meeple-card-features/SessionTurnSequence';
import { SnapshotHistorySlider } from '../../meeple-card-features/SnapshotHistorySlider';
import { TimeTravelOverlay } from '../../meeple-card-features/TimeTravelOverlay';
import {
  EntityIndicator,
  MetadataChips,
  RatingDisplay,
  MeepleCardSkeleton,
} from '../../meeple-card-parts';
import {
  entityColors,
  DRAWER_ENTITY_TYPE_MAP,
  meepleCardVariants,
  contentVariants,
} from '../../meeple-card-styles';
import { useMobileInteraction } from '../hooks/useMobileInteraction';
import { CardActions } from '../parts/CardActions';
import { CardBadges } from '../parts/CardBadges';
import { CardCover } from '../parts/CardCover';
import { CardTagStrip } from '../parts/CardTagStrip';

import type { MeepleCardProps } from '../types';

export type MeepleCardFeaturedProps = MeepleCardProps;

/**
 * Featured variant: wider card with 16:9 cover, action buttons, full content.
 */
export const MeepleCardFeatured = React.memo(function MeepleCardFeatured(
  props: MeepleCardFeaturedProps
) {
  const {
    id,
    entity,
    title,
    subtitle,
    imageUrl,
    avatarUrl,
    metadata = [],
    actions = [],
    rating,
    ratingMax = 5,
    badge,
    customColor,
    onClick,
    loading = false,
    className,
    'data-testid': testId,
    showWishlist,
    isWishlisted,
    onWishlistToggle,
    quickActions,
    userRole,
    status,
    showStatusIcon,
    selectable,
    selected,
    onSelect,
    flippable,
    entityQuickActions,
    showInfoButton,
    entityId,
    infoHref,
    infoTooltip,
    tags,
    maxVisibleTags = 3,
    showTagStrip,
    agentStatus,
    agentModel,
    agentStats,
    capabilities,
    chatStatus,
    chatAgent,
    chatGame,
    chatStats,
    chatPreview,
    unreadCount,
    navigateTo,
    hasAgent,
    agentId,
    onCreateAgent,
    sessionStatus,
    sessionPlayers,
    sessionRoundScores,
    sessionTurn,
    sessionActions,
    isSessionHost,
    onPrevTurn,
    onNextTurn,
    sessionSnapshots,
    currentSnapshotIndex,
    onSnapshotSelect,
    isTimeTravelMode,
    onTimeTravelToggle,
    documentStatus,
    linkCount,
    firstLinkPreview,
    onLinksClick,
    kbCards,
  } = props;

  const variant = 'featured' as const;
  const coverSrc = entity === 'player' ? avatarUrl || imageUrl : imageUrl;
  // eslint-disable-next-line security/detect-object-injection
  const color = customColor || entityColors[entity].hsl;
  const hasQuickActions = !!(quickActions && quickActions.length > 0);
  const showWishlistBtn = !!showWishlist && !hasQuickActions;
  const showActionBtns = actions.length > 0;
  const isInteractive = !!onClick && !showActionBtns;
  // eslint-disable-next-line security/detect-object-injection
  const drawerEntityType = DRAWER_ENTITY_TYPE_MAP[entity];

  const [drawerOpen, setDrawerOpen] = useState(false);

  const hasMobileActions =
    hasQuickActions ||
    !!entityQuickActions ||
    showWishlistBtn ||
    !!(showInfoButton && (entityId || infoHref));

  const { isMobile, showMobileActions, setShowMobileActions, handleMobileClick, cardRef } =
    useMobileInteraction({
      hasMobileActions,
      flippable,
      onClick,
    });

  const handleDesktopClick = () => {
    if (flippable) return;
    if (onClick) onClick();
  };

  const handleCardClick = isMobile
    ? handleMobileClick
    : isInteractive
      ? handleDesktopClick
      : undefined;

  if (loading) {
    return <MeepleCardSkeleton variant={variant} />;
  }

  const worstKbStatus =
    kbCards && kbCards.length > 0
      ? kbCards.some(k => k.status === 'failed')
        ? ('failed' as const)
        : kbCards.some(k => k.status === 'processing')
          ? ('processing' as const)
          : kbCards.some(k => k.status === 'indexed')
            ? ('indexed' as const)
            : ('none' as const)
      : null;

  const Component = isInteractive ? 'div' : 'article';

  return (
    <Component
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      ref={cardRef as React.Ref<any>}
      className={cn(
        meepleCardVariants({ variant }),
        'hover:outline-2 hover:outline-offset-2',
        '[&:hover]:[-webkit-transform:translateY(-6px)] [&:hover]:[transform:translateY(-6px)]',
        selected && 'ring-2 ring-offset-2 bg-accent/10',
        selected && `ring-[hsl(${color})]`,
        className
      )}
      style={
        {
          '--mc-entity-color': `hsl(${color})`,
          outlineColor: `hsla(${color}, 0.4)`,
          willChange: 'transform, box-shadow, outline',
          viewTransitionName: entityId ? `meeple-card-${entityId}` : undefined,
        } as React.CSSProperties
      }
      onClick={handleCardClick}
      role={isInteractive || (isMobile && hasMobileActions) ? 'button' : undefined}
      tabIndex={isInteractive || (isMobile && hasMobileActions) ? 0 : undefined}
      onKeyDown={
        isInteractive
          ? e => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onClick?.();
              }
            }
          : undefined
      }
      // eslint-disable-next-line security/detect-object-injection
      aria-label={`${entityColors[entity].name}: ${title}`}
      data-testid={testId || 'meeple-card'}
      data-entity={entity}
      data-variant={variant}
    >
      {selectable && (
        <BulkSelectCheckbox
          selectable={selectable}
          selected={!!selected}
          onSelect={onSelect || (() => {})}
          id={testId || 'card'}
          entityColor={color}
        />
      )}

      {entity === 'session' &&
        isTimeTravelMode &&
        sessionSnapshots &&
        currentSnapshotIndex != null &&
        sessionSnapshots[currentSnapshotIndex] && (
          <TimeTravelOverlay
            snapshot={sessionSnapshots[currentSnapshotIndex]}
            totalSnapshots={sessionSnapshots.length}
            isActive={isTimeTravelMode}
            onExit={() => onTimeTravelToggle?.(false)}
          />
        )}

      <EntityIndicator entity={entity} variant={variant} customColor={customColor} />

      <CardTagStrip
        variant={variant}
        entity={entity}
        customColor={customColor}
        tags={tags}
        maxVisibleTags={maxVisibleTags}
        showTagStrip={showTagStrip}
        status={status}
        showStatusIcon={showStatusIcon}
        badge={badge}
      />

      <CardBadges variant={variant} linkCount={linkCount} onLinksClick={onLinksClick} />

      <CardCover
        src={coverSrc}
        alt={title}
        variant={variant}
        entity={entity}
        customColor={customColor}
      />

      {/* Content area */}
      <div className={contentVariants({ variant })}>
        <CardActions
          variant={variant}
          entity={entity}
          customColor={customColor}
          actions={actions}
          entityQuickActions={entityQuickActions}
          quickActions={quickActions}
          userRole={userRole}
          showWishlistBtn={showWishlistBtn}
          isWishlisted={isWishlisted}
          onWishlistToggle={onWishlistToggle}
          showInfoButton={showInfoButton}
          entityId={entityId}
          infoHref={infoHref}
          infoTooltip={infoTooltip}
          drawerEntityType={drawerEntityType}
          onDrawerOpen={() => setDrawerOpen(true)}
          testId={testId}
          unreadCount={unreadCount}
          hasQuickActions={hasQuickActions}
        />

        <h3 className="font-quicksand font-bold leading-tight text-xl mb-1 text-card-foreground">
          {title}
        </h3>

        {subtitle && <p className="text-muted-foreground text-sm mb-2">{subtitle}</p>}

        {rating !== undefined && <RatingDisplay rating={rating} max={ratingMax} className="mb-2" />}

        {/* Agent-specific info */}
        {entity === 'agent' &&
          (agentStatus ||
            agentModel ||
            (capabilities && capabilities.length > 0) ||
            agentStats) && (
            <div className="flex flex-col gap-1.5 mb-2" data-testid="agent-info-section">
              {(agentStatus || agentModel) && (
                <div className="flex items-center gap-2 flex-wrap">
                  {agentStatus && <AgentStatusBadge status={agentStatus} size="sm" />}
                  {agentModel && (
                    <AgentModelInfo
                      modelName={agentModel.modelName}
                      parameters={agentModel.parameters}
                      size="sm"
                    />
                  )}
                </div>
              )}
              {capabilities && capabilities.length > 0 && (
                <div className="flex items-center gap-1 flex-wrap">
                  {capabilities.map(cap => (
                    <span
                      key={cap}
                      className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300"
                    >
                      {cap}
                    </span>
                  ))}
                </div>
              )}
              {agentStats && (
                <AgentStatsDisplay
                  stats={agentStats}
                  layout="horizontal"
                  className="text-muted-foreground"
                />
              )}
            </div>
          )}

        {/* ChatSession-specific info */}
        {entity === 'chatSession' && (chatStatus || chatAgent || chatStats || chatGame) && (
          <div className="flex flex-col gap-1.5 mb-2" data-testid="chat-info-section">
            {(chatStatus || chatAgent) && (
              <div className="flex items-center gap-2 flex-wrap">
                {chatStatus && <ChatStatusBadge status={chatStatus} size="sm" />}
                {chatAgent && <ChatAgentInfo agent={chatAgent} />}
              </div>
            )}
            {chatGame && <ChatGameContext game={chatGame} />}
            {chatStats && (
              <ChatStatsDisplay
                stats={chatStats}
                layout="horizontal"
                className="text-muted-foreground"
              />
            )}
            {chatPreview && (
              <p className="text-xs text-muted-foreground truncate" data-testid="chat-preview">
                <span className="font-semibold">
                  {chatPreview.sender === 'user' ? 'You' : 'Agent'}:
                </span>{' '}
                {chatPreview.lastMessage}
              </p>
            )}
          </div>
        )}

        {entity === 'chatSession' && unreadCount !== undefined && unreadCount > 0 && (
          <ChatUnreadBadge count={unreadCount} />
        )}

        {/* Session-specific info */}
        {entity === 'session' && (sessionStatus || sessionPlayers || sessionTurn) && (
          <div className="flex flex-col gap-1.5 mb-2" data-testid="session-info-section">
            {sessionStatus && <SessionStatusBadge status={sessionStatus} size="sm" />}
            {sessionPlayers && sessionPlayers.length > 0 && (
              <SessionScoreTable
                players={sessionPlayers}
                roundScores={sessionRoundScores ?? []}
                onEditScore={sessionActions?.onEditScore}
                maxVisibleRounds={5}
              />
            )}
            {sessionTurn &&
              sessionPlayers &&
              sessionPlayers.length > 0 &&
              sessionStatus !== 'completed' && (
                <SessionTurnSequence
                  players={sessionPlayers}
                  turn={sessionTurn}
                  isHost={isSessionHost}
                  onPrevTurn={onPrevTurn}
                  onNextTurn={onNextTurn}
                />
              )}
            {sessionStatus && sessionActions && (
              <SessionActionButtons status={sessionStatus} actions={sessionActions} />
            )}
          </div>
        )}

        {entity === 'kb' && documentStatus && (
          <div className="flex items-center gap-1.5 mb-2">
            <DocumentStatusBadge status={documentStatus} size="sm" />
          </div>
        )}

        {entity === 'game' && worstKbStatus && (
          <div className="flex items-center gap-1.5 mb-2" data-testid="meeple-card-kb-badge">
            <DocumentStatusBadge status={worstKbStatus} size="sm" />
            <span className="text-[10px] text-muted-foreground font-medium">
              {kbCards!.length} KB
            </span>
          </div>
        )}

        {entity === 'session' && sessionSnapshots && sessionSnapshots.length > 0 && (
          <SnapshotHistorySlider
            snapshots={sessionSnapshots}
            currentIndex={currentSnapshotIndex}
            onSelect={onSnapshotSelect}
            isTimeTravelMode={isTimeTravelMode}
            onTimeTravelToggle={onTimeTravelToggle}
          />
        )}

        {/* Metadata (inline for featured) */}
        {metadata.length > 0 && <MetadataChips metadata={metadata} variant={variant} />}

        {/* Action buttons are rendered by CardActions above (featured variant) */}
      </div>

      {/* Agent action footer */}
      {entity === 'game' && hasAgent !== undefined && id && (
        <CardAgentAction
          hasAgent={hasAgent}
          agentId={agentId}
          gameId={id}
          onCreateAgent={onCreateAgent}
          variant={variant}
          hasNavFooter={!!(navigateTo && navigateTo.length > 0)}
        />
      )}

      {navigateTo && navigateTo.length > 0 && <CardNavigationFooter links={navigateTo} />}

      {firstLinkPreview && linkCount !== undefined && linkCount > 0 && (
        <EntityLinkPreviewRow
          linkType={firstLinkPreview.linkType}
          targetName={firstLinkPreview.targetName}
          totalCount={linkCount}
          onClick={onLinksClick}
        />
      )}

      {entityId && drawerEntityType && (
        <ExtraMeepleCardDrawer
          entityType={drawerEntityType}
          entityId={entityId}
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
        />
      )}
    </Component>
  );
});
