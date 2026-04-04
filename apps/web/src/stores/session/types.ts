export type SessionStatus = 'idle' | 'live' | 'paused' | 'ended';

export type ActivityEventType =
  | 'dice_roll'
  | 'ai_tip'
  | 'score_update'
  | 'photo'
  | 'note'
  | 'audio_note'
  | 'turn_change'
  | 'pause_resume'
  | 'session_start'
  | 'card_draw';

export interface ActivityEvent {
  id: string;
  type: ActivityEventType;
  playerId?: string;
  data: Record<string, unknown>;
  timestamp: string;
}

export interface PlayerScore {
  playerId: string;
  score: number;
}

export interface SessionParticipant {
  id: string;
  displayName: string;
  isGuest: boolean;
  userId?: string;
  avatarUrl?: string;
}

export interface StartSessionPayload {
  sessionId: string;
  gameId: string;
  gameTitle: string;
  participants: SessionParticipant[];
}
