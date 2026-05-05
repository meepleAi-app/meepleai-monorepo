/**
 * ThreadCreator — Pure utility for creating chat threads
 *
 * Handles system-agent vs custom-agent ID resolution and calls the chat API.
 * Returns the created thread ID or throws on failure.
 */

import { api } from '@/lib/api';

import type { CustomAgent, PromptType } from './types';

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
 * Resolve the best agent ID for thread creation.
 * Custom agents (user-owned) take priority over system agents.
 * System agent types (auto/qa/rules/strategy) are UI-only labels
 * and don't map to backend agent UUIDs, so we return undefined.
 */
export function resolveAgentId(
  selectedCustomAgentId: string | null,
  customAgents: CustomAgent[]
): string | undefined {
  if (selectedCustomAgentId) return selectedCustomAgentId;
  if (customAgents.length > 0) return customAgents[0].id;
  return undefined;
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
  selectedCustomAgentId: string | null;
  customAgents: CustomAgent[];
  initialMessage?: string;
  promptType?: PromptType;
  selectedKbIds?: string[];
}): Promise<CreateThreadResult> {
  const { gameId, gameName, selectedCustomAgentId, customAgents, initialMessage, selectedKbIds } =
    opts;

  const agentId = resolveAgentId(selectedCustomAgentId, customAgents);

  return createThread({
    gameId: gameId && gameId !== '' ? gameId : null,
    gameName,
    agentId: agentId ?? null,
    initialMessage: initialMessage ?? null,
    selectedKnowledgeBaseIds: selectedKbIds,
  });
}
