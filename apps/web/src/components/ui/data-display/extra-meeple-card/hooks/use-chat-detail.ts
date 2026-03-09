import React from 'react';

import type { ChatDetailData, ChatDetailMessage } from '../types';

// ============================================================================
// Data Fetching Hook — Chat
// ============================================================================

interface UseChatDetailResult {
  data: ChatDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

export function useChatDetail(threadId: string): UseChatDetailResult {
  const [data, setData] = React.useState<ChatDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fetchData = React.useCallback(
    async (signal: AbortSignal) => {
      setLoading(true);
      setError(null);
      try {
        const [threadRes, msgsRes] = await Promise.all([
          fetch(`/api/v1/chat/threads/${threadId}`, { signal }),
          fetch(`/api/v1/chat/threads/${threadId}/messages?limit=10`, { signal }),
        ]);
        if (!threadRes.ok) {
          throw new Error(`Errore ${threadRes.status}: thread non trovato`);
        }
        const thread = (await threadRes.json()) as Record<string, unknown>;
        const msgs = msgsRes.ok ? ((await msgsRes.json()) as unknown[]) : [];
        setData(mapToChatDetailData(thread, msgs));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(err instanceof Error ? err.message : 'Impossibile caricare i dati della chat');
      } finally {
        setLoading(false);
      }
    },
    [threadId]
  );

  React.useEffect(() => {
    if (!threadId) return;
    const controller = new AbortController();
    void fetchData(controller.signal);
    return () => {
      controller.abort();
    };
  }, [threadId, fetchData]);

  const retry = React.useCallback(() => {
    const controller = new AbortController();
    void fetchData(controller.signal);
  }, [fetchData]);

  return { data, loading, error, retry };
}

function mapToChatDetailData(thread: Record<string, unknown>, msgs: unknown[]): ChatDetailData {
  const statusMap: Record<string, ChatDetailData['status']> = {
    active: 'active',
    waiting: 'waiting',
    archived: 'archived',
    closed: 'closed',
  };
  const rawStatus = String(thread.status ?? 'closed').toLowerCase();
  return {
    id: String(thread.id ?? ''),
    status: statusMap[rawStatus] ?? 'closed',
    agentId: thread.agentId != null ? String(thread.agentId) : undefined,
    agentName: thread.agentName != null ? String(thread.agentName) : undefined,
    agentModel: thread.agentModel != null ? String(thread.agentModel) : undefined,
    gameId: thread.gameId != null ? String(thread.gameId) : undefined,
    gameName: thread.gameName != null ? String(thread.gameName) : undefined,
    gameThumbnailUrl: thread.gameThumbnailUrl != null ? String(thread.gameThumbnailUrl) : undefined,
    startedAt: String(thread.startedAt ?? thread.createdAt ?? new Date().toISOString()),
    durationMinutes: thread.durationMinutes != null ? Number(thread.durationMinutes) : undefined,
    messageCount: thread.messageCount != null ? Number(thread.messageCount) : msgs.length,
    messages: mapChatMessages(msgs),
    temperature: thread.temperature != null ? Number(thread.temperature) : undefined,
    maxTokens: thread.maxTokens != null ? Number(thread.maxTokens) : undefined,
    systemPrompt: thread.systemPrompt != null ? String(thread.systemPrompt) : undefined,
  };
}

function mapChatMessages(json: unknown[]): ChatDetailMessage[] {
  return json.map(raw => {
    const m = raw as Record<string, unknown>;
    return {
      id: String(m.id ?? ''),
      role: m.role === 'user' ? 'user' : 'assistant',
      content: String(m.content ?? ''),
      createdAt: String(m.createdAt ?? m.timestamp ?? new Date().toISOString()),
    };
  });
}
