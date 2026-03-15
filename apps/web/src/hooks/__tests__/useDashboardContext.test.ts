import { describe, it, expect } from 'vitest';
import { computeHeroPriority } from '../useDashboardContext';

describe('computeHeroPriority', () => {
  it('returns active-session with priority 100 when active session exists', () => {
    const result = computeHeroPriority({
      activeSession: {
        id: 's1',
        gameId: 'g1',
        gameName: 'Catan',
        playerCount: 4,
        durationMinutes: 30,
      },
      upcomingGameNight: null,
      incompleteSessions: [],
      lastPlayed: null,
    });
    expect(result).toEqual({
      type: 'active-session',
      priority: 100,
      data: expect.objectContaining({ id: 's1' }),
    });
  });

  it('returns upcoming-game-night with priority 90 when game night < 24h', () => {
    const tomorrow = new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString();
    const result = computeHeroPriority({
      activeSession: null,
      upcomingGameNight: { id: 'gn1', title: 'Venerdì nerd', scheduledAt: tomorrow },
      incompleteSessions: [],
      lastPlayed: null,
    });
    expect(result).toEqual({
      type: 'upcoming-game-night',
      priority: 90,
      data: expect.objectContaining({ id: 'gn1' }),
    });
  });

  it('returns incomplete-session with priority 80 when unfinalised sessions exist', () => {
    const result = computeHeroPriority({
      activeSession: null,
      upcomingGameNight: null,
      incompleteSessions: [{ id: 's2', gameName: 'Catan', sessionDate: '2026-03-12T20:00:00Z' }],
      lastPlayed: null,
    });
    expect(result).toEqual({
      type: 'incomplete-session',
      priority: 80,
      data: expect.objectContaining({ id: 's2' }),
    });
  });

  it('returns last-played with priority 50 when last game exists', () => {
    const result = computeHeroPriority({
      activeSession: null,
      upcomingGameNight: null,
      incompleteSessions: [],
      lastPlayed: {
        id: 'g1',
        title: 'Catan',
        imageUrl: '/img.jpg',
        lastPlayedAt: '2026-03-13T20:00:00Z',
      },
    });
    expect(result).toEqual({
      type: 'last-played',
      priority: 50,
      data: expect.objectContaining({ id: 'g1' }),
    });
  });

  it('returns welcome with priority 10 when nothing exists', () => {
    const result = computeHeroPriority({
      activeSession: null,
      upcomingGameNight: null,
      incompleteSessions: [],
      lastPlayed: null,
    });
    expect(result).toEqual({ type: 'welcome', priority: 10, data: null });
  });

  it('active session wins over game night when both exist', () => {
    const result = computeHeroPriority({
      activeSession: {
        id: 's1',
        gameId: 'g1',
        gameName: 'Catan',
        playerCount: 4,
        durationMinutes: 30,
      },
      upcomingGameNight: {
        id: 'gn1',
        title: 'Venerdì nerd',
        scheduledAt: new Date(Date.now() + 3600000).toISOString(),
      },
      incompleteSessions: [],
      lastPlayed: null,
    });
    expect(result.type).toBe('active-session');
  });
});
