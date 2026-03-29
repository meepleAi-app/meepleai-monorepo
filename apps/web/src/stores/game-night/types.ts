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

export interface TimelineSlot {
  id: string;
  type: 'game' | 'break' | 'free';
  gameId?: string;
  startTime?: string;
  durationMinutes: number;
}
