/**
 * View model adapter for /game-nights index v2 (Stage 3, refs #1170).
 *
 * Translates the canonical {@link GameNightDto} into a denormalized,
 * presentation-friendly shape that calendar/list components consume directly.
 *
 * Notes:
 * - `playerIds` / `durationLabel` are placeholders — backend does not yet
 *   surface participant roster nor expected duration in the list endpoint.
 *   Components MUST tolerate empty values.
 * - Role is derived by comparing `dto.organizerId` against the caller-supplied
 *   `currentUserId` — the DTO does not carry viewer-specific flags.
 */

import type { GameNightDto } from '@/lib/api/schemas/game-nights.schemas';

export type StatusKey = 'confirmed' | 'planned' | 'cancelled' | 'completed';
export type RoleKey = 'organizer' | 'invited';

export interface GameNightVM {
  readonly id: string;
  readonly title: string;
  readonly scheduledAtIso: string;
  readonly day: number; // 1..31
  readonly month: number; // 0-indexed (matches Date API)
  readonly year: number;
  readonly timeLabel: string; // "HH:mm" zero-padded local time
  readonly durationLabel: string; // empty — backend gap
  readonly location: string;
  readonly gameIds: readonly string[];
  readonly playerIds: readonly string[]; // empty — backend gap
  readonly role: RoleKey;
  readonly statusKey: StatusKey;
}

// Mockup spec (sp4-game-nights-index): ≥3 accepted RSVPs promote a Published night to the "Confermata" status chip.
const CONFIRMED_ACCEPTED_THRESHOLD = 3;

function deriveStatusKey(dto: GameNightDto): StatusKey {
  switch (dto.status) {
    case 'Cancelled':
      return 'cancelled';
    case 'Completed':
      return 'completed';
    case 'Published':
      return dto.acceptedCount >= CONFIRMED_ACCEPTED_THRESHOLD ? 'confirmed' : 'planned';
    case 'Draft':
    default:
      return 'planned';
  }
}

function pad2(value: number): string {
  return value < 10 ? `0${value}` : String(value);
}

export function toGameNightVM(dto: GameNightDto, currentUserId: string | null): GameNightVM {
  const scheduled = new Date(dto.scheduledAt);
  const day = scheduled.getDate();
  const month = scheduled.getMonth();
  const year = scheduled.getFullYear();
  const timeLabel = `${pad2(scheduled.getHours())}:${pad2(scheduled.getMinutes())}`;

  const role: RoleKey =
    currentUserId !== null && dto.organizerId === currentUserId ? 'organizer' : 'invited';

  return {
    id: dto.id,
    title: dto.title,
    scheduledAtIso: dto.scheduledAt,
    day,
    month,
    year,
    timeLabel,
    durationLabel: '',
    location: dto.location ?? '',
    gameIds: dto.gameIds,
    playerIds: [],
    role,
    statusKey: deriveStatusKey(dto),
  };
}
