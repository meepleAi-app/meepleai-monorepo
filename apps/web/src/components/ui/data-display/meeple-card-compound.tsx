'use client';

/**
 * MeepleCard Compound Component API
 *
 * Provides entity-specific convenience wrappers around the base MeepleCard.
 * Each sub-component pre-sets the `entity` prop and narrows the type.
 *
 * @example
 * ```tsx
 * import { MeepleCards } from '@/components/ui/data-display/meeple-card-compound';
 *
 * <MeepleCards.Game variant="grid" title="Catan" subtitle="Kosmos" />
 * <MeepleCards.Player variant="list" title="Marco" avatarUrl="/img.jpg" />
 * <MeepleCards.Agent variant="compact" title="RAG Agent" agentStatus="active" />
 * ```
 */

import React from 'react';

import { MeepleCard, type MeepleCardProps } from './meeple-card';

// ── Entity-specific prop types (omit entity, it's pre-set) ──────────────────

type EntityCardProps = Omit<MeepleCardProps, 'entity'>;

// ── Entity sub-components ───────────────────────────────────────────────────

const GameCard = React.memo(function GameCard(props: EntityCardProps) {
  return <MeepleCard entity="game" {...props} />;
});
GameCard.displayName = 'MeepleCards.Game';

const PlayerCard = React.memo(function PlayerCard(props: EntityCardProps) {
  return <MeepleCard entity="player" {...props} />;
});
PlayerCard.displayName = 'MeepleCards.Player';

const SessionCard = React.memo(function SessionCard(props: EntityCardProps) {
  return <MeepleCard entity="session" {...props} />;
});
SessionCard.displayName = 'MeepleCards.Session';

const AgentCard = React.memo(function AgentCard(props: EntityCardProps) {
  return <MeepleCard entity="agent" {...props} />;
});
AgentCard.displayName = 'MeepleCards.Agent';

const KbCard = React.memo(function KbCard(props: EntityCardProps) {
  return <MeepleCard entity="kb" {...props} />;
});
KbCard.displayName = 'MeepleCards.Kb';

const ChatSessionCard = React.memo(function ChatSessionCard(props: EntityCardProps) {
  return <MeepleCard entity="chatSession" {...props} />;
});
ChatSessionCard.displayName = 'MeepleCards.ChatSession';

const EventCard = React.memo(function EventCard(props: EntityCardProps) {
  return <MeepleCard entity="event" {...props} />;
});
EventCard.displayName = 'MeepleCards.Event';

const ToolkitCard = React.memo(function ToolkitCard(props: EntityCardProps) {
  return <MeepleCard entity="toolkit" {...props} />;
});
ToolkitCard.displayName = 'MeepleCards.Toolkit';


// ── Compound export ─────────────────────────────────────────────────────────

/**
 * MeepleCards compound component.
 *
 * Usage: `<MeepleCards.Game />`, `<MeepleCards.Player />`, etc.
 *
 * The base `MeepleCard` is also available for dynamic entity types
 * or custom entity cards.
 */
export const MeepleCards = {
  /** Board game card (orange accent) */
  Game: GameCard,
  /** Player/user card (purple accent) */
  Player: PlayerCard,
  /** Game session card (emerald accent) */
  Session: SessionCard,
  /** AI agent card (cyan accent) */
  Agent: AgentCard,
  /** Knowledge base card (teal accent) */
  Kb: KbCard,
  /** Chat session card (violet accent) */
  ChatSession: ChatSessionCard,
  /** Event card (rose accent) */
  Event: EventCard,
  /** Game toolkit card (green accent) */
  Toolkit: ToolkitCard,
  /** Base card for dynamic entity types */
  Base: MeepleCard,
} as const;
