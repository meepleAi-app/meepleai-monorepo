/**
 * ThreadCreator — Pure utility for creating chat threads
 *
 * Calls the chat API and returns the created thread ID, or throws on failure.
 *
 * Issue #4138: a `resolveAgentId(selectedCustomAgentId, customAgents)` step stood here, picking a
 * per-game "custom agent" to attach to the thread. Those agents came from
 * `user_library_entries.CustomAgentConfigJson`, which nothing on the answer path reads, so the id
 * it resolved could not change any answer. Threads are scoped by GAME.
 */

import { api } from '@/lib/api';

import type { PromptType } from './types';

export interface CreateThreadParams {
  gameId?: string | null;
  gameName?: string;
  agentId?: string | null;
  initialMessage?: string | null;
  selectedKnowledgeBaseIds?: string[];
}

export interface CreateThreadResult {
  threadId: string;
}

/**
 * Create a chat thread via the API.
 * @returns threadId on success
 * @throws Error on API failure
 */
export async function createThread(params: CreateThreadParams): Promise<CreateThreadResult> {
  const { gameId, gameName, agentId, initialMessage, selectedKnowledgeBaseIds } = params;

  const thread = await api.chat.createThread({
    gameId: gameId ?? null,
    agentId: agentId ?? null,
    title: gameName ? `Chat: ${gameName}` : 'Nuova conversazione',
    initialMessage: initialMessage ?? null,
    selectedKnowledgeBaseIds: selectedKnowledgeBaseIds ?? null,
  });

  if (!thread?.id) {
    throw new Error('Thread creation returned no ID');
  }

  return { threadId: thread.id };
}

/**
 * Convenience: create thread with full context resolution.
 */
export async function createThreadWithContext(opts: {
  gameId: string | null;
  gameName?: string;
  initialMessage?: string;
  promptType?: PromptType;
  selectedKbIds?: string[];
}): Promise<CreateThreadResult> {
  const { gameId, gameName, initialMessage, selectedKbIds } = opts;

  return createThread({
    gameId: gameId && gameId !== '' ? gameId : null,
    gameName,
    initialMessage: initialMessage ?? null,
    selectedKnowledgeBaseIds: selectedKbIds,
  });
}
