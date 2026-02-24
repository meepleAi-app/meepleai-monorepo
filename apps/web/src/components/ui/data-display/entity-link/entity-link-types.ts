/**
 * EntityLink UI Types & Constants
 *
 * Shared types and configuration for all EntityLink UI components.
 * Issue #5129 — Epic C: Card Navigation Graph Completion
 */

import type { EntityLinkType, LinkEntityType } from '@/lib/api/schemas/entity-links.schemas';

export type { EntityLinkType, LinkEntityType };
export type {
  EntityLinkDto,
  CreateEntityLinkRequest,
} from '@/lib/api/schemas/entity-links.schemas';

// ============================================================================
// Link Type Display Configuration
// ============================================================================

export interface LinkTypeConfig {
  /** Short human-readable label */
  label: string;
  /** Direction symbol: directed (→) or bilateral (↔) */
  directionIcon: '→' | '↔';
  /** HSL color string (without hsl()) */
  color: string;
}

export const LINK_TYPE_CONFIG: Record<EntityLinkType, LinkTypeConfig> = {
  ExpansionOf: { label: 'expansion of', directionIcon: '→', color: '38 92% 50%' },
  SequelOf: { label: 'sequel of', directionIcon: '→', color: '220 80% 55%' },
  Reimplements: { label: 'reimplements', directionIcon: '→', color: '25 95% 45%' },
  CompanionTo: { label: 'companion', directionIcon: '↔', color: '142 70% 45%' },
  RelatedTo: { label: 'related', directionIcon: '↔', color: '210 40% 55%' },
  PartOf: { label: 'part of', directionIcon: '→', color: '262 83% 58%' },
  CollaboratesWith: { label: 'collaborates', directionIcon: '↔', color: '240 60% 55%' },
  SpecializedBy: { label: 'specialized by', directionIcon: '→', color: '270 70% 58%' },
};

// ============================================================================
// Entity Type Display Configuration
// ============================================================================

export interface LinkEntityConfig {
  /** Short label */
  label: string;
  /** HSL color string */
  color: string;
  /** Lucide icon name */
  iconName: string;
}

export const LINK_ENTITY_CONFIG: Record<LinkEntityType, LinkEntityConfig> = {
  Game: { label: 'Game', color: '25 95% 45%', iconName: 'Gamepad2' },
  Player: { label: 'Player', color: '262 83% 58%', iconName: 'User' },
  Session: { label: 'Session', color: '240 60% 55%', iconName: 'PlayCircle' },
  Agent: { label: 'Agent', color: '38 92% 50%', iconName: 'Bot' },
  Document: { label: 'Document', color: '210 40% 55%', iconName: 'FileText' },
  ChatSession: { label: 'Chat', color: '220 80% 55%', iconName: 'MessageCircle' },
  Event: { label: 'Event', color: '350 89% 60%', iconName: 'Calendar' },
  Toolkit: { label: 'Toolkit', color: '142 70% 45%', iconName: 'Wrench' },
};

// ============================================================================
// Link Type → allowed target entity types (AddEntityLinkModal)
// ============================================================================

export const LINK_TYPE_TARGET_ENTITIES: Record<EntityLinkType, LinkEntityType[]> = {
  ExpansionOf: ['Game'],
  SequelOf: ['Game'],
  Reimplements: ['Game'],
  CompanionTo: ['Game'],
  RelatedTo: ['Game', 'Agent', 'Document', 'Session', 'Event'],
  PartOf: ['Event'],
  CollaboratesWith: ['Agent'],
  SpecializedBy: ['Agent'],
};

// ============================================================================
// Group link types by category for drawer display
// ============================================================================

export interface LinkTypeGroup {
  label: string;
  types: EntityLinkType[];
}

export const LINK_TYPE_GROUPS: LinkTypeGroup[] = [
  { label: 'Expansions', types: ['ExpansionOf'] },
  { label: 'Sequels', types: ['SequelOf'] },
  { label: 'Reimplements', types: ['Reimplements'] },
  { label: 'Companions', types: ['CompanionTo'] },
  { label: 'Related', types: ['RelatedTo'] },
  { label: 'Part of', types: ['PartOf'] },
  { label: 'Collaborates', types: ['CollaboratesWith'] },
  { label: 'Specialized', types: ['SpecializedBy'] },
];
