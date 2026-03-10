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

const DocumentCard = React.memo(function DocumentCard(props: EntityCardProps) {
  return <MeepleCard entity="document" {...props} />;
});
DocumentCard.displayName = 'MeepleCards.Document';

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

const KbCardCard = React.memo(function KbCardCard(props: EntityCardProps) {
  return <MeepleCard entity="kb_card" {...props} />;
});
KbCardCard.displayName = 'MeepleCards.KbCard';

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
  /** Document/PDF card (slate accent) */
  Document: DocumentCard,
  /** Chat session card (violet accent) */
  ChatSession: ChatSessionCard,
  /** Event card (rose accent) */
  Event: EventCard,
  /** Game toolkit card (green accent) */
  Toolkit: ToolkitCard,
  /** Knowledge base card */
  KbCard: KbCardCard,
  /** Base card for dynamic entity types */
  Base: MeepleCard,
} as const;
