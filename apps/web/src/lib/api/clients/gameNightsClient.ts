/**
 * Game Nights API Client
 * Issue #33 — P3 Game Night Frontend
 */

import { z } from 'zod';

import {
  ConflictCheckDtoSchema,
  GameNightDtoSchema,
  GameNightRsvpDtoSchema,
  RegularDtoSchema,
  type ConflictCheckDto,
  type CreateGameNightInput,
  type GameNightDto,
  type GameNightRsvpDto,
  type RegularDto,
  type RsvpStatus,
  type UpdateGameNightInput,
} from '../schemas/game-nights.schemas';

import type { HttpClient } from '../core/httpClient';

export interface GameNightsClient {
  getUpcoming(): Promise<GameNightDto[]>;
  getMine(): Promise<GameNightDto[]>;
  getById(id: string): Promise<GameNightDto>;
  getRsvps(id: string): Promise<GameNightRsvpDto[]>;
  create(data: CreateGameNightInput): Promise<string>;
  update(id: string, data: UpdateGameNightInput): Promise<void>;
  publish(id: string): Promise<void>;
  cancel(id: string): Promise<void>;
  invite(id: string, userIds: string[]): Promise<void>;
  rsvp(id: string, response: RsvpStatus): Promise<void>;
  // Issue #950 W1-PR2 wizard hooks
  getRegulars(limit?: number): Promise<RegularDto[]>;
  checkConflict(at: string): Promise<ConflictCheckDto>;
}

export function createGameNightsClient({
  httpClient,
}: {
  httpClient: HttpClient;
}): GameNightsClient {
  return {
    async getUpcoming() {
      const data = await httpClient.get<GameNightDto[]>('/api/v1/game-nights');
      return z.array(GameNightDtoSchema).parse(data ?? []);
    },

    async getMine() {
      const data = await httpClient.get<GameNightDto[]>('/api/v1/game-nights/mine');
      return z.array(GameNightDtoSchema).parse(data ?? []);
    },

    async getById(id) {
      const data = await httpClient.get<GameNightDto>(`/api/v1/game-nights/${id}`);
      return GameNightDtoSchema.parse(data);
    },

    async getRsvps(id) {
      const data = await httpClient.get<GameNightRsvpDto[]>(`/api/v1/game-nights/${id}/rsvps`);
      return z.array(GameNightRsvpDtoSchema).parse(data ?? []);
    },

    async create(input) {
      return await httpClient.post<string>('/api/v1/game-nights', input);
    },

    async update(id, input) {
      await httpClient.put(`/api/v1/game-nights/${id}`, input);
    },

    async publish(id) {
      await httpClient.post(`/api/v1/game-nights/${id}/publish`, {});
    },

    async cancel(id) {
      await httpClient.post(`/api/v1/game-nights/${id}/cancel`, {});
    },

    async invite(id, userIds) {
      await httpClient.post(`/api/v1/game-nights/${id}/invite`, { invitedUserIds: userIds });
    },

    async rsvp(id, response) {
      await httpClient.post(`/api/v1/game-nights/${id}/rsvp`, { response });
    },

    async getRegulars(limit) {
      const query = limit !== undefined ? `?limit=${limit}` : '';
      const data = await httpClient.get<RegularDto[]>(`/api/v1/game-nights/regulars${query}`);
      return z.array(RegularDtoSchema).parse(data ?? []);
    },

    async checkConflict(at) {
      const data = await httpClient.get<ConflictCheckDto>(
        `/api/v1/game-nights/check-conflict?at=${encodeURIComponent(at)}`
      );
      return ConflictCheckDtoSchema.parse(data);
    },
  };
}
