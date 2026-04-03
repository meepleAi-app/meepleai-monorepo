import { describe, it, expect } from 'vitest';
import { buildSessionCardProps } from '../session-card-mapper';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

const baseSession: GameSessionDto = {
  id: 'session-1',
  gameId: '00000000-0000-0000-0000-000000000001',
  status: 'InProgress',
  startedAt: '2024-01-01T10:00:00Z',
  completedAt: null,
  playerCount: 3,
  players: [],
  winnerName: null,
  notes: null,
  durationMinutes: 0,
};

describe('buildSessionCardProps', () => {
  it('builds linkedEntities with game pip when gameId is present', () => {
    const props = buildSessionCardProps(baseSession);
    expect(props.linkedEntities).toContainEqual({ entityType: 'game', count: 1 });
  });

  it('does not add game pip when gameId is missing', () => {
    const props = buildSessionCardProps({ ...baseSession, gameId: '' });
    expect(props.linkedEntities ?? []).not.toContainEqual(
      expect.objectContaining({ entityType: 'game' })
    );
  });

  it('builds stateLabel for InProgress session', () => {
    const props = buildSessionCardProps(baseSession);
    expect(props.stateLabel).toEqual({ text: 'Live', variant: 'success' });
  });

  it('builds stateLabel for Paused session', () => {
    const props = buildSessionCardProps({ ...baseSession, status: 'Paused' });
    expect(props.stateLabel).toEqual({ text: 'Pausa', variant: 'warning' });
  });

  it('builds stateLabel for Completed session', () => {
    const props = buildSessionCardProps({ ...baseSession, status: 'Completed' });
    expect(props.stateLabel).toEqual({ text: 'Completata', variant: 'info' });
  });

  it('builds stateLabel for Setup session', () => {
    const props = buildSessionCardProps({ ...baseSession, status: 'Setup' });
    expect(props.stateLabel).toEqual({ text: 'Impostazione', variant: 'info' });
  });
});
