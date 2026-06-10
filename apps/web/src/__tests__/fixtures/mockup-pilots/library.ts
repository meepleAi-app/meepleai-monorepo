/**
 * Library page-mock fixtures (DS-17 Phase 2.5 — argTypes matrix pattern).
 *
 * Consumed by `_content.stories.tsx` con axis matrix:
 *   tab: 'all' | 'game' | 'agent' | 'player'
 *   view: 'grid' | 'list'
 *   bulk: boolean
 *   drawer: boolean
 *   state: 'default' | 'empty-first-run' | 'empty-filtered' | 'empty-tab-agents' | 'loading' | 'error'
 *
 * Stage axis discovery: grep stateOverride|initialTab|initialView|drawerOpen|withBulk
 *   in admin-mockups/design_files/sp4-library-desktop.jsx (frames 09-17).
 *
 * Refs: spec docs/superpowers/specs/2026-06-10-ds-17-phase-2.5-and-3-redesign.md, umbrella #2063.
 */

import type { Game } from '@/lib/api/schemas/games.schemas';

const baseGame = (overrides: Partial<Game>): Game => ({
  id: '00000000-0000-0000-0000-000000000000',
  title: 'Sample Game',
  publisher: null,
  yearPublished: null,
  minPlayers: 1,
  maxPlayers: 4,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 90,
  bggId: 0,
  createdAt: '2026-06-09T00:00:00.000Z',
  ...overrides,
});

// 10 games for grid coverage (full library "default" state)
export const MOCK_LIBRARY_GAMES: Game[] = [
  baseGame({
    id: '11111111-1111-1111-1111-111111111111',
    title: 'Wingspan',
    publisher: 'Stonemaier',
    yearPublished: 2019,
    minPlayers: 1,
    maxPlayers: 5,
    averageRating: 4.2,
  }),
  baseGame({
    id: '22222222-2222-2222-2222-222222222222',
    title: 'Catan',
    publisher: 'Kosmos',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    averageRating: 3.8,
  }),
  baseGame({
    id: '33333333-3333-3333-3333-333333333333',
    title: 'Paleo',
    publisher: 'Hans im Glück',
    yearPublished: 2020,
    minPlayers: 1,
    maxPlayers: 4,
    averageRating: 4.1,
  }),
  baseGame({
    id: '44444444-4444-4444-4444-444444444444',
    title: 'Codenames',
    publisher: 'CGE',
    yearPublished: 2015,
    minPlayers: 2,
    maxPlayers: 8,
    averageRating: 4.0,
  }),
  baseGame({
    id: '55555555-5555-5555-5555-555555555555',
    title: 'Azul',
    publisher: 'Plan B',
    yearPublished: 2017,
    minPlayers: 2,
    maxPlayers: 4,
    averageRating: 4.0,
  }),
  baseGame({
    id: '66666666-6666-6666-6666-666666666666',
    title: 'Power Grid',
    publisher: 'Rio Grande',
    yearPublished: 2004,
    minPlayers: 2,
    maxPlayers: 6,
    averageRating: 4.1,
  }),
  baseGame({
    id: '77777777-7777-7777-7777-777777777777',
    title: 'Puerto Rico',
    publisher: 'Rio Grande',
    yearPublished: 2002,
    minPlayers: 3,
    maxPlayers: 5,
    averageRating: 4.3,
  }),
  baseGame({
    id: '88888888-8888-8888-8888-888888888888',
    title: 'Zombicide',
    publisher: 'CMON',
    yearPublished: 2012,
    minPlayers: 1,
    maxPlayers: 6,
    averageRating: 3.9,
  }),
  baseGame({
    id: '99999999-9999-9999-9999-999999999999',
    title: 'Carcassonne',
    publisher: 'Hans im Glück',
    yearPublished: 2000,
    minPlayers: 2,
    maxPlayers: 5,
    averageRating: 3.8,
  }),
  baseGame({
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    title: 'Splendor',
    publisher: 'Space Cowboys',
    yearPublished: 2014,
    minPlayers: 2,
    maxPlayers: 4,
    averageRating: 3.9,
  }),
];

export const MOCK_LIBRARY_GAMES_EMPTY: Game[] = [];
