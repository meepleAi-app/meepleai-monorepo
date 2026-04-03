import { describe, it, expect } from 'vitest';
import { buildPlayerCardProps } from '../player-card-mapper';
import type { SessionPlayer } from '@/lib/api/schemas/play-records.schemas';

const basePlayer: SessionPlayer = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  displayName: 'Mario Rossi',
  userId: null,
  scores: [],
};

describe('buildPlayerCardProps', () => {
  it('maps displayName to title', () => {
    const props = buildPlayerCardProps(basePlayer);
    expect(props.title).toBe('Mario Rossi');
  });

  it('returns only title field', () => {
    const props = buildPlayerCardProps(basePlayer);
    expect(Object.keys(props)).toEqual(['title']);
  });

  it('handles different player names', () => {
    const player: SessionPlayer = {
      ...basePlayer,
      displayName: 'Luigi Bianchi',
    };
    const props = buildPlayerCardProps(player);
    expect(props.title).toBe('Luigi Bianchi');
  });
});
