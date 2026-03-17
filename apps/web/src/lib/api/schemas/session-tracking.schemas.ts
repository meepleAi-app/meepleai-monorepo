/**
 * Session Tracking API Schemas
 *
 * Zod schemas for SessionTracking bounded context (legacy tools).
 * Maps to SessionTrackingEndpoints.cs DTOs.
 *
 * Issue #5041 — Sessions Redesign Phase 1
 */

import { z } from 'zod';

// ========== Core Session DTOs ==========

export const ParticipantDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid().nullable(),
  displayName: z.string(),
  isOwner: z.boolean(),
  joinOrder: z.number().int(),
  finalRank: z.number().int().nullable(),
  totalScore: z.number(),
});

export type ParticipantDto = z.infer<typeof ParticipantDtoSchema>;

export const ScoreEntryDtoSchema = z.object({
  id: z.string().uuid(),
  participantId: z.string().uuid(),
  roundNumber: z.number().int().nullable(),
  category: z.string().nullable(),
  scoreValue: z.number(),
  timestamp: z.string(),
});

export type ScoreEntryDto = z.infer<typeof ScoreEntryDtoSchema>;

export const PlayerNoteDtoSchema = z.object({
  id: z.string().uuid(),
  participantId: z.string().uuid(),
  noteType: z.string(),
  templateKey: z.string().nullable(),
  content: z.string(),
  isHidden: z.boolean(),
  createdAt: z.string(),
});

export type PlayerNoteDto = z.infer<typeof PlayerNoteDtoSchema>;

export const SessionDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  gameId: z.string().uuid().nullable(),
  sessionCode: z.string(),
  sessionType: z.string(),
  status: z.string(),
  sessionDate: z.string(),
  location: z.string().nullable(),
  finalizedAt: z.string().nullable(),
  participants: z.array(ParticipantDtoSchema),
  scores: z.array(ScoreEntryDtoSchema),
});

export type SessionDto = z.infer<typeof SessionDtoSchema>;

export const SessionDetailsDtoSchema = SessionDtoSchema.extend({
  notes: z.array(PlayerNoteDtoSchema),
});

export type SessionDetailsDto = z.infer<typeof SessionDetailsDtoSchema>;

export const SessionSummaryDtoSchema = z.object({
  id: z.string().uuid(),
  sessionCode: z.string(),
  sessionDate: z.string(),
  gameName: z.string().nullable(),
  gameIcon: z.string().nullable(),
  participantsNames: z.string(),
  winnerName: z.string().nullable(),
  durationMinutes: z.number().int(),
  status: z.string(),
});

export type SessionSummaryDto = z.infer<typeof SessionSummaryDtoSchema>;

// ========== Scoreboard ==========

export const ParticipantScoreDtoSchema = z.object({
  participantId: z.string().uuid(),
  displayName: z.string(),
  totalScore: z.number(),
  currentRank: z.number().int(),
});

export type ParticipantScoreDto = z.infer<typeof ParticipantScoreDtoSchema>;

export const ScoreboardDtoSchema = z.object({
  sessionId: z.string().uuid(),
  participants: z.array(ParticipantScoreDtoSchema),
  scoresByRound: z.record(z.string(), z.record(z.string(), z.number())),
  scoresByCategory: z.record(z.string(), z.record(z.string(), z.number())),
  currentLeaderId: z.string().uuid().nullable(),
});

export type ScoreboardDto = z.infer<typeof ScoreboardDtoSchema>;

// ========== Dice & Random Tools ==========

export const DiceRollResultDtoSchema = z.object({
  id: z.string().uuid().optional(),
  diceType: z.string(),
  numberOfDice: z.number().int(),
  results: z.array(z.number().int()),
  total: z.number().int(),
  rolledBy: z.string().optional(),
  rolledAt: z.string().optional(),
});

export type DiceRollResultDto = z.infer<typeof DiceRollResultDtoSchema>;

export const DiceRollHistoryDtoSchema = z.object({
  id: z.string().uuid(),
  participantId: z.string().uuid().nullable(),
  diceType: z.string(),
  numberOfDice: z.number().int(),
  results: z.array(z.number().int()),
  total: z.number().int(),
  rolledAt: z.string(),
});

export type DiceRollHistoryDto = z.infer<typeof DiceRollHistoryDtoSchema>;

export const CoinFlipResultDtoSchema = z.object({
  result: z.enum(['Heads', 'Tails']),
  flippedAt: z.string().optional(),
});

export type CoinFlipResultDto = z.infer<typeof CoinFlipResultDtoSchema>;

export const WheelSpinResultDtoSchema = z.object({
  selectedIndex: z.number().int(),
  selectedValue: z.string(),
  spunAt: z.string().optional(),
});

export type WheelSpinResultDto = z.infer<typeof WheelSpinResultDtoSchema>;

// ========== Card Deck ==========

export const CardDtoSchema = z.object({
  id: z.string().uuid(),
  suit: z.string(),
  value: z.string(),
  imageUrl: z.string().nullable().optional(),
});

export type CardDto = z.infer<typeof CardDtoSchema>;

export const DeckDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  totalCards: z.number().int(),
  remainingCards: z.number().int(),
  createdAt: z.string(),
});

export type DeckDto = z.infer<typeof DeckDtoSchema>;

export const DrawnCardsDtoSchema = z.object({
  cards: z.array(CardDtoSchema),
  remainingInDeck: z.number().int(),
});

export type DrawnCardsDto = z.infer<typeof DrawnCardsDtoSchema>;

// ========== Chat ==========

export const GameSessionChatMessageDtoSchema = z.object({
  id: z.string().uuid(),
  sessionId: z.string().uuid(),
  senderId: z.string().uuid().nullable(),
  senderName: z.string(),
  messageType: z.enum(['User', 'System', 'Agent']),
  content: z.string(),
  sentAt: z.string(),
});

export type GameSessionChatMessageDto = z.infer<typeof GameSessionChatMessageDtoSchema>;

// ========== Media ==========

export const MediaDtoSchema = z.object({
  id: z.string().uuid(),
  sessionId: z.string().uuid(),
  uploadedBy: z.string().uuid(),
  fileName: z.string(),
  contentType: z.string(),
  url: z.string(),
  caption: z.string().nullable(),
  uploadedAt: z.string(),
});

export type MediaDto = z.infer<typeof MediaDtoSchema>;

// ========== Shareable Session ==========

export const ShareableSessionDtoSchema = z.object({
  sessionId: z.string().uuid(),
  shareUrl: z.string(),
  sessionCode: z.string(),
  gameName: z.string().nullable(),
  playerCount: z.number().int(),
  status: z.string(),
});

export type ShareableSessionDto = z.infer<typeof ShareableSessionDtoSchema>;

// ========== Request DTOs ==========

export const CreateSessionCommandSchema = z.object({
  gameId: z.string().uuid().optional(),
  gameName: z.string().optional(),
  sessionType: z.string().optional().default('Standard'),
  location: z.string().optional(),
});

export type CreateSessionCommand = z.infer<typeof CreateSessionCommandSchema>;

export const AddParticipantCommandSchema = z.object({
  displayName: z.string().min(1),
  userId: z.string().uuid().optional(),
});

export type AddParticipantCommand = z.infer<typeof AddParticipantCommandSchema>;

export const UpdateScoreCommandSchema = z.object({
  participantId: z.string().uuid(),
  roundNumber: z.number().int().nullable().optional(),
  category: z.string().nullable().optional(),
  scoreValue: z.number(),
});

export type UpdateScoreCommand = z.infer<typeof UpdateScoreCommandSchema>;

export const RollDiceCommandSchema = z.object({
  diceType: z.string().default('d6'),
  numberOfDice: z.number().int().min(1).default(1),
});

export type RollDiceCommand = z.infer<typeof RollDiceCommandSchema>;

export const CreateDeckCommandSchema = z.object({
  name: z.string().min(1),
  deckType: z.string().optional().default('Standard52'),
});

export type CreateDeckCommand = z.infer<typeof CreateDeckCommandSchema>;

export const DrawCardsCommandSchema = z.object({
  count: z.number().int().min(1).default(1),
});

export type DrawCardsCommand = z.infer<typeof DrawCardsCommandSchema>;

export const SaveNoteCommandSchema = z.object({
  content: z.string().min(1),
  noteType: z.string().optional().default('Generic'),
  templateKey: z.string().nullable().optional(),
  isHidden: z.boolean().optional().default(true),
});

export type SaveNoteCommand = z.infer<typeof SaveNoteCommandSchema>;

export const SendChatMessageCommandSchema = z.object({
  content: z.string().min(1),
});

export type SendChatMessageCommand = z.infer<typeof SendChatMessageCommandSchema>;

export const AskAgentCommandSchema = z.object({
  content: z.string().min(1),
});

export type AskAgentCommand = z.infer<typeof AskAgentCommandSchema>;

export const FinalizeSessionCommandSchema = z.object({
  ranks: z.record(z.string(), z.number().int()).optional(),
});

export type FinalizeSessionCommand = z.infer<typeof FinalizeSessionCommandSchema>;

// ========== Turn Summary AI (Issue #277) ==========

export const TurnSummaryRequestSchema = z.object({
  fromPhase: z.number().int().min(0).optional(),
  toPhase: z.number().int().min(0).optional(),
  lastNEvents: z.number().int().min(1).max(100).optional(),
});

export type TurnSummaryRequest = z.infer<typeof TurnSummaryRequestSchema>;

export const TurnSummaryResultSchema = z.object({
  summaryEventId: z.string().uuid(),
  summary: z.string(),
  eventsAnalyzed: z.number().int(),
  generatedAt: z.string(),
});

export type TurnSummaryResult = z.infer<typeof TurnSummaryResultSchema>;

// ========== Session Checkpoints (Issue #278) ==========

export const CreateCheckpointRequestSchema = z.object({
  name: z.string().min(1).max(200),
});

export type CreateCheckpointRequest = z.infer<typeof CreateCheckpointRequestSchema>;

export const CreateCheckpointResultSchema = z.object({
  checkpointId: z.string().uuid(),
  name: z.string(),
  createdAt: z.string(),
  diaryEventCount: z.number().int(),
});

export type CreateCheckpointResult = z.infer<typeof CreateCheckpointResultSchema>;

export const SessionCheckpointDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  createdAt: z.string(),
  createdBy: z.string().uuid(),
  diaryEventCount: z.number().int(),
});

export type SessionCheckpointDto = z.infer<typeof SessionCheckpointDtoSchema>;

export const ListCheckpointsResultSchema = z.object({
  checkpoints: z.array(SessionCheckpointDtoSchema),
});

export type ListCheckpointsResult = z.infer<typeof ListCheckpointsResultSchema>;

export const RestoreCheckpointResultSchema = z.object({
  checkpointId: z.string().uuid(),
  name: z.string(),
  restoredAt: z.string(),
  widgetsRestored: z.number().int(),
});

export type RestoreCheckpointResult = z.infer<typeof RestoreCheckpointResultSchema>;
