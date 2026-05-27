/**
 * useHybridHubItems — Phase 2a (#1605) orchestration hook for the `/library`
 * hybrid hub. Calls the 3 ready entity sources (games / sessions / chat),
 * maps each DTO to a `HybridHubItem` via the Phase 1 mappers, caps each source
 * to `PER_SOURCE_CAP`, and reports per-source errors so the hub can degrade
 * gracefully (AC9.1). Agents + KB are stubbed to `[]` until BE-2 #1589 / BE-1
 * #1588 ship (Phase 2b).
 *
 * Returns `HybridHubSources` (the shape `deriveHybridItems` consumes) — the
 * hook is the data layer; tab/query/sort derivation stays in `LibraryHub`.
 */

import { useMemo } from 'react';

import type { HybridHubSources } from '@/lib/library/hybrid-hub.derive';
import {
  chatToHubItem,
  libraryEntryToHubItem,
  sessionToHubItem,
} from '@/lib/library/hybrid-hub.mappers';
import type { HybridHubItem } from '@/lib/library/hybrid-hub.types';

import { useActiveSessions } from './useActiveSessions';
import { useAgents } from './useAgents';
import { useRecentChatSessions } from './useChatSessions';
import { useLibrary } from './useLibrary';

export const PER_SOURCE_CAP = 20;

export type HybridHubSourceKey = keyof HybridHubSources;

export interface UseHybridHubItemsResult {
  readonly sources: HybridHubSources;
  readonly isLoading: boolean;
  readonly allFailed: boolean;
  readonly partialErrors: Record<HybridHubSourceKey, Error | null>;
  readonly totalCounts: Record<HybridHubSourceKey, number>;
}

export function useHybridHubItems(): UseHybridHubItemsResult {
  const libraryQuery = useLibrary({
    page: 1,
    pageSize: 50,
    sortBy: 'addedAt',
    sortDescending: true,
  });
  const sessionsQuery = useActiveSessions(PER_SOURCE_CAP);
  const chatQuery = useRecentChatSessions(50);
  const agentsQuery = useAgents({});

  return useMemo(() => {
    const gameItems = (libraryQuery.data?.items ?? []).map(libraryEntryToHubItem);
    const sessionItems = (sessionsQuery.data?.sessions ?? []).map(sessionToHubItem);
    const chatItems = (chatQuery.data?.sessions ?? []).map(chatToHubItem);

    const cap = (items: readonly HybridHubItem[]) => items.slice(0, PER_SOURCE_CAP);

    const sources: HybridHubSources = {
      games: libraryQuery.isError ? [] : cap(gameItems),
      sessions: sessionsQuery.isError ? [] : cap(sessionItems),
      chat: chatQuery.isError ? [] : cap(chatItems),
      agents: [],
      kb: [],
    };

    const partialErrors: Record<HybridHubSourceKey, Error | null> = {
      games: libraryQuery.isError ? (libraryQuery.error ?? new Error('library')) : null,
      sessions: sessionsQuery.isError ? (sessionsQuery.error ?? new Error('sessions')) : null,
      chat: chatQuery.isError ? (chatQuery.error ?? new Error('chat')) : null,
      agents: null,
      kb: null,
    };

    const totalCounts: Record<HybridHubSourceKey, number> = {
      games: gameItems.length,
      sessions: sessionItems.length,
      chat: chatItems.length,
      agents: 0,
      kb: 0,
    };

    const readyErrors = [libraryQuery.isError, sessionsQuery.isError, chatQuery.isError];
    const allFailed = readyErrors.every(Boolean);
    const isLoading = libraryQuery.isLoading || sessionsQuery.isLoading || chatQuery.isLoading;

    void agentsQuery;

    return { sources, isLoading, allFailed, partialErrors, totalCounts };
  }, [
    libraryQuery.data,
    libraryQuery.isError,
    libraryQuery.error,
    libraryQuery.isLoading,
    sessionsQuery.data,
    sessionsQuery.isError,
    sessionsQuery.error,
    sessionsQuery.isLoading,
    chatQuery.data,
    chatQuery.isError,
    chatQuery.error,
    chatQuery.isLoading,
    agentsQuery,
  ]);
}
