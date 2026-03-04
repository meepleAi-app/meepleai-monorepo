/**
 * Session Tracking API Client
 *
 * Issue #5041 — Sessions Redesign Phase 1
 *
 * Maps to SessionTrackingEndpoints.cs (legacy tools system).
 * Handles dice, cards, notes, chat, timer, media, and session tools.
 */

import { z } from 'zod';

import {
  SessionDtoSchema,
  SessionDetailsDtoSchema,
  ScoreEntryDtoSchema,
  ParticipantDtoSchema,
  PlayerNoteDtoSchema,
  ScoreboardDtoSchema,
  SessionSummaryDtoSchema,
  DiceRollResultDtoSchema,
  DiceRollHistoryDtoSchema,
  DeckDtoSchema,
  DrawnCardsDtoSchema,
  CardDtoSchema,
  GameSessionChatMessageDtoSchema,
  CoinFlipResultDtoSchema,
  WheelSpinResultDtoSchema,
  MediaDtoSchema,
  ShareableSessionDtoSchema,
  type SessionDto,
  type SessionDetailsDto,
  type ScoreEntryDto,
  type ParticipantDto,
  type PlayerNoteDto,
  type ScoreboardDto,
  type SessionSummaryDto,
  type DiceRollResultDto,
  type DiceRollHistoryDto,
  type DeckDto,
  type DrawnCardsDto,
  type CardDto,
  type GameSessionChatMessageDto,
  type CoinFlipResultDto,
  type WheelSpinResultDto,
  type MediaDto,
  type ShareableSessionDto,
  type CreateSessionCommand,
  type AddParticipantCommand,
  type UpdateScoreCommand,
  type RollDiceCommand,
  type CreateDeckCommand,
  type DrawCardsCommand,
  type SaveNoteCommand,
  type SendChatMessageCommand,
  type AskAgentCommand,
  type FinalizeSessionCommand,
} from '../schemas/session-tracking.schemas';

import type { HttpClient } from '../core/httpClient';

const BASE = '/api/v1/game-sessions';

export interface SessionTrackingClient {
  // ========== Session CRUD ==========

  /** Create a new game session */
  createSession(request: CreateSessionCommand): Promise<SessionDto>;

  /** Get session details */
  getSession(sessionId: string): Promise<SessionDetailsDto>;

  /** Get active session */
  getActiveSession(): Promise<SessionDto | null>;

  /** Get session by code */
  getByCode(code: string): Promise<SessionDto>;

  /** Join a session by code */
  joinByCode(code: string): Promise<void>;

  /** Get session history (paginated) */
  getHistory(options?: { limit?: number; offset?: number }): Promise<SessionSummaryDto[]>;

  /** Finalize a session */
  finalizeSession(sessionId: string, request?: FinalizeSessionCommand): Promise<void>;

  // ========== Participants ==========

  /** Add a participant */
  addParticipant(sessionId: string, request: AddParticipantCommand): Promise<ParticipantDto>;

  /** Mark player as ready */
  markPlayerReady(sessionId: string): Promise<void>;

  /** Kick a participant */
  kickParticipant(sessionId: string, participantId: string): Promise<void>;

  /** Assign role to participant */
  assignRole(sessionId: string, playerId: string, role: string): Promise<void>;

  // ========== Scores ==========

  /** Update a score */
  updateScore(sessionId: string, request: UpdateScoreCommand): Promise<ScoreEntryDto>;

  /** Update player score directly */
  updatePlayerScore(sessionId: string, playerId: string, score: number): Promise<void>;

  /** Get scoreboard */
  getScoreboard(sessionId: string): Promise<ScoreboardDto>;

  // ========== Dice ==========

  /** Roll dice for the session */
  rollDice(sessionId: string, request: RollDiceCommand): Promise<DiceRollResultDto>;

  /** Roll dice for a specific player */
  rollDiceForPlayer(
    sessionId: string,
    playerId: string,
    request: RollDiceCommand
  ): Promise<DiceRollResultDto>;

  /** Get dice roll history */
  getDiceHistory(sessionId: string): Promise<DiceRollHistoryDto[]>;

  // ========== Card Decks ==========

  /** Create a new card deck */
  createDeck(sessionId: string, request: CreateDeckCommand): Promise<DeckDto>;

  /** Get all decks */
  getDecks(sessionId: string): Promise<DeckDto[]>;

  /** Shuffle a deck */
  shuffleDeck(sessionId: string, deckId: string): Promise<void>;

  /** Draw cards from a deck */
  drawCards(sessionId: string, deckId: string, request: DrawCardsCommand): Promise<DrawnCardsDto>;

  /** Get hand (drawn cards) */
  getHand(sessionId: string, deckId: string): Promise<CardDto[]>;

  /** Get discard pile */
  getDiscardPile(sessionId: string, deckId: string): Promise<CardDto[]>;

  /** Discard cards */
  discardCards(sessionId: string, deckId: string, cardIds: string[]): Promise<void>;

  // ========== Notes ==========

  /** Get all notes */
  getNotes(sessionId: string): Promise<PlayerNoteDto[]>;

  /** Get a single note */
  getNote(sessionId: string, noteId: string): Promise<PlayerNoteDto>;

  /** Save a private note */
  saveNote(sessionId: string, request: SaveNoteCommand): Promise<PlayerNoteDto>;

  /** Reveal a private note */
  revealNote(sessionId: string, noteId: string): Promise<void>;

  /** Hide a note */
  hideNote(sessionId: string, noteId: string): Promise<void>;

  /** Delete a note */
  deleteNote(sessionId: string, noteId: string): Promise<void>;

  // ========== Chat ==========

  /** Get chat messages */
  getChatMessages(sessionId: string): Promise<GameSessionChatMessageDto[]>;

  /** Send a chat message */
  sendMessage(
    sessionId: string,
    request: SendChatMessageCommand
  ): Promise<GameSessionChatMessageDto>;

  /** Ask the AI agent */
  askAgent(sessionId: string, request: AskAgentCommand): Promise<GameSessionChatMessageDto>;

  /** Delete a chat message */
  deleteMessage(sessionId: string, messageId: string): Promise<void>;

  // ========== Random Tools ==========

  /** Flip a coin */
  flipCoin(sessionId: string): Promise<CoinFlipResultDto>;

  /** Spin the wheel */
  spinWheel(sessionId: string): Promise<WheelSpinResultDto>;

  // ========== Timer ==========

  /** Start timer */
  startTimer(sessionId: string): Promise<void>;

  /** Pause timer */
  pauseTimer(sessionId: string): Promise<void>;

  /** Resume timer */
  resumeTimer(sessionId: string): Promise<void>;

  /** Reset timer */
  resetTimer(sessionId: string): Promise<void>;

  // ========== Media ==========

  /** Get session media */
  getMedia(sessionId: string): Promise<MediaDto[]>;

  /** Delete a media item */
  deleteMedia(sessionId: string, mediaId: string): Promise<void>;

  /** Update media caption */
  updateMediaCaption(sessionId: string, mediaId: string, caption: string): Promise<void>;

  // ========== Sharing ==========

  /** Get shareable session info */
  getShareInfo(sessionId: string): Promise<ShareableSessionDto>;

  /** Export session as PDF */
  exportPdf(sessionId: string): Promise<Blob>;

  // ========== SSE Streams ==========

  /** Get SSE stream URL for a session */
  getStreamUrl(sessionId: string): string;

  /** Get enhanced SSE stream URL */
  getEnhancedStreamUrl(sessionId: string): string;
}

export function createSessionTrackingClient({
  httpClient,
}: {
  httpClient: HttpClient;
}): SessionTrackingClient {
  return {
    // ========== Session CRUD ==========

    async createSession(request) {
      const response = await httpClient.post<SessionDto>(`${BASE}`, request);
      return SessionDtoSchema.parse(response);
    },

    async getSession(sessionId) {
      const response = await httpClient.get<SessionDetailsDto>(
        `${BASE}/${encodeURIComponent(sessionId)}`
      );
      if (!response) throw new Error('Session not found');
      return SessionDetailsDtoSchema.parse(response);
    },

    async getActiveSession() {
      return httpClient.get<SessionDto>(`${BASE}/active`, SessionDtoSchema);
    },

    async getByCode(code) {
      const response = await httpClient.get<SessionDto>(`${BASE}/code/${encodeURIComponent(code)}`);
      if (!response) throw new Error('Session not found');
      return SessionDtoSchema.parse(response);
    },

    async joinByCode(code) {
      await httpClient.post(`${BASE}/code/${encodeURIComponent(code)}/join`);
    },

    async getHistory(options) {
      const params = new URLSearchParams();
      if (options?.limit) params.set('limit', options.limit.toString());
      if (options?.offset) params.set('offset', options.offset.toString());
      const query = params.toString();
      const url = `${BASE}/history${query ? `?${query}` : ''}`;
      const response = await httpClient.get<SessionSummaryDto[]>(url);
      return z.array(SessionSummaryDtoSchema).parse(response ?? []);
    },

    async finalizeSession(sessionId, request) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/finalize`, request ?? {});
    },

    // ========== Participants ==========

    async addParticipant(sessionId, request) {
      const response = await httpClient.post<ParticipantDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/participants`,
        request
      );
      return ParticipantDtoSchema.parse(response);
    },

    async markPlayerReady(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/players/ready`);
    },

    async kickParticipant(sessionId, participantId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/players/kick`, {
        participantId,
      });
    },

    async assignRole(sessionId, playerId, role) {
      await httpClient.put(
        `${BASE}/${encodeURIComponent(sessionId)}/players/${encodeURIComponent(playerId)}/role`,
        { role }
      );
    },

    // ========== Scores ==========

    async updateScore(sessionId, request) {
      const response = await httpClient.put<ScoreEntryDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/scores`,
        request
      );
      return ScoreEntryDtoSchema.parse(response);
    },

    async updatePlayerScore(sessionId, playerId, score) {
      await httpClient.put(
        `${BASE}/${encodeURIComponent(sessionId)}/players/${encodeURIComponent(playerId)}/score`,
        { score }
      );
    },

    async getScoreboard(sessionId) {
      const response = await httpClient.get<ScoreboardDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/scoreboard`
      );
      if (!response) throw new Error('Scoreboard not found');
      return ScoreboardDtoSchema.parse(response);
    },

    // ========== Dice ==========

    async rollDice(sessionId, request) {
      const response = await httpClient.post<DiceRollResultDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/dice`,
        request
      );
      return DiceRollResultDtoSchema.parse(response);
    },

    async rollDiceForPlayer(sessionId, playerId, request) {
      const response = await httpClient.post<DiceRollResultDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/players/${encodeURIComponent(playerId)}/dice`,
        request
      );
      return DiceRollResultDtoSchema.parse(response);
    },

    async getDiceHistory(sessionId) {
      const response = await httpClient.get<DiceRollHistoryDto[]>(
        `${BASE}/${encodeURIComponent(sessionId)}/dice`
      );
      return z.array(DiceRollHistoryDtoSchema).parse(response ?? []);
    },

    // ========== Card Decks ==========

    async createDeck(sessionId, request) {
      const response = await httpClient.post<DeckDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/decks`,
        request
      );
      return DeckDtoSchema.parse(response);
    },

    async getDecks(sessionId) {
      const response = await httpClient.get<DeckDto[]>(
        `${BASE}/${encodeURIComponent(sessionId)}/decks`
      );
      return z.array(DeckDtoSchema).parse(response ?? []);
    },

    async shuffleDeck(sessionId, deckId) {
      await httpClient.post(
        `${BASE}/${encodeURIComponent(sessionId)}/decks/${encodeURIComponent(deckId)}/shuffle`
      );
    },

    async drawCards(sessionId, deckId, request) {
      const response = await httpClient.post<DrawnCardsDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/decks/${encodeURIComponent(deckId)}/draw`,
        request
      );
      return DrawnCardsDtoSchema.parse(response);
    },

    async getHand(sessionId, deckId) {
      const response = await httpClient.get<CardDto[]>(
        `${BASE}/${encodeURIComponent(sessionId)}/decks/${encodeURIComponent(deckId)}/hand`
      );
      return z.array(CardDtoSchema).parse(response ?? []);
    },

    async getDiscardPile(sessionId, deckId) {
      const response = await httpClient.get<CardDto[]>(
        `${BASE}/${encodeURIComponent(sessionId)}/decks/${encodeURIComponent(deckId)}/discard`
      );
      return z.array(CardDtoSchema).parse(response ?? []);
    },

    async discardCards(sessionId, deckId, cardIds) {
      await httpClient.post(
        `${BASE}/${encodeURIComponent(sessionId)}/decks/${encodeURIComponent(deckId)}/discard`,
        { cardIds }
      );
    },

    // ========== Notes ==========

    async getNotes(sessionId) {
      const response = await httpClient.get<PlayerNoteDto[]>(
        `${BASE}/${encodeURIComponent(sessionId)}/notes`
      );
      return z.array(PlayerNoteDtoSchema).parse(response ?? []);
    },

    async getNote(sessionId, noteId) {
      const response = await httpClient.get<PlayerNoteDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/notes/${encodeURIComponent(noteId)}`
      );
      if (!response) throw new Error('Note not found');
      return PlayerNoteDtoSchema.parse(response);
    },

    async saveNote(sessionId, request) {
      const response = await httpClient.post<PlayerNoteDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/private-notes`,
        request
      );
      return PlayerNoteDtoSchema.parse(response);
    },

    async revealNote(sessionId, noteId) {
      await httpClient.put(
        `${BASE}/${encodeURIComponent(sessionId)}/private-notes/${encodeURIComponent(noteId)}/reveal`,
        {}
      );
    },

    async hideNote(sessionId, noteId) {
      await httpClient.put(
        `${BASE}/${encodeURIComponent(sessionId)}/private-notes/${encodeURIComponent(noteId)}/hide`,
        {}
      );
    },

    async deleteNote(sessionId, noteId) {
      await httpClient.delete(
        `${BASE}/${encodeURIComponent(sessionId)}/private-notes/${encodeURIComponent(noteId)}`
      );
    },

    // ========== Chat ==========

    async getChatMessages(sessionId) {
      const response = await httpClient.get<GameSessionChatMessageDto[]>(
        `${BASE}/${encodeURIComponent(sessionId)}/chat`
      );
      return z.array(GameSessionChatMessageDtoSchema).parse(response ?? []);
    },

    async sendMessage(sessionId, request) {
      const response = await httpClient.post<GameSessionChatMessageDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/chat`,
        request
      );
      return GameSessionChatMessageDtoSchema.parse(response);
    },

    async askAgent(sessionId, request) {
      const response = await httpClient.post<GameSessionChatMessageDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/chat/agent`,
        request
      );
      return GameSessionChatMessageDtoSchema.parse(response);
    },

    async deleteMessage(sessionId, messageId) {
      await httpClient.delete(
        `${BASE}/${encodeURIComponent(sessionId)}/chat/${encodeURIComponent(messageId)}`
      );
    },

    // ========== Random Tools ==========

    async flipCoin(sessionId) {
      const response = await httpClient.post<CoinFlipResultDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/random/flip-coin`
      );
      return CoinFlipResultDtoSchema.parse(response);
    },

    async spinWheel(sessionId) {
      const response = await httpClient.post<WheelSpinResultDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/random/spin-wheel`
      );
      return WheelSpinResultDtoSchema.parse(response);
    },

    // ========== Timer ==========

    async startTimer(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/timer/start`);
    },

    async pauseTimer(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/timer/pause`);
    },

    async resumeTimer(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/timer/resume`);
    },

    async resetTimer(sessionId) {
      await httpClient.post(`${BASE}/${encodeURIComponent(sessionId)}/timer/reset`);
    },

    // ========== Media ==========

    async getMedia(sessionId) {
      const response = await httpClient.get<MediaDto[]>(
        `${BASE}/${encodeURIComponent(sessionId)}/media`
      );
      return z.array(MediaDtoSchema).parse(response ?? []);
    },

    async deleteMedia(sessionId, mediaId) {
      await httpClient.delete(
        `${BASE}/${encodeURIComponent(sessionId)}/media/${encodeURIComponent(mediaId)}`
      );
    },

    async updateMediaCaption(sessionId, mediaId, caption) {
      await httpClient.put(
        `${BASE}/${encodeURIComponent(sessionId)}/media/${encodeURIComponent(mediaId)}/caption`,
        { caption }
      );
    },

    // ========== Sharing ==========

    async getShareInfo(sessionId) {
      const response = await httpClient.get<ShareableSessionDto>(
        `${BASE}/${encodeURIComponent(sessionId)}/share`
      );
      if (!response) throw new Error('Share info not found');
      return ShareableSessionDtoSchema.parse(response);
    },

    async exportPdf(sessionId) {
      const response = await fetch(`${BASE}/${encodeURIComponent(sessionId)}/pdf`, {
        credentials: 'include',
      });
      if (!response.ok) throw new Error(`PDF export failed: ${response.status}`);
      return response.blob();
    },

    // ========== SSE Streams ==========

    getStreamUrl(sessionId) {
      return `${BASE}/${encodeURIComponent(sessionId)}/stream`;
    },

    getEnhancedStreamUrl(sessionId) {
      return `${BASE}/${encodeURIComponent(sessionId)}/stream-enhanced`;
    },
  };
}
