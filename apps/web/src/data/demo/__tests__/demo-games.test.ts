/**
 * Demo Games Fixture Tests
 * Issue #4781: Verify fixtures are complete and type-safe
 */

import { describe, it, expect } from 'vitest';
import { SharedGameSchema } from '@/lib/api/schemas/shared-games.schemas';

import {
  DEMO_GAMES,
  DEMO_GAMES_BY_ID,
  DEMO_GAME_IDS,
  demoCatan,
  demoDescent,
  demoTicketToRide,
  demoPandemic,
} from '../demo-games';

describe('demo-games', () => {
  // --------------------------------------------------------------------------
  // Structure
  // --------------------------------------------------------------------------

  it('exports exactly 4 demo games', () => {
    expect(DEMO_GAMES).toHaveLength(4);
  });

  it('exports stable game IDs', () => {
    expect(Object.keys(DEMO_GAME_IDS)).toEqual([
      'catan',
      'descent',
      'ticketToRide',
      'pandemic',
    ]);
  });

  it('has unique IDs for all games', () => {
    const ids = DEMO_GAMES.map(g => g.id);
    expect(new Set(ids).size).toBe(4);
  });

  it('DEMO_GAMES_BY_ID maps all games correctly', () => {
    for (const game of DEMO_GAMES) {
      expect(DEMO_GAMES_BY_ID[game.id]).toBe(game);
    }
  });

  // --------------------------------------------------------------------------
  // Zod Schema Validation
  // --------------------------------------------------------------------------

  it.each(DEMO_GAMES)('$title passes SharedGame Zod schema validation', (game) => {
    const result = SharedGameSchema.safeParse(game);
    expect(result.success).toBe(true);
  });

  // --------------------------------------------------------------------------
  // Individual Games
  // --------------------------------------------------------------------------

  describe('Catan', () => {
    it('has correct metadata', () => {
      expect(demoCatan.title).toBe('Catan');
      expect(demoCatan.bggId).toBe(13);
      expect(demoCatan.minPlayers).toBe(3);
      expect(demoCatan.maxPlayers).toBe(4);
      expect(demoCatan.yearPublished).toBe(1995);
      expect(demoCatan.status).toBe('Published');
    });

    it('has Italian description', () => {
      expect(demoCatan.description).toContain('isola di Catan');
    });

    it('has valid rating range', () => {
      expect(demoCatan.averageRating).toBeGreaterThanOrEqual(0);
      expect(demoCatan.averageRating).toBeLessThanOrEqual(10);
      expect(demoCatan.complexityRating).toBeGreaterThanOrEqual(0);
      expect(demoCatan.complexityRating).toBeLessThanOrEqual(5);
    });
  });

  describe('Descent', () => {
    it('has correct metadata', () => {
      expect(demoDescent.title).toContain('Descent');
      expect(demoDescent.minPlayers).toBe(1);
      expect(demoDescent.maxPlayers).toBe(4);
    });

    it('has Italian description', () => {
      expect(demoDescent.description).toContain('cooperativo');
    });
  });

  describe('Ticket to Ride', () => {
    it('has correct metadata', () => {
      expect(demoTicketToRide.title).toBe('Ticket to Ride');
      expect(demoTicketToRide.minPlayers).toBe(2);
      expect(demoTicketToRide.maxPlayers).toBe(5);
    });

    it('has Italian description', () => {
      expect(demoTicketToRide.description).toContain('ferroviaria');
    });
  });

  describe('Pandemic', () => {
    it('has correct metadata', () => {
      expect(demoPandemic.title).toBe('Pandemic');
      expect(demoPandemic.minPlayers).toBe(2);
      expect(demoPandemic.maxPlayers).toBe(4);
    });

    it('has Italian description', () => {
      expect(demoPandemic.description).toContain('virus');
    });
  });

  // --------------------------------------------------------------------------
  // Data Quality
  // --------------------------------------------------------------------------

  it('all games have non-empty image URLs', () => {
    for (const game of DEMO_GAMES) {
      expect(game.imageUrl).toBeTruthy();
      expect(game.thumbnailUrl).toBeTruthy();
    }
  });

  it('all games have Published status', () => {
    for (const game of DEMO_GAMES) {
      expect(game.status).toBe('Published');
    }
  });

  it('all games have positive playing time', () => {
    for (const game of DEMO_GAMES) {
      expect(game.playingTimeMinutes).toBeGreaterThan(0);
    }
  });

  it('all games have valid player counts (min <= max)', () => {
    for (const game of DEMO_GAMES) {
      expect(game.minPlayers).toBeLessThanOrEqual(game.maxPlayers);
    }
  });
});
