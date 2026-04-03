import { describe, it, expect } from 'vitest';
import { buildGameCardProps } from '../game-card-mapper';
import type { Game } from '@/lib/api';

const baseGame: Game = {
  id: 'game-1',
  title: 'Catan',
  publisher: 'Kosmos',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  createdAt: '2024-01-01T00:00:00Z',
  imageUrl: 'https://example.com/catan.jpg',
  averageRating: 7.5,
};

describe('buildGameCardProps', () => {
  it('builds playerCountDisplay from min/max players', () => {
    const props = buildGameCardProps(baseGame);
    expect(props.playerCountDisplay).toBe('3-4p');
  });

  it('builds playTimeDisplay from avg of min/max play time', () => {
    const props = buildGameCardProps(baseGame); // avg of 60+120 = 90min → "1h30min"
    expect(props.playTimeDisplay).toBe('1h30min');
  });

  it('builds subtitle from publisher and year', () => {
    const props = buildGameCardProps(baseGame);
    expect(props.subtitle).toBe('Kosmos \u00B7 1995');
  });

  it('omits playerCountDisplay when both values are null', () => {
    const props = buildGameCardProps({ ...baseGame, minPlayers: null, maxPlayers: null });
    expect(props.playerCountDisplay).toBeUndefined();
  });

  it('omits playTimeDisplay when both play time values are null', () => {
    const props = buildGameCardProps({
      ...baseGame,
      minPlayTimeMinutes: null,
      maxPlayTimeMinutes: null,
    });
    expect(props.playTimeDisplay).toBeUndefined();
  });

  it('uses only minPlayTime when max is null', () => {
    const props = buildGameCardProps({
      ...baseGame,
      minPlayTimeMinutes: 60,
      maxPlayTimeMinutes: null,
    });
    expect(props.playTimeDisplay).toBe('1h');
  });

  it('maps averageRating and ratingMax', () => {
    const props = buildGameCardProps(baseGame);
    expect(props.rating).toBe(7.5);
    expect(props.ratingMax).toBe(10);
  });

  it('handles publisher-only subtitle', () => {
    const props = buildGameCardProps({ ...baseGame, yearPublished: null });
    expect(props.subtitle).toBe('Kosmos');
  });

  it('handles year-only subtitle', () => {
    const props = buildGameCardProps({ ...baseGame, publisher: null });
    expect(props.subtitle).toBe('1995');
  });

  it('returns undefined subtitle when both publisher and year are null', () => {
    const props = buildGameCardProps({ ...baseGame, publisher: null, yearPublished: null });
    expect(props.subtitle).toBeUndefined();
  });
});
