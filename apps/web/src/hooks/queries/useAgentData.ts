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
      const res = await fetch(`/api/v1/chat-threads/my?agentId=${agentId}`);
      if (!res.ok) throw new Error('Failed to fetch threads');
      const json = (await res.json()) as unknown[];
      return mapThreads(json);
    },
    enabled: !!agentId,
    staleTime: 30_000,
  });
}
