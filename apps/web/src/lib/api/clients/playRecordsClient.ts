/**
 * Play Records API Client
 * Issue #3892: Frontend UI + Statistics Dashboard
 */

import {
  PlayRecordDtoSchema,
  PlayHistoryResponseSchema,
  PlayerStatisticsSchema,
  type PlayRecordDto,
  type PlayHistoryResponse,
  type PlayerStatistics,
  type CreatePlayRecordRequest,
  type AddPlayerRequest,
  type RecordScoreRequest,
  type UpdatePlayRecordRequest,
} from '../schemas/play-records.schemas';

import type { HttpClient } from '../core/httpClient';

export interface PlayRecordsClient {
  create(data: CreatePlayRecordRequest): Promise<string>;
  addPlayer(recordId: string, data: AddPlayerRequest): Promise<void>;
  recordScore(recordId: string, data: RecordScoreRequest): Promise<void>;
  start(recordId: string): Promise<void>;
  complete(recordId: string, data?: { manualDuration?: string }): Promise<void>;
  update(recordId: string, data: UpdatePlayRecordRequest): Promise<void>;
  getById(recordId: string): Promise<PlayRecordDto>;
  getHistory(params?: {
    page?: number;
    pageSize?: number;
    gameId?: string;
  }): Promise<PlayHistoryResponse>;
  getStatistics(params?: { startDate?: string; endDate?: string }): Promise<PlayerStatistics>;
}

export function createPlayRecordsClient({
  httpClient,
}: {
  httpClient: HttpClient;
}): PlayRecordsClient {
  return {
    async create(input) {
      return await httpClient.post<string>('/api/v1/play-records', input);
    },

    async addPlayer(recordId, input) {
      await httpClient.post(`/api/v1/play-records/${recordId}/players`, input);
    },

    async recordScore(recordId, input) {
      await httpClient.post(`/api/v1/play-records/${recordId}/scores`, input);
    },

    async start(recordId) {
      await httpClient.post(`/api/v1/play-records/${recordId}/start`, {});
    },

    async complete(recordId, data = {}) {
      await httpClient.post(`/api/v1/play-records/${recordId}/complete`, data);
    },

    async update(recordId, input) {
      await httpClient.put(`/api/v1/play-records/${recordId}`, input);
    },

    async getById(recordId) {
      const data = await httpClient.get<PlayRecordDto>(`/api/v1/play-records/${recordId}`);
      return PlayRecordDtoSchema.parse(data);
    },

    async getHistory(params) {
      const query = new URLSearchParams();
      if (params?.page !== undefined) query.set('Page', String(params.page));
      if (params?.pageSize !== undefined) query.set('PageSize', String(params.pageSize));
      if (params?.gameId) query.set('GameId', params.gameId);
      const qs = query.toString();
      const url = qs ? `/api/v1/play-records/history?${qs}` : '/api/v1/play-records/history';
      const data = await httpClient.get<PlayHistoryResponse>(url);
      return PlayHistoryResponseSchema.parse(data);
    },

    async getStatistics(params) {
      const query = new URLSearchParams();
      if (params?.startDate) query.set('StartDate', params.startDate);
      if (params?.endDate) query.set('EndDate', params.endDate);
      const qs = query.toString();
      const url = qs ? `/api/v1/play-records/statistics?${qs}` : '/api/v1/play-records/statistics';
      const data = await httpClient.get<PlayerStatistics>(url);
      return PlayerStatisticsSchema.parse(data);
    },
  };
}
