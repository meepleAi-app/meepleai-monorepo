/**
 * #4113 — la mappatura `LiveSessionDto` → `Session`.
 *
 * Il difetto che questi test impediscono di ripetere non era un errore di calcolo: era un
 * `as unknown as` che faceva accettare al compilatore due forme diverse in quattro campi. A
 * runtime `sessionDate` era `undefined` e la pagina della sessione mostrava l'error boundary
 * per **qualunque** sessione.
 *
 * Ogni test qui corrisponde a uno dei quattro campi che il cast nascondeva.
 */

import { describe, it, expect } from 'vitest';

import type { LiveSessionDto, LiveSessionStatus } from '@/lib/api/schemas/live-sessions.schemas';
import { liveSessionToUiSession, uiSessionStatus } from '@/lib/sessions/live-session-to-ui';

function dto(overrides: Partial<LiveSessionDto> = {}): LiveSessionDto {
  return {
    id: '11111111-1111-4111-8111-111111111111',
    sessionCode: 'ABC123',
    gameId: '22222222-2222-4222-8222-222222222222',
    gameName: '7 Wonders',
    gameSlug: null,
    createdByUserId: '33333333-3333-4333-8333-333333333333',
    status: 'InProgress',
    visibility: 'Private',
    groupId: null,
    createdAt: '2026-10-01T10:00:00.000Z',
    startedAt: '2026-10-02T18:30:00.000Z',
    pausedAt: null,
    completedAt: null,
    updatedAt: '2026-10-02T18:30:00.000Z',
    lastSavedAt: null,
    currentTurnIndex: 1,
    currentTurnPlayerId: null,
    agentMode: 'None',
    notes: null,
    players: [],
    teams: [],
    roundScores: [],
    scoringConfig: null,
    gameState: null,
    ...overrides,
  } as unknown as LiveSessionDto;
}

describe('liveSessionToUiSession', () => {
  it('ricava sessionDate da startedAt: era il campo che mancava e faceva lanciare la pagina', () => {
    const ui = liveSessionToUiSession(dto());

    expect(ui.sessionDate).toBeInstanceOf(Date);
    expect(ui.sessionDate.toISOString()).toBe('2026-10-02T18:30:00.000Z');
  });

  it('prima dell avvio ricade su createdAt invece di restare undefined', () => {
    const ui = liveSessionToUiSession(dto({ startedAt: null }));

    expect(ui.sessionDate.toISOString()).toBe('2026-10-01T10:00:00.000Z');
  });

  it('deriva sessionType dal legame col catalogo', () => {
    expect(liveSessionToUiSession(dto()).sessionType).toBe('GameSpecific');
    expect(liveSessionToUiSession(dto({ gameId: null })).sessionType).toBe('Generic');
  });

  it('conta solo i partecipanti attivi', () => {
    // I giocatori rimossi restano in lista con `isActive: false` (#2561): contarli mostrerebbe
    // più partecipanti di quanti stanno giocando.
    const players = [
      { id: 'a', isActive: true },
      { id: 'b', isActive: false },
      { id: 'c', isActive: true },
    ] as unknown as LiveSessionDto['players'];

    expect(liveSessionToUiSession(dto({ players })).participantCount).toBe(2);
  });
});

describe('uiSessionStatus', () => {
  it.each([
    ['Created', 'Active'],
    ['Setup', 'Active'],
    ['InProgress', 'Active'],
    ['Paused', 'Paused'],
    ['Completed', 'Finalized'],
  ] as const)('traduce %s in %s', (domain, ui) => {
    expect(uiSessionStatus(domain as LiveSessionStatus)).toBe(ui);
  });

  it('non lascia passare uno stato del dominio non tradotto', () => {
    // `SessionHeader` indicizza `statusColors[status]` con una mappa di tre chiavi: inoltrare
    // `Created` dava `undefined`, cioè nessuno stile. Tutti gli stati del dominio devono
    // finire in una delle tre chiavi — questo test fallisce se qualcuno ne aggiunge uno e
    // dimentica la traduzione (il `never` nel default lo blocca già a compile time).
    const domainStatuses: LiveSessionStatus[] = [
      'Created',
      'Setup',
      'InProgress',
      'Paused',
      'Completed',
    ];
    const uiStatuses = new Set(domainStatuses.map(uiSessionStatus));

    expect([...uiStatuses].sort()).toEqual(['Active', 'Finalized', 'Paused']);
  });
});
