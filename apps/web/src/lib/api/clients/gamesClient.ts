/**
 * Games Client (FE-IMP-005)
 *
 * Modular client for GameManagement bounded context.
 * Covers: Games CRUD, filtering, sorting, pagination, documents
 */

import type { HttpClient } from '../core/httpClient';
import {
  GameSchema,
  PaginatedGamesResponseSchema,
  PdfDocumentDtoSchema,
  type Game,
  type PaginatedGamesResponse,
  type PdfDocumentDto,
} from '../schemas';

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

export type GameSortField = 'title' | 'yearPublished' | 'minPlayers' | 'maxPlayers' | 'minPlayTimeMinutes' | 'maxPlayTimeMinutes';
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
      // Fetch all games from backend
      const allGamesRaw = await httpClient.get<Game[]>('/api/v1/games');
      const allGames = allGamesRaw ?? [];

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
          if (filters.minPlayers !== undefined && game.maxPlayers !== null && game.maxPlayers < filters.minPlayers) {
            return false;
          }
          if (filters.maxPlayers !== undefined && game.minPlayers !== null && game.minPlayers > filters.maxPlayers) {
            return false;
          }

          // Play time filters
          if (filters.minPlayTime !== undefined && game.maxPlayTimeMinutes !== null && game.maxPlayTimeMinutes < filters.minPlayTime) {
            return false;
          }
          if (filters.maxPlayTime !== undefined && game.minPlayTimeMinutes !== null && game.minPlayTimeMinutes > filters.maxPlayTime) {
            return false;
          }

          // Year filters
          if (filters.yearFrom !== undefined && game.yearPublished !== null && game.yearPublished < filters.yearFrom) {
            return false;
          }
          if (filters.yearTo !== undefined && game.yearPublished !== null && game.yearPublished > filters.yearTo) {
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
     * Get all agents for a specific game
     * @param gameId Game ID
     */
    async getAgents(gameId: string): Promise<Array<{ id: string; gameId: string; name: string; kind: string; createdAt: string }> | null> {
      return httpClient.get<Array<{ id: string; gameId: string; name: string; kind: string; createdAt: string }>>(
        `/api/v1/games/${gameId}/agents`
      );
    },
  };
}

export type GamesClient = ReturnType<typeof createGamesClient>;
