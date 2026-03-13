/**
 * Session Invite API Client (Game Night Improvvisata)
 *
 * Maps to SessionInviteEndpoints.cs (6 endpoints).
 * Handles invite creation, session joining, participants, agent access, and score proposal/confirm.
 */

import { z } from 'zod';

import {
  SessionInviteResultSchema,
  JoinSessionResultSchema,
  SessionParticipantDtoSchema,
  type SessionInviteResult,
  type JoinSessionResult,
  type SessionParticipantDto,
  type CreateInviteRequest,
  type JoinSessionRequest,
  type ToggleAgentAccessRequest,
  type ProposeScoreProposalRequest,
  type ConfirmScoreProposalRequest,
} from '../schemas/session-invite.schemas';

import type { HttpClient } from '../core/httpClient';

const BASE = '/api/v1/live-sessions';

export interface SessionInviteClient {
  /** Create an invite link/PIN for a session */
  createInvite(sessionId: string, request: CreateInviteRequest): Promise<SessionInviteResult>;

  /** Join a session via token (no auth required for guests) */
  joinSession(request: JoinSessionRequest): Promise<JoinSessionResult>;

  /** Get all participants for a session */
  getParticipants(sessionId: string): Promise<SessionParticipantDto[]>;

  /** Toggle agent access for a participant */
  toggleAgentAccess(
    sessionId: string,
    participantId: string,
    request: ToggleAgentAccessRequest
  ): Promise<void>;

  /** Propose a score (requires confirmation from host) */
  proposeScore(sessionId: string, request: ProposeScoreProposalRequest): Promise<void>;

  /** Confirm a proposed score */
  confirmScore(sessionId: string, request: ConfirmScoreProposalRequest): Promise<void>;
}

export function createSessionInviteClient({
  httpClient,
}: {
  httpClient: HttpClient;
}): SessionInviteClient {
  return {
    async createInvite(sessionId, request) {
      const response = await httpClient.post<SessionInviteResult>(
        `${BASE}/${encodeURIComponent(sessionId)}/invite`,
        request
      );
      if (!response) throw new Error('Failed to create invite');
      return SessionInviteResultSchema.parse(response);
    },

    async joinSession(request) {
      const response = await httpClient.post<JoinSessionResult>(`${BASE}/join`, request);
      if (!response) throw new Error('Failed to join session');
      return JoinSessionResultSchema.parse(response);
    },

    async getParticipants(sessionId) {
      const response = await httpClient.get<SessionParticipantDto[]>(
        `${BASE}/${encodeURIComponent(sessionId)}/participants`
      );
      return z.array(SessionParticipantDtoSchema).parse(response ?? []);
    },

    async toggleAgentAccess(sessionId, participantId, request) {
      await httpClient.put(
        `${BASE}/${encodeURIComponent(sessionId)}/participants/${encodeURIComponent(participantId)}/agent-access`,
        request
      );
    },

    async proposeScore(sessionId, request) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/scores/propose`, request);
    },

    async confirmScore(sessionId, request) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/scores/confirm`, request);
    },
  };
}
