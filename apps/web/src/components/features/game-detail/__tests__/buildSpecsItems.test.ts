/**
 * buildSpecsItems - Unit tests (Issue #1463).
 *
 * Test matrix (Crispin, T7 + T8):
 *   T7. Full data → all 8 items pre-formatted.
 *   T8. Null cascade → 6/8 items '—' placeholder, 2/8 still formatted when present.
 */

import { describe, expect, it } from 'vitest';

import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

import { buildSpecsItems } from '../buildSpecsItems';

const t = (id: string): string => {
  // Simulated i18n map mirroring `apps/web/src/locales/it.json` keys.
  const dict: Record<string, string> = {
    'pages.gameDetail.info.specsPlayers': 'Giocatori',
    'pages.gameDetail.info.specsDuration': 'Durata',
    'pages.gameDetail.info.specsAge': 'Età',
    'pages.gameDetail.info.specsComplexity': 'Complessità',
    'pages.gameDetail.info.specsYear': 'Anno',
    'pages.gameDetail.info.specsDesigner': 'Designer',
    'pages.gameDetail.info.specsPublisher': 'Editore',
    'pages.gameDetail.info.specsRatingBgg': 'Rating BGG',
    'pages.gameDetail.info.specsMinutesUnit': 'min',
  };
  return dict[id] ?? id;
};

function makeDetail(overrides: Partial<LibraryGameDetail> = {}): LibraryGameDetail {
  return {
    libraryEntryId: 'lib-1',
    userId: 'user-1',
    gameId: 'game-1',
    addedAt: '2026-01-01T00:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: 'owned',
    stateChangedAt: null,
    stateNotes: null,
    isAvailableForPlay: true,
    hasCustomPdf: false,
    hasRagAccess: false,
    gameTitle: 'Test Game',
    gamePublisher: null,
    gameYearPublished: 2017,
    gameIconUrl: null,
    gameImageUrl: null,
    description: null,
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
    minAge: 14,
    complexityRating: 2.4,
    averageRating: 8.1,
    timesPlayed: 0,
    lastPlayed: null,
    winRate: null,
    avgDuration: null,
    designers: [{ id: 'd-1', name: 'Stefan Feld' }],
    publishers: [{ id: 'p-1', name: 'Kosmos' }],
    ...overrides,
  };
}

describe('buildSpecsItems (Issue #1463)', () => {
  // ─── T7: Full data ──────────────────────────────────────────────────────────
  it('maps full LibraryGameDetail to 8 formatted spec items (T7)', () => {
    const items = buildSpecsItems(makeDetail(), t);

    expect(items).toHaveLength(8);

    const byKey = Object.fromEntries(items.map(item => [item.key, item]));

    expect(byKey.players).toEqual({ key: 'players', label: 'Giocatori', value: '1–5' });
    expect(byKey.duration).toEqual({ key: 'duration', label: 'Durata', value: '70 min' });
    expect(byKey.age).toEqual({ key: 'age', label: 'Età', value: '14+' });
    expect(byKey.complexity).toEqual({
      key: 'complexity',
      label: 'Complessità',
      value: '2.4 / 5',
    });
    expect(byKey.year).toEqual({ key: 'year', label: 'Anno', value: '2017' });
    expect(byKey.designer).toEqual({ key: 'designer', label: 'Designer', value: 'Stefan Feld' });
    expect(byKey.publisher).toEqual({ key: 'publisher', label: 'Editore', value: 'Kosmos' });
    expect(byKey.rating).toEqual({ key: 'rating', label: 'Rating BGG', value: '8.1' });
  });

  it('renders players as single value when minPlayers === maxPlayers', () => {
    const items = buildSpecsItems(makeDetail({ minPlayers: 4, maxPlayers: 4 }), t);
    const players = items.find(item => item.key === 'players');
    expect(players?.value).toBe('4');
  });

  it('renders only first designer when multiple co-authors present (D4 first-only)', () => {
    const items = buildSpecsItems(
      makeDetail({
        designers: [
          { id: 'd-1', name: 'Stefan Feld' },
          { id: 'd-2', name: 'Klaus Teuber' },
        ],
      }),
      t
    );
    const designer = items.find(item => item.key === 'designer');
    expect(designer?.value).toBe('Stefan Feld');
  });

  it('falls back to gamePublisher when publishers array empty (D5)', () => {
    const items = buildSpecsItems(
      makeDetail({
        publishers: [],
        gamePublisher: 'Legacy Publisher Co.',
      }),
      t
    );
    const publisher = items.find(item => item.key === 'publisher');
    expect(publisher?.value).toBe('Legacy Publisher Co.');
  });

  // ─── T8: Null cascade ───────────────────────────────────────────────────────
  it('returns em-dash placeholder for all missing fields (T8 null cascade)', () => {
    const items = buildSpecsItems(
      makeDetail({
        minPlayers: null,
        maxPlayers: null,
        playingTimeMinutes: null,
        minAge: undefined,
        complexityRating: null,
        gameYearPublished: null,
        designers: [],
        publishers: [],
        gamePublisher: null,
        averageRating: null,
      }),
      t
    );

    expect(items).toHaveLength(8);
    for (const item of items) {
      expect(item.value).toBe('—');
    }
  });

  it('preserves stable item order regardless of data presence (layout stability)', () => {
    const items = buildSpecsItems(
      makeDetail({
        averageRating: null,
        complexityRating: null,
        minAge: undefined,
      }),
      t
    );
    const keys = items.map(item => item.key);
    expect(keys).toEqual([
      'players',
      'duration',
      'age',
      'complexity',
      'year',
      'designer',
      'publisher',
      'rating',
    ]);
  });
});
