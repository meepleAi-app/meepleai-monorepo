/**
 * Entity Variant ExtraMeepleCards — Barrel re-exports
 * Issue #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
 * Issue #5026 - AgentExtraMeepleCard (Epic #5023)
 * Issue #5027 - ChatExtraMeepleCard (Epic #5023)
 * Issue #5028 - KbExtraMeepleCard (Epic #5023)
 * Issue #5029 - GameExtraMeepleCard: KB + Agent tabs (Epic #5023)
 *
 * Components have been decomposed into individual files under ./entities/
 * Shared infrastructure (colors, header, stat cards) lives in ./shared.tsx
 */

export { GameExtraMeepleCard } from './entities/GameExtraMeepleCard';
export type { GameExtraMeepleCardProps } from './entities/GameExtraMeepleCard';

export { PlayerExtraMeepleCard } from './entities/PlayerExtraMeepleCard';
export type { PlayerExtraMeepleCardProps } from './entities/PlayerExtraMeepleCard';

export { CollectionExtraMeepleCard } from './entities/CollectionExtraMeepleCard';
export type { CollectionExtraMeepleCardProps } from './entities/CollectionExtraMeepleCard';

export { AgentExtraMeepleCard } from './entities/AgentExtraMeepleCard';
export type { AgentExtraMeepleCardProps } from './entities/AgentExtraMeepleCard';

export { ChatExtraMeepleCard } from './entities/ChatExtraMeepleCard';
export type { ChatExtraMeepleCardProps } from './entities/ChatExtraMeepleCard';

export { KbExtraMeepleCard } from './entities/KbExtraMeepleCard';
export type { KbExtraMeepleCardProps } from './entities/KbExtraMeepleCard';
