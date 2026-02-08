/**
 * Play Records API Client
 *
 * Type-safe API client for Play Records endpoints.
 * Issue #3892: Frontend integration for play records
 *
 * Backend: GameManagement bounded context
 * Endpoints: /api/v1/game-management/play-records
 */

import type {
  PlayRecordDto,
  PlayRecordSummary,
  PlayHistoryResponse,
  PlayerStatistics,
  CreatePlayRecordRequest,
  AddPlayerRequest,
  RecordScoreRequest,
  UpdatePlayRecordRequest,
} from '@/lib/api/schemas/play-records.schemas';

const BASE_URL = '/api/v1/game-management/play-records';

/**
 * Play Records API Client
 */
export const playRecordsApi = {
  // ========== Commands ==========

  /**
   * Create a new play record
   */
  async createRecord(data: CreatePlayRecordRequest): Promise<string> {
    const res = await fetch(BASE_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to create record' }));
      throw new Error(error.message || 'Failed to create record');
    }
    return res.json();
  },

  /**
   * Add a player to an existing record
   */
  async addPlayer(recordId: string, player: AddPlayerRequest): Promise<void> {
    const res = await fetch(`${BASE_URL}/${recordId}/players`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(player),
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to add player' }));
      throw new Error(error.message || 'Failed to add player');
    }
  },

  /**
   * Record a score for a player
   */
  async recordScore(recordId: string, score: RecordScoreRequest): Promise<void> {
    const res = await fetch(`${BASE_URL}/${recordId}/scores`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(score),
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to record score' }));
      throw new Error(error.message || 'Failed to record score');
    }
  },

  /**
   * Start a play record (mark as InProgress)
   */
  async startRecord(recordId: string): Promise<void> {
    const res = await fetch(`${BASE_URL}/${recordId}/start`, {
      method: 'POST',
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to start record' }));
      throw new Error(error.message || 'Failed to start record');
    }
  },

  /**
   * Complete a play record
   */
  async completeRecord(recordId: string): Promise<void> {
    const res = await fetch(`${BASE_URL}/${recordId}/complete`, {
      method: 'POST',
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to complete record' }));
      throw new Error(error.message || 'Failed to complete record');
    }
  },

  /**
   * Update play record details
   */
  async updateRecord(recordId: string, updates: UpdatePlayRecordRequest): Promise<void> {
    const res = await fetch(`${BASE_URL}/${recordId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(updates),
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to update record' }));
      throw new Error(error.message || 'Failed to update record');
    }
  },

  // ========== Queries ==========

  /**
   * Get full play record details
   */
  async getRecord(id: string): Promise<PlayRecordDto> {
    const res = await fetch(`${BASE_URL}/${id}`);
    if (!res.ok) {
      if (res.status === 404) throw new Error('Play record not found');
      const error = await res.json().catch(() => ({ message: 'Failed to get record' }));
      throw new Error(error.message || 'Failed to get record');
    }
    return res.json();
  },

  /**
   * Get user's play history with pagination and filters
   */
  async getUserHistory(params: {
    page?: number;
    pageSize?: number;
    gameId?: string;
    status?: string;
    dateFrom?: string;
    dateTo?: string;
  } = {}): Promise<PlayHistoryResponse> {
    const searchParams = new URLSearchParams();
    if (params.page) searchParams.set('page', params.page.toString());
    if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());
    if (params.gameId) searchParams.set('gameId', params.gameId);
    if (params.status && params.status !== 'all') searchParams.set('status', params.status);
    if (params.dateFrom) searchParams.set('dateFrom', params.dateFrom);
    if (params.dateTo) searchParams.set('dateTo', params.dateTo);

    const res = await fetch(`${BASE_URL}/history?${searchParams.toString()}`);
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to get history' }));
      throw new Error(error.message || 'Failed to get history');
    }
    return res.json();
  },

  /**
   * Get player statistics across all games
   */
  async getPlayerStatistics(): Promise<PlayerStatistics> {
    const res = await fetch(`${BASE_URL}/statistics`);
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to get statistics' }));
      throw new Error(error.message || 'Failed to get statistics');
    }
    return res.json();
  },
};
