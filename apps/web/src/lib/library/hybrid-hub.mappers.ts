/**
 * Pure mappers DTO → HybridHubItem — Phase 1 (Issue #1591).
 *
 * Each function is a deterministic transform over a single source DTO; no IO,
 * no React, no global state — so unit-testable in isolation. Phase 2's
 * orchestrator will call these inside `useMemo` after the per-entity hooks
 * resolve.
 *
 * Conventions:
 *   - `subtitle`, `gameName`, optional fields: convert `null` from the DTO to
 *     `undefined` (the FE convention for "absent"); the union types only model
 *     `undefined`.
 *   - `updatedAt`: pick the most meaningful recency signal on the source.
 *   - `href`: stick to current public routes; final routing in Phase 2.
 */

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import type { AgentHubItem, GameHubItem, KbHubItem, SessionHubItem } from './hybrid-hub.types';

/**
 * KbDoc — greenfield FE interface for the cross-game user KB listing.
 *
 * This shape will be replaced by an inferred Zod schema in
 * `lib/api/schemas/kb-docs.schemas.ts` when BE-1 #1588 ships the
 * `GET /kb-docs?userId` endpoint. The fields here mirror what we expect from
 * `PdfDocumentEntity` filtered by `UploadedByUserId` (see issue #1588 body).
 * Keep this stable: replacing the type later should be a zero-touch swap.
 */
export interface KbDoc {
  readonly id: string;
  readonly gameId: string | null;
  readonly gameName: string | null;
  readonly fileName: string;
  readonly processingState: string;
  readonly pageCount?: number | null;
  readonly processedAt: string | null;
  readonly updatedAt: string;
}

export function libraryEntryToHubItem(entry: UserLibraryEntry): GameHubItem {
  return {
    id: entry.id,
    entity: 'game',
    title: entry.gameTitle,
    subtitle: entry.gamePublisher ?? undefined,
    updatedAt: entry.stateChangedAt ?? entry.addedAt,
    href: `/library/${entry.gameId}`,
    gameId: entry.gameId,
    rating: entry.averageRating ?? undefined,
    state: entry.currentState,
    imageUrl: entry.gameImageUrl ?? entry.gameIconUrl ?? undefined,
  };
}

export function agentToHubItem(agent: AgentDto): AgentHubItem {
  return {
    id: agent.id,
    entity: 'agent',
    title: agent.name,
    subtitle: agent.gameName ?? undefined,
    updatedAt: agent.lastInvokedAt ?? agent.createdAt,
    href: `/agents/${agent.id}`,
    gameName: agent.gameName ?? undefined,
    agentType: agent.type,
    isActive: agent.isActive,
  };
}

export function kbDocToHubItem(doc: KbDoc): KbHubItem {
  return {
    id: doc.id,
    entity: 'kb',
    title: doc.fileName,
    subtitle: doc.gameName ?? undefined,
    updatedAt: doc.updatedAt,
    href: `/knowledge-base/${doc.id}`,
    gameName: doc.gameName ?? undefined,
    processingState: doc.processingState,
    pageCount: doc.pageCount ?? undefined,
  };
}

export function sessionToHubItem(session: GameSessionDto): SessionHubItem {
  return {
    id: session.id,
    entity: 'session',
    title: `Session ${session.id.slice(0, 8)}`,
    subtitle: session.winnerName ?? undefined,
    updatedAt: session.completedAt ?? session.startedAt,
    href: `/sessions/${session.id}`,
    gameName: undefined,
    status: session.status,
    playerCount: session.playerCount,
  };
}
