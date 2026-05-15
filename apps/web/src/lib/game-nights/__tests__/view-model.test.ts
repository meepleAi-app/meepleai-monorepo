/**
 * Tests for toGameNightVM (Stage 3 /game-nights index v2 — Foundation).
 */

import { describe, expect, it } from 'vitest';

import type { GameNightDto } from '@/lib/api/schemas/game-nights.schemas';

import { toGameNightVM } from '../view-model';

const HOST_ID = '11111111-1111-1111-1111-111111111111';
const OTHER_ID = '22222222-2222-2222-2222-222222222222';

function buildDto(overrides: Partial<GameNightDto> = {}): GameNightDto {
  return {
    id: '00000000-0000-0000-0000-0000000000aa',
    organizerId: HOST_ID,
    organizerName: 'Marco R.',
    title: 'Serata Test',
    description: null,
    scheduledAt: '2026-06-12T19:00:00',
    location: 'Da Marco',
    maxPlayers: 8,
    gameIds: ['00000000-0000-0000-0000-0000000000bb'],
    status: 'Published',
    acceptedCount: 3,
    pendingCount: 0,
    totalInvited: 3,
    createdAt: '2026-05-15T10:00:00Z',
    updatedAt: null,
    ...overrides,
  };
}

describe('toGameNightVM', () => {
  it('derives day/month/year from scheduledAt (local time)', () => {
    const vm = toGameNightVM(buildDto({ scheduledAt: '2026-06-12T19:00:00' }), HOST_ID);
    expect(vm.day).toBe(12);
    expect(vm.month).toBe(5); // 0-indexed June
    expect(vm.year).toBe(2026);
  });

  it('formats timeLabel as HH:mm zero-padded', () => {
    const vm = toGameNightVM(buildDto({ scheduledAt: '2026-06-12T09:05:00' }), HOST_ID);
    expect(vm.timeLabel).toBe('09:05');
  });

  it('role=organizer when currentUserId matches organizerId', () => {
    const vm = toGameNightVM(buildDto(), HOST_ID);
    expect(vm.role).toBe('organizer');
  });

  it('role=invited when currentUserId differs', () => {
    const vm = toGameNightVM(buildDto(), OTHER_ID);
    expect(vm.role).toBe('invited');
  });

  it('role=invited when currentUserId is null', () => {
    const vm = toGameNightVM(buildDto(), null);
    expect(vm.role).toBe('invited');
  });

  it('statusKey=cancelled when status=Cancelled', () => {
    const vm = toGameNightVM(buildDto({ status: 'Cancelled' }), HOST_ID);
    expect(vm.statusKey).toBe('cancelled');
  });

  it('statusKey=completed when status=Completed', () => {
    const vm = toGameNightVM(buildDto({ status: 'Completed' }), HOST_ID);
    expect(vm.statusKey).toBe('completed');
  });

  it('statusKey=confirmed when Published and acceptedCount >= 3', () => {
    const vm = toGameNightVM(buildDto({ status: 'Published', acceptedCount: 3 }), HOST_ID);
    expect(vm.statusKey).toBe('confirmed');
  });

  it('statusKey=planned when Published and acceptedCount < 3', () => {
    const vm = toGameNightVM(buildDto({ status: 'Published', acceptedCount: 2 }), HOST_ID);
    expect(vm.statusKey).toBe('planned');
  });

  it('statusKey=planned when status=Draft', () => {
    const vm = toGameNightVM(buildDto({ status: 'Draft', acceptedCount: 10 }), HOST_ID);
    expect(vm.statusKey).toBe('planned');
  });

  it('exposes gameIds and an empty playerIds list (backend gap)', () => {
    const vm = toGameNightVM(
      buildDto({ gameIds: ['00000000-0000-0000-0000-0000000000bb'] }),
      HOST_ID
    );
    expect(vm.gameIds).toEqual(['00000000-0000-0000-0000-0000000000bb']);
    expect(vm.playerIds).toEqual([]);
  });

  it('uses empty string for location when null', () => {
    const vm = toGameNightVM(buildDto({ location: null }), HOST_ID);
    expect(vm.location).toBe('');
  });

  it('exposes empty durationLabel (backend gap)', () => {
    const vm = toGameNightVM(buildDto(), HOST_ID);
    expect(vm.durationLabel).toBe('');
  });

  it('preserves id, title and scheduledAtIso', () => {
    const dto = buildDto();
    const vm = toGameNightVM(dto, HOST_ID);
    expect(vm.id).toBe(dto.id);
    expect(vm.title).toBe(dto.title);
    expect(vm.scheduledAtIso).toBe(dto.scheduledAt);
  });
});
