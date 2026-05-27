/**
 * Hybrid hub item types — Phase 1 (Issue #1591) foundation for the
 * `/library` multi-entity hub (parent issue #1585).
 *
 * `HybridHubItem` is a discriminated union over 5 entity kinds (game / agent /
 * kb / session / chat). Each variant carries the common `HybridHubItemBase`
 * fields plus its own entity-specific extras.
 *
 * The discriminant is `entity`. The 5 values here are a strict subset of
 * `MeepleEntityType` in `components/ui/data-display/meeple-card/types.ts`
 * (which covers 9 kinds total: game/player/session/agent/kb/chat/event/toolkit/tool).
 * Because the 5 values are valid `MeepleEntityType` members, passing
 * `item.entity` directly to `<MeepleCard entity={item.entity} />` in
 * Phase 2's grid requires no cast.
 *
 * `updatedAt` is an ISO timestamp string — it drives the cross-entity
 * "recent" sort in `deriveHybridItems`. Each mapper resolves it from the
 * most meaningful timestamp on the source DTO (e.g. `lastInvokedAt` for an
 * agent, falling back to `createdAt`).
 *
 * `href` is the canonical navigation target rendered by the grid card click.
 * Final URL shapes are still subject to routing decisions in Phase 2; the
 * mappers stick to current public routes (`/library/{gameId}`,
 * `/agents/{id}`, `/knowledge-base/{id}`, `/sessions/{id}`, `/chats/{id}`)
 * and any redirect can be done at the routing layer later.
 */

import type { GameStateType } from '@/lib/api/schemas/library.schemas';

export type HybridHubEntity = 'game' | 'agent' | 'kb' | 'session' | 'chat';

export interface HybridHubItemBase {
  readonly id: string;
  readonly entity: HybridHubEntity;
  readonly title: string;
  readonly subtitle?: string;
  readonly updatedAt: string;
  readonly href: string;
}

export interface GameHubItem extends HybridHubItemBase {
  readonly entity: 'game';
  readonly gameId: string;
  readonly rating?: number;
  readonly state?: GameStateType;
  readonly imageUrl?: string;
  /** True if the game has ≥1 KB doc (mirrors the retired `kb` tab). Optional in the
   *  type (so test literals stay valid) but always set by `libraryEntryToHubItem`. */
  readonly hasKb?: boolean;
}

export interface AgentHubItem extends HybridHubItemBase {
  readonly entity: 'agent';
  readonly gameName?: string;
  /** Maps from `AgentDto.type` (renamed to avoid the reserved-word feel of `type`). */
  readonly agentType: string;
  readonly isActive: boolean;
}

export interface KbHubItem extends HybridHubItemBase {
  readonly entity: 'kb';
  readonly gameName?: string;
  /** Backend `ProcessingState` enum (Pending/Processing/Ready/Failed); kept as `string` until the FE schema lands with BE-1 #1588. */
  readonly processingState: string;
  readonly pageCount?: number;
}

export interface SessionHubItem extends HybridHubItemBase {
  readonly entity: 'session';
  readonly gameName?: string;
  readonly status: string;
  /** `GameSessionDto.playerCount` if available; otherwise the mapper derives it from `participants.length`. */
  readonly playerCount: number;
}

export interface ChatHubItem extends HybridHubItemBase {
  readonly entity: 'chat';
  readonly gameName?: string;
  readonly messageCount?: number;
}

export type HybridHubItem = GameHubItem | AgentHubItem | KbHubItem | SessionHubItem | ChatHubItem;
