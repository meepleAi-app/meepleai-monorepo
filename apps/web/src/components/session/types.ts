// Session Toolkit - TypeScript Interfaces

// Turn Order Types (Issue #4970 / #4975)
export interface TurnOrderData {
  id: string;
  sessionId: string;
  playerOrder: string[];
  currentIndex: number;
  currentPlayer: string | null;
  nextPlayer: string | null;
  roundNumber: number;
}

export interface TurnAdvancedPayload {
  currentPlayerName: string;
  previousPlayerName: string;
  nextPlayerName: string;
  roundNumber: number;
}

export interface Participant {
  id: string;
  displayName: string;
  isOwner: boolean;
  isCurrentUser: boolean;
  avatarColor: string;
  totalScore: number;
  rank?: number;
  isTyping?: boolean;
}

export interface ScoreEntry {
  id: string;
  participantId: string;
  roundNumber: number | null;
  category: string | null;
  scoreValue: number;
  timestamp: Date;
  createdBy: string;
}

export interface Session {
  id: string;
  sessionCode: string;
  sessionType: 'Generic' | 'GameSpecific';
  gameName?: string;
  gameIcon?: string;
  sessionDate: Date;
  status: 'Active' | 'Paused' | 'Finalized';
  participantCount: number;
}

export interface ScoreboardData {
  participants: Participant[];
  scores: ScoreEntry[];
  rounds: number[];
  categories: string[];
}

export type SyncStatus = 'idle' | 'saving' | 'synced' | 'error';

export interface DiceRoll {
  id: string;
  participantId: string;
  participantName: string;
  formula: string;
  label?: string;
  rolls: number[];
  modifier: number;
  total: number;
  timestamp: Date;
}

export type DiceType = 'd4' | 'd6' | 'd8' | 'd10' | 'd12' | 'd20' | 'd100';

export const DICE_TYPES: { type: DiceType; sides: number; label: string }[] = [
  { type: 'd4', sides: 4, label: 'D4' },
  { type: 'd6', sides: 6, label: 'D6' },
  { type: 'd8', sides: 8, label: 'D8' },
  { type: 'd10', sides: 10, label: 'D10' },
  { type: 'd12', sides: 12, label: 'D12' },
  { type: 'd20', sides: 20, label: 'D20' },
  { type: 'd100', sides: 100, label: 'D100' },
];

// Card Deck Types (Issue #3343)
export interface Card {
  id: string;
  name: string;
  imageUrl?: string;
  suit?: string;
  value?: string;
}

export interface SessionDeck {
  id: string;
  name: string;
  deckType: 'Standard52' | 'Standard54' | 'Custom';
  totalCards: number;
  cardsInDrawPile: number;
  cardsInDiscardPile: number;
  createdAt: Date;
  lastShuffledAt?: Date;
}

export interface PlayerHand {
  deckId: string;
  participantId: string;
  cards: Card[];
}

export interface DiscardPile {
  deckId: string;
  cards: Card[];
  totalCount: number;
}

export type CardSuit = 'Hearts' | 'Diamonds' | 'Clubs' | 'Spades' | 'Joker';

export const CARD_SUITS: { suit: CardSuit; symbol: string; color: string }[] = [
  { suit: 'Hearts', symbol: '♥', color: 'text-red-500' },
  { suit: 'Diamonds', symbol: '♦', color: 'text-red-500' },
  { suit: 'Clubs', symbol: '♣', color: 'text-gray-900 dark:text-white' },
  { suit: 'Spades', symbol: '♠', color: 'text-gray-900 dark:text-white' },
  { suit: 'Joker', symbol: '🃏', color: 'text-purple-500' },
];

// Private Notes Types (Issue #3344)
export interface SessionNote {
  id: string;
  sessionId: string;
  participantId: string;
  content?: string; // Null if not owner and not revealed
  isOwner: boolean;
  isRevealed: boolean;
  obscuredText?: string;
  createdAt: Date;
  updatedAt: Date;
}

// Random Tools Types (Issue #3345)
export type TimerStatus = 'idle' | 'running' | 'paused' | 'completed';

export interface TimerState {
  id: string;
  sessionId: string;
  durationSeconds: number;
  remainingSeconds: number;
  status: TimerStatus;
  startedBy: string;
  startedByName: string;
  startedAt?: Date;
  pausedAt?: Date;
}

export interface CoinFlipResult {
  id: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  result: 'heads' | 'tails';
  timestamp: Date;
}

export interface WheelOption {
  id: string;
  label: string;
  color: string;
  weight: number;
}

export interface WheelSpinResult {
  id: string;
  sessionId: string;
  participantId: string;
  participantName: string;
  selectedOption: WheelOption;
  timestamp: Date;
}

export const DEFAULT_WHEEL_COLORS = [
  '#ef4444', // red
  '#f97316', // orange
  '#eab308', // yellow
  '#22c55e', // green
  '#0ea5e9', // sky
  '#8b5cf6', // violet
  '#ec4899', // pink
  '#06b6d4', // cyan
];

// Session Invite Types (Issue #3354)
export interface InviteTokenResponse {
  inviteToken: string;
  inviteUrl: string;
  expiresAt: Date | null;
  sessionCode: string;
  qrCodeDataUrl: string;
}

export interface SessionInviteResponse {
  sessionId: string;
  sessionCode: string;
  gameName: string | null;
  gameImageUrl: string | null;
  sessionDate: Date;
  location: string | null;
  status: string;
  participantCount: number;
  ownerDisplayName: string;
  canJoin: boolean;
  reasonCannotJoin: string | null;
}

export interface JoinSessionResponse {
  sessionId: string;
  sessionCode: string;
  participantId: string;
  displayName: string;
  joinOrder: number;
}
