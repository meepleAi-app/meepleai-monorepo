/**
 * useHybridHubItems — Phase 2a (#1605) + Phase 2b (#1592) orchestration hook
 * for the `/library` hybrid hub.
 *
 * Calls all 5 entity sources (games / sessions / chat / agents / kb), maps each
 * DTO to a `HybridHubItem` via the Phase 1 mappers, caps each source to
 * `PER_SOURCE_CAP`, and reports per-source errors so the hub can degrade
 * gracefully (AC9.1 + AC2.b.5 partial-failure).
 *
 * Phase 2b changes: agents source is now wired via `useAgents({scope:'my-library'})`
 * (BE-2 #1589); kb source is wired via `useUserKbDocs` (BE-1 #1588). The hook
 * propagates real errors and includes both in `isLoading`.
 *
 * Returns `HybridHubSources` (the shape `deriveHybridItems` consumes) — the
 * hook is the data layer; tab/query/sort derivation stays in `LibraryHub`.
 */

import { useMemo } from 'react';

import type { HybridHubSources } from '@/lib/library/hybrid-hub.derive';
import {
  agentToHubItem,
  chatToHubItem,
  kbDocToHubItem,
  libraryEntryToHubItem,
  sessionToHubItem,
} from '@/lib/library/hybrid-hub.mappers';
import type { HybridHubItem } from '@/lib/library/hybrid-hub.types';

import { useActiveSessions } from './useActiveSessions';
import { useAgents } from './useAgents';
import { useRecentChatSessions } from './useChatSessions';
import { useLibrary } from './useLibrary';
import { useUserKbDocs } from './useUserKbDocs';

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
  // BE-2 #1589: library-scoped (game ∈ caller's library + system agents).
  const agentsQuery = useAgents({ scope: 'my-library' });
  // BE-1 #1588: state=ready, page 1, pageSize=20, sortBy=recent (K3).
  const kbQuery = useUserKbDocs();

  return useMemo(() => {
    const gameItems = (libraryQuery.data?.items ?? []).map(libraryEntryToHubItem);
    const sessionItems = (sessionsQuery.data?.sessions ?? []).map(sessionToHubItem);
    const chatItems = (chatQuery.data?.sessions ?? []).map(chatToHubItem);
    const agentItems = (agentsQuery.data ?? []).map(agentToHubItem);
    const kbItems = (kbQuery.data?.items ?? []).map(kbDocToHubItem);

    const cap = (items: readonly HybridHubItem[]) => items.slice(0, PER_SOURCE_CAP);

    const sources: HybridHubSources = {
      games: libraryQuery.isError ? [] : cap(gameItems),
      sessions: sessionsQuery.isError ? [] : cap(sessionItems),
      chat: chatQuery.isError ? [] : cap(chatItems),
      agents: agentsQuery.isError ? [] : cap(agentItems),
      kb: kbQuery.isError ? [] : cap(kbItems),
    };

    const partialErrors: Record<HybridHubSourceKey, Error | null> = {
      games: libraryQuery.isError ? (libraryQuery.error ?? new Error('library')) : null,
      sessions: sessionsQuery.isError ? (sessionsQuery.error ?? new Error('sessions')) : null,
      chat: chatQuery.isError ? (chatQuery.error ?? new Error('chat')) : null,
      agents: agentsQuery.isError ? (agentsQuery.error ?? new Error('agents')) : null,
      kb: kbQuery.isError ? (kbQuery.error ?? new Error('kb')) : null,
    };

    const totalCounts: Record<HybridHubSourceKey, number> = {
      games: gameItems.length,
      sessions: sessionItems.length,
      chat: chatItems.length,
      agents: agentItems.length,
      kb: kbItems.length,
    };

    const allErrors = [
      libraryQuery.isError,
      sessionsQuery.isError,
      chatQuery.isError,
      agentsQuery.isError,
      kbQuery.isError,
    ];
    const allFailed = allErrors.every(Boolean);
    const isLoading =
      libraryQuery.isLoading ||
      sessionsQuery.isLoading ||
      chatQuery.isLoading ||
      agentsQuery.isLoading ||
      kbQuery.isLoading;

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
    agentsQuery.data,
    agentsQuery.isError,
    agentsQuery.error,
    agentsQuery.isLoading,
    kbQuery.data,
    kbQuery.isError,
    kbQuery.error,
    kbQuery.isLoading,
  ]);
}
