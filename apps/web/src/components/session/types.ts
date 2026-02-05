// Session Toolkit - TypeScript Interfaces

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
