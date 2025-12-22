/**
 * useSearch Hook
 * Advanced search with Fuse.js fuzzy matching (Issue #1101)
 */

import { useState, useMemo, useCallback, useEffect } from 'react';

import Fuse from 'fuse.js';

import { CHAT_CONFIG } from '@/config';
import type {
  SearchResult,
  SearchFilters,
  SearchOptions,
  RecentSearch,
  MessageSearchResult,
  ChatSearchResult,
  GameSearchResult,
  AgentSearchResult,
} from '@/types';
import type { Game, Agent, Message, ChatThread } from '@/types';

import type { IFuseOptions } from 'fuse.js';

/**
 * Search data sources
 * Issue #2030: Added messagesByChat for chat context association
 */
interface SearchDataSources {
  messages?: Message[];
  messagesByChat?: Record<string, Message[]>; // Issue #2030 - Map chatId → messages for context
  chats?: ChatThread[];
  games?: Game[];
  agents?: Agent[];
}

/**
 * Local storage key for recent searches
 */
const RECENT_SEARCHES_KEY = 'meepleai_recent_searches';
const MAX_RECENT_SEARCHES = 20;

/**
 * Fuse.js configuration for fuzzy matching
 */
const FUSE_OPTIONS: IFuseOptions<SearchResult> = {
  keys: [
    { name: 'title', weight: 2 },
    { name: 'subtitle', weight: 1.5 },
    { name: 'message.content', weight: 1.8 },
    { name: 'chat.title', weight: 1.5 },
    { name: 'game.title', weight: 1.5 },
    { name: 'agent.name', weight: 1.5 },
  ],
  threshold: 0.3, // 0.0 = perfect match, 1.0 = match anything
  includeScore: true,
  minMatchCharLength: 2,
  ignoreLocation: true,
};

/**
 * Convert data sources to searchable results
 */
function buildSearchIndex(sources: SearchDataSources): SearchResult[] {
  const results: SearchResult[] = [];

  // Build message → chatId lookup map (Issue #2030)
  const messageToChatId = new Map<string, string>();
  if (sources.messagesByChat) {
    Object.entries(sources.messagesByChat).forEach(([chatId, messages]) => {
      messages.forEach(msg => {
        messageToChatId.set(msg.id, chatId);
      });
    });
  }

  // Index messages
  sources.messages?.forEach(message => {
    const chatId = messageToChatId.get(message.id) || '';
    const result: MessageSearchResult = {
      id: message.id,
      type: 'message',
      title: message.content.slice(0, CHAT_CONFIG.SEARCH_PREVIEW_MAX_LENGTH),
      subtitle: message.role === 'user' ? 'You' : 'Assistant',
      timestamp: message.timestamp,
      message,
      chatId, // Issue #2030 - Resolved from messagesByChat mapping
      gameId: message.gameId,
      relevanceScore: 0,
    };
    results.push(result);
  });

  // Index chats
  sources.chats?.forEach(chat => {
    const result: ChatSearchResult = {
      id: chat.id,
      type: 'chat',
      title: chat.title || 'Untitled Chat',
      subtitle: `${chat.messageCount} messages`,
      timestamp: new Date(chat.createdAt),
      chat,
      gameId: chat.gameId || undefined,
      messageCount: chat.messageCount,
      relevanceScore: 0,
    };
    results.push(result);
  });

  // Index games
  sources.games?.forEach(game => {
    const result: GameSearchResult = {
      id: game.id,
      type: 'game',
      title: game.title,
      subtitle: 'Board Game',
      timestamp: game.createdAt ? new Date(game.createdAt) : undefined,
      game,
      relevanceScore: 0,
    };
    results.push(result);
  });

  // Index agents
  sources.agents?.forEach(agent => {
    const result: AgentSearchResult = {
      id: agent.id,
      type: 'agent',
      title: agent.name,
      subtitle: `${agent.type} Agent`, // Changed from 'kind' to match Agent interface
      timestamp: new Date(agent.createdAt),
      agent,
      gameId: undefined, // Issue #868: Agents are global, not tied to games
      relevanceScore: 0,
    };
    results.push(result);
  });

  return results;
}

/**
 * Apply filters to search results
 */
function applyFilters(results: SearchResult[], filters?: SearchFilters): SearchResult[] {
  if (!filters) return results;

  return results.filter(result => {
    // Filter by type
    if (filters.types && filters.types.length > 0) {
      if (!filters.types.includes(result.type)) return false;
    }

    // Filter by game (agents are global and always included)
    if (filters.gameId && result.type !== 'agent') {
      const gameId =
        result.type === 'message'
          ? result.gameId
          : result.type === 'chat'
            ? result.gameId
            : undefined;
      if (gameId !== filters.gameId) return false;
    }

    // Filter by chat ID (Issue #2030)
    if (filters.chatId) {
      // Messages: filter by chat association
      if (result.type === 'message' && result.chatId !== filters.chatId) return false;

      // Chats: filter by exact chat ID
      if (result.type === 'chat' && result.id !== filters.chatId) return false;

      // Other types: not filterable by chat
      if (result.type === 'agent' || result.type === 'game' || result.type === 'pdf') return false;
    }

    // Filter by agent (Issue #2030)
    if (filters.agentId) {
      // Agent results: filter by agent ID
      if (result.type === 'agent' && result.id !== filters.agentId) return false;

      // Note: Messages and Chats will be filterable by agent after backend implementation
      // For now, we rely on chatId filtering (users select chat → messages filtered automatically)

      // Game results: cannot be filtered by agent (games don't have agents)
      if (result.type === 'game') return false;

      // PDF results: not currently filterable by agent
      if (result.type === 'pdf') return false;
    }

    // Filter by date range
    if (filters.dateFrom && result.timestamp) {
      if (result.timestamp < filters.dateFrom) return false;
    }
    if (filters.dateTo && result.timestamp) {
      if (result.timestamp > filters.dateTo) return false;
    }

    return true;
  });
}

/**
 * Load recent searches from localStorage
 */
function loadRecentSearches(): RecentSearch[] {
  try {
    const stored = localStorage.getItem(RECENT_SEARCHES_KEY);
    if (!stored) return [];
    const searches = JSON.parse(stored) as RecentSearch[];
    // Convert timestamp strings back to Date objects
    return searches.map(s => ({
      ...s,
      timestamp: new Date(s.timestamp),
      filters: s.filters
        ? {
            ...s.filters,
            dateFrom: s.filters.dateFrom ? new Date(s.filters.dateFrom) : undefined,
            dateTo: s.filters.dateTo ? new Date(s.filters.dateTo) : undefined,
          }
        : undefined,
    }));
  } catch {
    return [];
  }
}

/**
 * Save recent searches to localStorage
 */
function saveRecentSearches(searches: RecentSearch[]): void {
  try {
    localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(searches));
  } catch {
    // Ignore localStorage errors (e.g., quota exceeded)
  }
}

/**
 * Advanced search hook with Fuse.js fuzzy matching
 */
export function useSearch(dataSources: SearchDataSources) {
  const [query, setQuery] = useState('');
  const [filters, setFilters] = useState<SearchFilters>({});
  const [recentSearches, setRecentSearches] = useState<RecentSearch[]>([]);

  // Load recent searches on mount
  useEffect(() => {
    setRecentSearches(loadRecentSearches());
  }, []);

  // Build search index from data sources
  const searchIndex = useMemo(() => {
    return buildSearchIndex(dataSources);
  }, [dataSources]);

  // Create Fuse instance with search index
  const fuse = useMemo(() => {
    return new Fuse(searchIndex, FUSE_OPTIONS);
  }, [searchIndex]);

  // Perform search with fuzzy matching
  const search = useCallback(
    (options: SearchOptions): SearchResult[] => {
      const { query: searchQuery, filters: searchFilters, limit = 50, threshold } = options;

      // If no query, return filtered results
      if (!searchQuery || searchQuery.trim() === '') {
        const filtered = applyFilters(searchIndex, searchFilters);
        return filtered
          .sort((a, b) => {
            // Sort by timestamp (newest first)
            if (!a.timestamp) return 1;
            if (!b.timestamp) return -1;
            return b.timestamp.getTime() - a.timestamp.getTime();
          })
          .slice(0, limit);
      }

      // Apply custom threshold if provided
      const fuseInstance =
        threshold !== undefined ? new Fuse(searchIndex, { ...FUSE_OPTIONS, threshold }) : fuse;

      // Perform fuzzy search
      const fuseResults = fuseInstance.search(searchQuery);

      // Convert Fuse results to SearchResult[]
      let results = fuseResults.map(result => ({
        ...result.item,
        relevanceScore: 1 - (result.score || 0), // Invert score (higher is better)
      }));

      // Apply filters
      results = applyFilters(results, searchFilters);

      // Limit results
      return results.slice(0, limit);
    },
    [searchIndex, fuse]
  );

  // Add search to recent history
  const addToRecentSearches = useCallback(
    (searchQuery: string, searchFilters: SearchFilters, resultCount: number) => {
      if (!searchQuery.trim()) return;

      const newSearch: RecentSearch = {
        id: `${Date.now()}-${Math.random()}`,
        query: searchQuery,
        filters: searchFilters,
        timestamp: new Date(),
        resultCount,
      };

      setRecentSearches(prev => {
        // Remove duplicates (same query and filters)
        const filtered = prev.filter(
          s =>
            s.query !== searchQuery || JSON.stringify(s.filters) !== JSON.stringify(searchFilters)
        );
        // Add new search at the beginning
        const updated = [newSearch, ...filtered].slice(0, MAX_RECENT_SEARCHES);
        saveRecentSearches(updated);
        return updated;
      });
    },
    []
  );

  // Clear recent searches
  const clearRecentSearches = useCallback(() => {
    setRecentSearches([]);
    try {
      localStorage.removeItem(RECENT_SEARCHES_KEY);
    } catch {
      // Ignore
    }
  }, []);

  // Remove specific recent search
  const removeRecentSearch = useCallback((id: string) => {
    setRecentSearches(prev => {
      const updated = prev.filter(s => s.id !== id);
      saveRecentSearches(updated);
      return updated;
    });
  }, []);

  return {
    query,
    setQuery,
    filters,
    setFilters,
    search,
    recentSearches,
    addToRecentSearches,
    clearRecentSearches,
    removeRecentSearch,
  };
}
