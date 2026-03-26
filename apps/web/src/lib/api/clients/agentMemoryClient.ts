/**
 * Agent Memory API Client
 *
 * Client for AgentMemory bounded context endpoints.
 * Covers: group memory, game memory (house rules, notes), player stats, guest claiming.
 */

import { z } from 'zod';

import type { HttpClient } from '../core/httpClient';

// --- Zod Schemas ---

export const HouseRuleDtoSchema = z.object({
  description: z.string(),
  addedAt: z.string(),
  source: z.string(),
});

export const MemoryNoteDtoSchema = z.object({
  content: z.string(),
  addedAt: z.string(),
});

export const GameMemoryDtoSchema = z.object({
  id: z.string(),
  gameId: z.string(),
  ownerId: z.string(),
  houseRules: z.array(HouseRuleDtoSchema),
  notes: z.array(MemoryNoteDtoSchema),
});

export const GroupMemberDtoSchema = z.object({
  userId: z.string().nullable(),
  guestName: z.string().nullable(),
  joinedAt: z.string(),
});

export const GroupPreferencesDtoSchema = z.object({
  maxDuration: z.string().nullable(),
  preferredComplexity: z.string().nullable(),
  customNotes: z.string().nullable(),
});

export const GroupStatsDtoSchema = z.object({
  totalSessions: z.number(),
  gamePlayCounts: z.record(z.string(), z.number()),
  lastPlayedAt: z.string().nullable(),
});

export const GroupMemoryDtoSchema = z.object({
  id: z.string(),
  name: z.string(),
  members: z.array(GroupMemberDtoSchema),
  preferences: GroupPreferencesDtoSchema.nullable(),
  stats: GroupStatsDtoSchema.nullable(),
});

export const PlayerGameStatsDtoSchema = z.object({
  gameId: z.string(),
  wins: z.number(),
  losses: z.number(),
  totalPlayed: z.number(),
  bestScore: z.number().nullable(),
});

export const PlayerMemoryDtoSchema = z.object({
  id: z.string(),
  groupId: z.string().nullable(),
  gameStats: z.array(PlayerGameStatsDtoSchema),
  claimedAt: z.string().nullable(),
});

export const ClaimableGuestDtoSchema = z.object({
  playerMemoryId: z.string(),
  guestName: z.string(),
  groupId: z.string().nullable(),
  groupName: z.string().nullable(),
});

// --- Types ---

export type HouseRuleDto = z.infer<typeof HouseRuleDtoSchema>;
export type MemoryNoteDto = z.infer<typeof MemoryNoteDtoSchema>;
export type GameMemoryDto = z.infer<typeof GameMemoryDtoSchema>;
export type GroupMemberDto = z.infer<typeof GroupMemberDtoSchema>;
export type GroupPreferencesDto = z.infer<typeof GroupPreferencesDtoSchema>;
export type GroupStatsDto = z.infer<typeof GroupStatsDtoSchema>;
export type GroupMemoryDto = z.infer<typeof GroupMemoryDtoSchema>;
export type PlayerGameStatsDto = z.infer<typeof PlayerGameStatsDtoSchema>;
export type PlayerMemoryDto = z.infer<typeof PlayerMemoryDtoSchema>;
export type ClaimableGuestDto = z.infer<typeof ClaimableGuestDtoSchema>;

// --- Client ---

export interface CreateAgentMemoryClientParams {
  httpClient: HttpClient;
}

export type AgentMemoryClient = ReturnType<typeof createAgentMemoryClient>;

/**
 * Agent Memory API client with Zod validation
 */
export function createAgentMemoryClient({ httpClient }: CreateAgentMemoryClientParams) {
  const BASE = '/api/v1/agent-memory';

  return {
    // === Group Memory ===

    async getGroup(groupId: string): Promise<GroupMemoryDto | null> {
      return httpClient.get(`${BASE}/groups/${groupId}`, GroupMemoryDtoSchema);
    },

    // === Game Memory ===

    async getGameMemory(gameId: string): Promise<GameMemoryDto | null> {
      return httpClient.get(`${BASE}/games/${gameId}/memory`, GameMemoryDtoSchema);
    },

    async addHouseRule(gameId: string, description: string): Promise<void> {
      await httpClient.post(`${BASE}/games/${gameId}/memory/house-rules`, { description });
    },

    async addNote(gameId: string, content: string): Promise<void> {
      await httpClient.post(`${BASE}/games/${gameId}/memory/notes`, { content });
    },

    // === Player Stats ===

    async getMyStats(): Promise<PlayerMemoryDto[]> {
      const result = await httpClient.get(
        `${BASE}/players/me/stats`,
        z.array(PlayerMemoryDtoSchema)
      );
      return result ?? [];
    },

    async getClaimableGuests(guestName: string): Promise<ClaimableGuestDto[]> {
      const result = await httpClient.get(
        `${BASE}/players/me/claimable-guests?guestName=${encodeURIComponent(guestName)}`,
        z.array(ClaimableGuestDtoSchema)
      );
      return result ?? [];
    },

    async claimGuest(playerMemoryId: string): Promise<void> {
      await httpClient.post(`${BASE}/players/me/claim-guest`, { playerMemoryId });
    },
  };
}
