/**
 * useGameManaPips — Aggregate session, KB, and agent counts for a game
 *
 * Builds ManaPips data for the Hero MeepleCard on the game detail page.
 * Uses three parallel API calls to gather related entity counts and items.
 */

import { useQuery } from '@tanstack/react-query';

import type { ManaPip, ManaPipItem } from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import { getKbPipColor } from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import { api } from '@/lib/api';

// ========== Query Key Factory ==========

export const gameManaPipsKeys = {
  all: ['game-mana-pips'] as const,
  byGame: (gameId: string) => ['game-mana-pips', gameId] as const,
} as const;

// ========== Types ==========

export interface GameManaPipsBucket {
  count: number;
  items: ManaPipItem[];
  indexedCount?: number;
  processingCount?: number;
}

export interface GameManaPipsData {
  sessions: GameManaPipsBucket;
  kbs: GameManaPipsBucket;
  agents: GameManaPipsBucket;
}

export interface GameManaPipsActions {
  onCreateSession?: () => void;
  onCreateKb?: () => void;
  onCreateAgent?: () => void;
  /** Open the chat panel with this game pre-selected (when KB is indexed). */
  onKbClick?: () => void;
}

// ========== Hook ==========

/**
 * Fetch and aggregate session, KB, and agent data for a game's ManaPips.
 *
 * All three API calls are made in parallel; individual failures fall back to
 * empty arrays so a single endpoint error never blocks the whole card.
 *
 * NOTE: `api.liveSessions.getActive()` returns `LiveSessionSummaryDto[]` which
 * does not include a `gameId` field, so live sessions are returned as-is
 * (no per-game filtering is possible from that endpoint).
 *
 * @param gameId - Game UUID, or null/undefined to disable the query
 * @returns React Query result with `GameManaPipsData`
 *
 * @example
 * ```tsx
 * const { data } = useGameManaPips(game.id);
 * const pips = buildGameManaPips(data, { onCreateSession: () => router.push('/sessions/new') });
 * ```
 */
export function useGameManaPips(gameId: string | null | undefined) {
  return useQuery<GameManaPipsData, Error>({
    queryKey: gameManaPipsKeys.byGame(gameId ?? ''),
    queryFn: async (): Promise<GameManaPipsData> => {
      const [sessions, kbDocs, agents] = await Promise.all([
        api.liveSessions.getActive().catch(() => []),
        api.knowledgeBase.getGameDocuments(gameId!).catch(() => []),
        api.agents.getUserAgentsForGame(gameId!).catch(() => []),
      ]);

      const sessionItems: ManaPipItem[] = sessions.map(s => ({
        id: s.id,
        label: s.gameName ?? s.sessionCode,
        href: `/sessions/${s.id}`,
      }));

      const kbItems: ManaPipItem[] = kbDocs.map(doc => ({
        id: doc.id,
        label: doc.title,
        href: `/games/${gameId}/knowledge-base`,
      }));

      const agentItems: ManaPipItem[] = agents.map(agent => ({
        id: agent.id,
        label: agent.name ?? agent.id,
        href: `/games/${gameId}/agents/${agent.id}`,
      }));

      return {
        sessions: { count: sessionItems.length, items: sessionItems },
        kbs: {
          count: kbItems.length,
          items: kbItems,
          indexedCount: kbDocs.filter(d => d.status === 'indexed').length,
          processingCount: kbDocs.filter(d => d.status === 'processing').length,
        },
        agents: { count: agentItems.length, items: agentItems },
      };
    },
    enabled: !!gameId,
    staleTime: 2 * 60_000, // 2 minutes
  });
}

// ========== Pure Builder ==========

/**
 * Build a `ManaPip[]` array from aggregated game data and optional action callbacks.
 *
 * Designed as a pure function so it can be used outside React (e.g. in tests or
 * server components that receive pre-fetched data).
 *
 * @param data - Aggregated data from `useGameManaPips`, or undefined while loading
 * @param actions - Optional callbacks for "create" actions on each pip type
 * @returns ManaPip array ready to pass to `<ManaPips pips={...} />`
 */
export function buildGameManaPips(
  data: GameManaPipsData | undefined,
  actions: GameManaPipsActions = {}
): ManaPip[] {
  if (!data) return [];

  const pips: ManaPip[] = [
    {
      entityType: 'session',
      count: data.sessions.count,
      items: data.sessions.items,
      ...(actions.onCreateSession && {
        onCreate: actions.onCreateSession,
        createLabel: 'Nuova sessione',
      }),
    },
    {
      entityType: 'kb',
      count: data.kbs.count,
      items: data.kbs.items,
      colorOverride: getKbPipColor({
        kbIndexedCount: data.kbs.indexedCount ?? 0,
        kbProcessingCount: data.kbs.processingCount ?? 0,
      }),
      ...((data.kbs.indexedCount ?? 0) > 0 && actions.onKbClick
        ? { onCreate: actions.onKbClick, createLabel: 'Chatta con AI' }
        : actions.onCreateKb
          ? { onCreate: actions.onCreateKb, createLabel: 'Carica PDF' }
          : {}),
    },
    {
      entityType: 'agent',
      count: data.agents.count,
      items: data.agents.items,
      ...(actions.onCreateAgent && {
        onCreate: actions.onCreateAgent,
        createLabel: 'Crea agente',
      }),
    },
  ];

  return pips;
}
