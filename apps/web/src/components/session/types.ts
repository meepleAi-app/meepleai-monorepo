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
