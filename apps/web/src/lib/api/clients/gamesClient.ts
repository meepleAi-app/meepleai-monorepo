/**
 * Games Client (FE-IMP-005)
 *
 * Modular client for GameManagement bounded context.
 * Covers: Games CRUD, filtering, sorting, pagination, documents
 */

import type { HttpClient } from '../core/httpClient';
import { z } from 'zod';
import {
  GameSchema,
  GameSessionDtoSchema,
  PaginatedGamesResponseSchema,
  PdfDocumentDtoSchema,
  RuleSpecSchema,
  RuleSpecHistorySchema,
  VersionTimelineSchema,
  RuleSpecDiffSchema,
  type Game,
  type GameSessionDto,
  type PaginatedGamesResponse,
  type PdfDocumentDto,
  type RuleSpec,
  type RuleSpecHistory,
  type VersionTimeline,
  type RuleSpecDiff,
} from '../schemas';
import { AgentDtoSchema, type AgentDto } from '../schemas';

export interface CreateGamesClientParams {
  httpClient: HttpClient;
}

export interface GameFilters {
  search?: string;
  minPlayers?: number;
  maxPlayers?: number;
  minPlayTime?: number;
  maxPlayTime?: number;
  yearFrom?: number;
  yearTo?: number;
  bggOnly?: boolean;
}

export type GameSortField =
  | 'title'
  | 'yearPublished'
  | 'minPlayers'
  | 'maxPlayers'
  | 'minPlayTimeMinutes'
  | 'maxPlayTimeMinutes';
export type SortDirection = 'asc' | 'desc';

export interface GameSortOptions {
  field: GameSortField;
  direction: SortDirection;
}

/**
 * Games API client with Zod validation
 */
export function createGamesClient({ httpClient }: CreateGamesClientParams) {
  return {
    // ========== Games CRUD ==========

    /**
     * Get all games with optional filtering, sorting, and pagination
     * Note: Filtering, sorting, and pagination are done client-side for MVP
     * @param filters Optional filters (search, players, playtime, year, BGG)
     * @param sort Optional sorting (field and direction)
     * @param page Page number (1-indexed)
     * @param pageSize Number of games per page
     */
    async getAll(
      filters?: GameFilters,
      sort?: GameSortOptions,
      page: number = 1,
      pageSize: number = 20
    ): Promise<PaginatedGamesResponse> {
      // Fetch all games from backend with Zod validation
      // Backend serializes to camelCase (Program.cs:204 - JsonNamingPolicy.CamelCase)
      const allGames: Game[] = (await httpClient.get('/api/v1/games', z.array(GameSchema))) ?? [];

      // Client-side filtering
      let filtered = allGames;

      if (filters) {
        filtered = filtered.filter(game => {
          // Search filter (title or publisher)
          if (filters.search) {
            const searchLower = filters.search.toLowerCase();
            const titleMatch = game.title.toLowerCase().includes(searchLower);
            const publisherMatch = game.publisher?.toLowerCase().includes(searchLower) || false;
            if (!titleMatch && !publisherMatch) {
              return false;
            }
          }

          // Player count filters
          if (
            filters.minPlayers !== undefined &&
            game.maxPlayers !== null &&
            game.maxPlayers < filters.minPlayers
          ) {
            return false;
          }
          if (
            filters.maxPlayers !== undefined &&
            game.minPlayers !== null &&
            game.minPlayers > filters.maxPlayers
          ) {
            return false;
          }

          // Play time filters
          if (
            filters.minPlayTime !== undefined &&
            game.maxPlayTimeMinutes !== null &&
            game.maxPlayTimeMinutes < filters.minPlayTime
          ) {
            return false;
          }
          if (
            filters.maxPlayTime !== undefined &&
            game.minPlayTimeMinutes !== null &&
            game.minPlayTimeMinutes > filters.maxPlayTime
          ) {
            return false;
          }

          // Year filters
          if (
            filters.yearFrom !== undefined &&
            game.yearPublished !== null &&
            game.yearPublished < filters.yearFrom
          ) {
            return false;
          }
          if (
            filters.yearTo !== undefined &&
            game.yearPublished !== null &&
            game.yearPublished > filters.yearTo
          ) {
            return false;
          }

          // BGG-only filter
          if (filters.bggOnly && !game.bggId) {
            return false;
          }

          return true;
        });
      }

      // Client-side sorting
      if (sort) {
        filtered = [...filtered].sort((a, b) => {
          const aVal = a[sort.field];
          const bVal = b[sort.field];

          // Handle null values (push to end)
          if (aVal === null && bVal === null) return 0;
          if (aVal === null) return 1;
          if (bVal === null) return -1;

          // Sort by field
          let comparison = 0;
          if (sort.field === 'title') {
            comparison = aVal.toString().localeCompare(bVal.toString());
          } else {
            comparison = (aVal as number) - (bVal as number);
          }

          return sort.direction === 'asc' ? comparison : -comparison;
        });
      }

      // Client-side pagination
      const total = filtered.length;
      const totalPages = Math.ceil(total / pageSize);
      const startIndex = (page - 1) * pageSize;
      const endIndex = startIndex + pageSize;
      const paginatedGames = filtered.slice(startIndex, endIndex);

      return {
        games: paginatedGames,
        total,
        page,
        pageSize,
        totalPages,
      };
    },

    /**
     * Get a single game by ID
     * @param id Game ID (GUID format)
     */
    async getById(id: string): Promise<Game | null> {
      return httpClient.get(`/api/v1/games/${encodeURIComponent(id)}`, GameSchema);
    },

    /**
     * Get all sessions for a specific game with optional pagination
     * Issue #1675: FE-IMP-005 - Connect frontend to backend sessions API
     * @param gameId Game ID (GUID format)
     * @param pageNumber Optional page number (1-indexed)
     * @param pageSize Optional page size (default determined by backend)
     */
    async getSessions(
      gameId: string,
      pageNumber?: number,
      pageSize?: number
    ): Promise<GameSessionDto[]> {
      const params = new URLSearchParams();
      if (pageNumber) params.append('pageNumber', pageNumber.toString());
      if (pageSize) params.append('pageSize', pageSize.toString());
      const queryString = params.toString() ? `?${params.toString()}` : '';
      const url = `/api/v1/games/${encodeURIComponent(gameId)}/sessions${queryString}`;
      const response = await httpClient.get(url, z.array(GameSessionDtoSchema));
      return response ?? [];
    },

    /**
     * Get PDF documents for a specific game
     * @param gameId Game ID (GUID format)
     */
    async getDocuments(gameId: string): Promise<PdfDocumentDto[]> {
      const response = await httpClient.get<PdfDocumentDto[]>(
        `/api/v1/games/${encodeURIComponent(gameId)}/documents`
      );
      return response ?? [];
    },

    /**
     * Create new game
     * POST /api/v1/games
     */
    async create(name: string): Promise<{ id: string; title: string; createdAt: string }> {
      return httpClient.post<{ id: string; title: string; createdAt: string }>('/api/v1/games', {
        name,
      });
    },

    // ========== RuleSpec Management ==========

    /**
     * Get RuleSpec for a game
     * GET /api/v1/games/{gameId}/rulespec
     *
     * Issue #1977: Added RuleSpecSchema validation
     */
    async getRuleSpec(gameId: string): Promise<RuleSpec | null> {
      return httpClient.get(`/api/v1/games/${encodeURIComponent(gameId)}/rulespec`, RuleSpecSchema);
    },

    /**
     * Get specific RuleSpec version
     * GET /api/v1/games/{gameId}/rulespec/versions/{version}
     *
     * Issue #1977: Added RuleSpecSchema validation
     */
    async getRuleSpecVersion(gameId: string, version: number): Promise<RuleSpec | null> {
      return httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/versions/${version}`,
        RuleSpecSchema
      );
    },

    /**
     * Get RuleSpec version history
     * GET /api/v1/games/{gameId}/rulespec/history
     *
     * Issue #1977: Added RuleSpecHistorySchema validation
     */
    async getRuleSpecHistory(gameId: string): Promise<RuleSpecHistory | null> {
      return httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/history`,
        RuleSpecHistorySchema
      );
    },

    /**
     * Get RuleSpec version timeline with authors
     * GET /api/v1/games/{gameId}/rulespec/versions/timeline
     *
     * Issue #1977: Added VersionTimelineSchema validation
     */
    async getRuleSpecTimeline(gameId: string): Promise<VersionTimeline | null> {
      return httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/versions/timeline`,
        VersionTimelineSchema
      );
    },

    /**
     * Update RuleSpec for a game
     * PUT /api/v1/games/{gameId}/rulespec
     *
     * Issue #1977: Added RuleSpecSchema validation
     */
    async updateRuleSpec(gameId: string, ruleSpecData: Partial<RuleSpec>): Promise<RuleSpec> {
      const result = await httpClient.put(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec`,
        ruleSpecData,
        RuleSpecSchema
      );
      if (!result) {
        throw new Error('Failed to update RuleSpec: no response from server');
      }
      return result;
    },

    /**
     * Get AI agents available for a game
     * GET /api/v1/games/{gameId}/agents
     *
     * Issue #1977: Added AgentDtoSchema validation
     */
    async getAgents(gameId: string): Promise<AgentDto[]> {
      const result = await httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/agents`,
        AgentDtoSchema.array()
      );
      return result ?? [];
    },

    /**
     * Get diff between two RuleSpec versions
     * GET /api/v1/games/{gameId}/rulespec/diff?from={from}&to={to}
     *
     * Issue #1977: Added RuleSpecDiffSchema validation
     */
    async getRuleSpecDiff(
      gameId: string,
      fromVersion: number | string,
      toVersion: number | string
    ): Promise<RuleSpecDiff | null> {
      return httpClient.get(
        `/api/v1/games/${encodeURIComponent(gameId)}/rulespec/diff?from=${encodeURIComponent(fromVersion)}&to=${encodeURIComponent(toVersion)}`,
        RuleSpecDiffSchema
      );
    },
  };
}

export type GamesClient = ReturnType<typeof createGamesClient>;
