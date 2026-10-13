/**
 * useAgentData - Shared hooks for agent KB documents and chat threads
 *
 * Extracted from AgentCharacterSheet and AgentExtraMeepleCard to eliminate
 * inline fetch+useState+useEffect duplication. Uses React Query for caching,
 * deduplication, and consistent loading/error states.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import type {
  ChatThreadPreview,
  KbDocumentPreview,
} from '@/components/ui/data-display/extra-meeple-card/types';

// Re-export types for consumer convenience
export type { ChatThreadPreview, KbDocumentPreview };

// ========== Query Keys ==========

export const agentDataKeys = {
  all: ['agent-data'] as const,
  kbDocs: (gameId: string) => [...agentDataKeys.all, 'kb-docs', gameId] as const,
  threads: (agentId: string) => [...agentDataKeys.all, 'threads', agentId] as const,
  threadsByGame: (gameId: string) => [...agentDataKeys.all, 'threads-by-game', gameId] as const,
};

// ========== Mapping Functions ==========

/**
 * Map raw API thread response to ChatThreadPreview[].
 * The API may return messages inline or as a count — this normalises both shapes.
 */
export function mapThreads(json: unknown[]): ChatThreadPreview[] {
  return json.map(raw => {
    const t = raw as Record<string, unknown>;
    const messages = Array.isArray(t.messages) ? (t.messages as Record<string, unknown>[]) : [];
    const firstMsg = messages[0];
    const preview = typeof firstMsg?.content === 'string' ? firstMsg.content : '';
    return {
      id: String(t.id ?? ''),
      createdAt: String(t.createdAt ?? t.startedAt ?? new Date().toISOString()),
      messageCount: messages.length,
      firstMessagePreview: preview,
    };
  });
}

/**
 * Map raw API document response to KbDocumentPreview[].
 * Normalises varying field names (fileName/name, uploadedAt/createdAt) and status strings.
 */
export function mapKbDocs(json: unknown[]): KbDocumentPreview[] {
  return json.map(raw => {
    const d = raw as Record<string, unknown>;
    const statusMap: Record<string, KbDocumentPreview['status']> = {
      indexed: 'indexed',
      processing: 'processing',
      failed: 'failed',
      none: 'none',
    };
    const rawStatus = String(d.status ?? 'none').toLowerCase();
    return {
      id: String(d.id ?? ''),
      fileName: String(d.fileName ?? d.name ?? 'Documento'),
      uploadedAt: String(d.uploadedAt ?? d.createdAt ?? new Date().toISOString()),
      status: statusMap[rawStatus] ?? 'none',
    };
  });
}

// ========== Query Hooks ==========

/**
 * Fetch KB documents for a game.
 *
 * @param gameId - Game ID whose KB documents to fetch (empty/undefined disables the query)
 * @returns React Query result with KbDocumentPreview[]
 *
 * @example
 * ```tsx
 * const { data: docs = [], isLoading } = useAgentKbDocs(game.id);
 * ```
 */
export function useAgentKbDocs(gameId: string | undefined): UseQueryResult<KbDocumentPreview[]> {
  return useQuery({
    queryKey: agentDataKeys.kbDocs(gameId ?? ''),
    queryFn: async () => {
      const res = await fetch(`/api/v1/knowledge-base/${gameId}/documents`);
      if (!res.ok) throw new Error('Failed to fetch KB docs');
      const json = (await res.json()) as unknown[];
      return mapKbDocs(json);
    },
    enabled: !!gameId,
    staleTime: 60_000,
  });
}

/**
 * Fetch chat threads for an agent.
 *
 * @param agentId - Agent ID whose threads to fetch (empty disables the query)
 * @returns React Query result with ChatThreadPreview[]
 *
 * @example
 * ```tsx
 * const { data: threads = [], isLoading } = useAgentThreads(agent.id);
 * ```
 */
export function useAgentThreads(agentId: string): UseQueryResult<ChatThreadPreview[]> {
  return useQuery({
    queryKey: agentDataKeys.threads(agentId),
    queryFn: async () => {
      // 🔴 Issue #4138 — THE BACKEND IGNORES `agentId`. `GET /api/v1/chat-threads/my`
      // (KnowledgeBaseEndpoints → HandleGetMyFilteredThreads) binds exactly
      // `gameId`, `agentType`, `status`, `search`, `page`, `pageSize`. `agentId` is not
      // among them, and ASP.NET drops unknown query parameters silently — so this
      // returns the caller's threads UNFILTERED, not the agent's.
      //
      // Not fixed here: the three remaining callers (AgentCharacterSheet,
      // AgentChatDrawerLayout, AgentExtraMeepleCard) are agent-detail surfaces outside
      // the scope of this retirement, and "filter by agent" may not be expressible at
      // all once there is one system agent. `useGameThreads` below is the correct
      // scoping for anything that asks about a GAME.
      const res = await fetch(`/api/v1/chat-threads/my?agentId=${agentId}`);
      if (!res.ok) throw new Error('Failed to fetch threads');
      const json = (await res.json()) as unknown[];
      return mapThreads(json);
    },
    enabled: !!agentId,
    staleTime: 30_000,
  });
}

/**
 * Chat threads of the caller for a GAME.
 *
 * Issue #4138: `useAgentThreads` was used for a per-game chat preview by way of
 * `agents[0].id`, which was wrong twice over — the agent was incidental, and the
 * `agentId` parameter does not exist on the endpoint. `gameId` does.
 */
export function useGameThreads(gameId: string): UseQueryResult<ChatThreadPreview[]> {
  return useQuery({
    queryKey: agentDataKeys.threadsByGame(gameId),
    queryFn: async () => {
      const res = await fetch(`/api/v1/chat-threads/my?gameId=${gameId}`);
      if (!res.ok) throw new Error('Failed to fetch threads');
      const json = (await res.json()) as unknown[];
      return mapThreads(json);
    },
    enabled: !!gameId,
    staleTime: 30_000,
  });
}
