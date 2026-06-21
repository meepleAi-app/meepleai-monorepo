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
  PlayHistoryResponse,
  PlayerStatistics,
  CreatePlayRecordRequest,
  AddPlayerRequest,
  RecordScoreRequest,
  UpdatePlayRecordRequest,
} from '@/lib/api/schemas/play-records.schemas';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const BASE_URL = `${API_BASE}/api/v1/play-records`;

// #2437-1: Typed error for updateRecord — consumers can narrow on `kind` for conflict UI.
export class UpdatePlayRecordError extends Error {
  constructor(
    message: string,
    public readonly kind: 'conflict' | 'forbidden' | 'validation' | 'server',
    public readonly status: number,
    public readonly warningCode?: string
  ) {
    super(message);
    this.name = 'UpdatePlayRecordError';
  }
}

export interface UploadPlayRecordPhotoResult {
  photoId: string;
  photoUrl: string;
  thumbnailUrl: string | null;
  ocrText: string | null;
  wasDeduplicated: boolean;
}

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
      credentials: 'include',
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
      credentials: 'include',
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
      credentials: 'include',
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
      credentials: 'include',
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
      credentials: 'include',
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to complete record' }));
      throw new Error(error.message || 'Failed to complete record');
    }
  },

  /**
   * Update play record details.
   * #2437-1: Sends optional `xmin` for optimistic-concurrency; throws `UpdatePlayRecordError`
   * with `kind='conflict'` on 409 + `X-Warning-Code: concurrent-edit`.
   */
  async updateRecord(recordId: string, updates: UpdatePlayRecordRequest): Promise<void> {
    const res = await fetch(`${BASE_URL}/${recordId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(updates),
    });
    if (res.ok) return;
    const warningCode = res.headers.get('X-Warning-Code') ?? undefined;
    if (res.status === 409) {
      throw new UpdatePlayRecordError(
        'Modifica concorrente rilevata.',
        'conflict',
        409,
        warningCode
      );
    }
    if (res.status === 403) {
      throw new UpdatePlayRecordError(
        'Non hai i permessi per modificare.',
        'forbidden',
        403,
        warningCode
      );
    }
    if (res.status === 400) {
      throw new UpdatePlayRecordError('Dati non validi.', 'validation', 400, warningCode);
    }
    const err = await res.json().catch(() => ({ message: 'Failed to update record' }));
    throw new UpdatePlayRecordError(
      err.message || 'Failed to update record',
      'server',
      res.status,
      warningCode
    );
  },

  /**
   * Upload a photo to an existing play record (multipart). #2436 PR-C.
   * Raw fetch — httpClient does not support multipart FormData.
   */
  async uploadPhoto(
    recordId: string,
    file: Blob,
    opts: { caption?: string; extractScoreFromPhoto?: boolean } = {}
  ): Promise<UploadPlayRecordPhotoResult> {
    const form = new FormData();
    form.append('file', file, file instanceof File ? file.name : 'photo.jpg');
    if (opts.extractScoreFromPhoto) form.append('extractScoreFromPhoto', 'true');
    if (opts.caption) form.append('caption', opts.caption);

    const res = await fetch(`${BASE_URL}/${recordId}/photos`, {
      method: 'POST',
      body: form,
      credentials: 'include',
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to upload photo' }));
      throw new Error(error.error || error.message || 'Failed to upload photo');
    }
    return res.json();
  },

  // ========== Queries ==========

  /**
   * Get full play record details
   */
  async getRecord(id: string): Promise<PlayRecordDto> {
    const res = await fetch(`${BASE_URL}/${id}`, { credentials: 'include' });
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
  async getUserHistory(
    params: {
      page?: number;
      pageSize?: number;
      gameId?: string;
      status?: string;
      dateFrom?: string;
      dateTo?: string;
    } = {}
  ): Promise<PlayHistoryResponse> {
    const searchParams = new URLSearchParams();
    if (params.page) searchParams.set('page', params.page.toString());
    if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());
    if (params.gameId) searchParams.set('gameId', params.gameId);
    if (params.status && params.status !== 'all') searchParams.set('status', params.status);
    if (params.dateFrom) searchParams.set('dateFrom', params.dateFrom);
    if (params.dateTo) searchParams.set('dateTo', params.dateTo);

    const res = await fetch(`${BASE_URL}/history?${searchParams.toString()}`, {
      credentials: 'include',
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to get history' }));
      throw new Error(error.message || 'Failed to get history');
    }
    return res.json();
  },

  /**
   * Get player statistics across all games. Optional date range narrows the
   * window; the BE binds startDate/endDate case-insensitively (#2438).
   */
  async getPlayerStatistics(
    params: { startDate?: string; endDate?: string } = {}
  ): Promise<PlayerStatistics> {
    const search = new URLSearchParams();
    if (params.startDate) search.set('startDate', params.startDate);
    if (params.endDate) search.set('endDate', params.endDate);
    const qs = search.toString();
    const res = await fetch(`${BASE_URL}/statistics${qs ? `?${qs}` : ''}`, {
      credentials: 'include',
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to get statistics' }));
      throw new Error(error.message || 'Failed to get statistics');
    }
    return res.json();
  },

  /**
   * Delete a play record by ID (AC-4.6)
   */
  async deleteRecord(recordId: string): Promise<void> {
    const res = await fetch(`${BASE_URL}/${recordId}`, {
      method: 'DELETE',
      credentials: 'include',
    });
    if (!res.ok) {
      const error = await res.json().catch(() => ({ message: 'Failed to delete record' }));
      throw new Error(error.message || 'Failed to delete record');
    }
  },
};
