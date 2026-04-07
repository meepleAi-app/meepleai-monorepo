export type GameNightStatus = 'draft' | 'upcoming' | 'completed';

export interface GameNightSummary {
  id: string;
  title: string;
  status: GameNightStatus;
  date: string;
  location?: string;
  playerCount: number;
  gameCount: number;
  playerAvatars: string[];
  gameThumbnails: string[];
  winnerId?: string;
}

export interface GameNightPlayer {
  id: string;
  name: string;
  avatarUrl?: string;
}

export interface GameNightGame {
  id: string;
  title: string;
  thumbnailUrl?: string;
  minPlayers?: number;
  maxPlayers?: number;
}

export type DiaryEntryType =
  | 'game_started'
  | 'game_completed'
  | 'night_started'
  | 'night_finalized'
  | 'score_update'
  | 'dice_roll'
  | 'card_draw'
  | 'photo'
  | 'pause_resume'
  | 'player_joined'
  | 'player_left'
  | 'dispute_resolved'
  | 'note_added'
  | 'resource_update';

export interface DiaryEntry {
  id: string;
  sessionId: string;
  gameNightId?: string;
  eventType: DiaryEntryType;
  description: string;
  payload?: Record<string, unknown>;
  actorId?: string;
  timestamp: string;
}

export interface GameNightActiveSession {
  id: string;
  gameNightSessionId: string;
  gameId: string;
  gameTitle: string;
  playOrder: number;
  status: 'pending' | 'in_progress' | 'completed' | 'skipped';
  winnerId?: string;
  startedAt?: string;
  completedAt?: string;
}

export interface PlayerResource {
  participantId: string;
  playerName: string;
  resources: Record<string, number>;
}

export interface TimelineSlot {
  id: string;
  type: 'game' | 'break' | 'free';
  gameId?: string;
  startTime?: string;
  durationMinutes: number;
}
