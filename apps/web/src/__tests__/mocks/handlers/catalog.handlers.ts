/**
 * MSW handlers for catalog filter endpoints
 *
 * Covers: /api/v1/games/categories, /api/v1/games/mechanics, etc.
 * - List available categories
 * - List available mechanics
 * - Get complexity ranges
 * - Get player count options
 *
 * Issue #2760: MSW Infrastructure Setup
 */

import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Categories for filtering
const categories = [
  { id: 'strategy', name: 'Strategy', count: 150 },
  { id: 'family', name: 'Family', count: 120 },
  { id: 'party', name: 'Party Games', count: 80 },
  { id: 'thematic', name: 'Thematic', count: 95 },
  { id: 'wargame', name: 'Wargame', count: 45 },
  { id: 'abstract', name: 'Abstract', count: 60 },
  { id: 'economic', name: 'Economic', count: 55 },
  { id: 'card-game', name: 'Card Game', count: 110 },
  { id: 'dice', name: 'Dice Games', count: 40 },
  { id: 'cooperative', name: 'Cooperative', count: 70 },
];

// Mechanics for filtering
const mechanics = [
  { id: 'worker-placement', name: 'Worker Placement', count: 85 },
  { id: 'deck-building', name: 'Deck Building', count: 65 },
  { id: 'area-control', name: 'Area Control', count: 90 },
  { id: 'dice-rolling', name: 'Dice Rolling', count: 120 },
  { id: 'hand-management', name: 'Hand Management', count: 150 },
  { id: 'set-collection', name: 'Set Collection', count: 95 },
  { id: 'tile-placement', name: 'Tile Placement', count: 70 },
  { id: 'drafting', name: 'Drafting', count: 55 },
  { id: 'engine-building', name: 'Engine Building', count: 60 },
  { id: 'route-building', name: 'Route Building', count: 35 },
  { id: 'auction', name: 'Auction/Bidding', count: 40 },
  { id: 'trading', name: 'Trading', count: 50 },
  { id: 'variable-player-powers', name: 'Variable Player Powers', count: 75 },
  { id: 'modular-board', name: 'Modular Board', count: 45 },
  { id: 'pattern-building', name: 'Pattern Building', count: 30 },
];

// Complexity ranges for filtering
const complexityRanges = [
  { id: 'light', label: 'Light (1.0 - 2.0)', min: 1.0, max: 2.0, count: 85 },
  { id: 'medium-light', label: 'Medium Light (2.0 - 2.5)', min: 2.0, max: 2.5, count: 120 },
  { id: 'medium', label: 'Medium (2.5 - 3.0)', min: 2.5, max: 3.0, count: 95 },
  { id: 'medium-heavy', label: 'Medium Heavy (3.0 - 3.5)', min: 3.0, max: 3.5, count: 60 },
  { id: 'heavy', label: 'Heavy (3.5 - 5.0)', min: 3.5, max: 5.0, count: 40 },
];

// Player count options
const playerCounts = [
  { id: '1', label: '1 Player (Solo)', value: 1, count: 45 },
  { id: '2', label: '2 Players', value: 2, count: 180 },
  { id: '3', label: '3 Players', value: 3, count: 150 },
  { id: '4', label: '4 Players', value: 4, count: 160 },
  { id: '5', label: '5 Players', value: 5, count: 90 },
  { id: '6+', label: '6+ Players', value: 6, count: 55 },
];

// Playing time ranges
const playingTimeRanges = [
  { id: 'quick', label: 'Quick (< 30 min)', min: 0, max: 30, count: 65 },
  { id: 'short', label: 'Short (30-60 min)', min: 30, max: 60, count: 120 },
  { id: 'medium', label: 'Medium (1-2 hours)', min: 60, max: 120, count: 110 },
  { id: 'long', label: 'Long (2-3 hours)', min: 120, max: 180, count: 55 },
  { id: 'epic', label: 'Epic (3+ hours)', min: 180, max: 999, count: 25 },
];

export const catalogHandlers = [
  // GET /api/v1/games/categories - List all categories
  http.get(`${API_BASE}/api/v1/games/categories`, () => {
    return HttpResponse.json(categories);
  }),

  // GET /api/v1/games/mechanics - List all mechanics
  http.get(`${API_BASE}/api/v1/games/mechanics`, () => {
    return HttpResponse.json(mechanics);
  }),

  // GET /api/v1/games/complexity-ranges - Get complexity ranges
  http.get(`${API_BASE}/api/v1/games/complexity-ranges`, () => {
    return HttpResponse.json(complexityRanges);
  }),

  // GET /api/v1/games/player-counts - Get player count options
  http.get(`${API_BASE}/api/v1/games/player-counts`, () => {
    return HttpResponse.json(playerCounts);
  }),

  // GET /api/v1/games/playing-time-ranges - Get playing time ranges
  http.get(`${API_BASE}/api/v1/games/playing-time-ranges`, () => {
    return HttpResponse.json(playingTimeRanges);
  }),

  // GET /api/v1/games/filters - Get all filter options at once
  http.get(`${API_BASE}/api/v1/games/filters`, () => {
    return HttpResponse.json({
      categories,
      mechanics,
      complexityRanges,
      playerCounts,
      playingTimeRanges,
    });
  }),

  // GET /api/v1/games/designers - Search designers
  http.get(`${API_BASE}/api/v1/games/designers`, ({ request }) => {
    const url = new URL(request.url);
    const query = url.searchParams.get('q') || '';

    const designers = [
      { id: 'uwe-rosenberg', name: 'Uwe Rosenberg', gameCount: 45 },
      { id: 'stefan-feld', name: 'Stefan Feld', gameCount: 38 },
      { id: 'reiner-knizia', name: 'Reiner Knizia', gameCount: 120 },
      { id: 'vital-lacerda', name: 'Vital Lacerda', gameCount: 12 },
      { id: 'jamey-stegmaier', name: 'Jamey Stegmaier', gameCount: 8 },
      { id: 'elizabeth-hargrave', name: 'Elizabeth Hargrave', gameCount: 3 },
      { id: 'michael-kiesling', name: 'Michael Kiesling', gameCount: 25 },
      { id: 'wolfgang-kramer', name: 'Wolfgang Kramer', gameCount: 85 },
    ];

    const filtered = query
      ? designers.filter((d) => d.name.toLowerCase().includes(query.toLowerCase()))
      : designers;

    return HttpResponse.json(filtered);
  }),

  // GET /api/v1/games/publishers - Search publishers
  http.get(`${API_BASE}/api/v1/games/publishers`, ({ request }) => {
    const url = new URL(request.url);
    const query = url.searchParams.get('q') || '';

    const publishers = [
      { id: 'stonemaier', name: 'Stonemaier Games', gameCount: 15 },
      { id: 'asmodee', name: 'Asmodee', gameCount: 200 },
      { id: 'cmon', name: 'CMON', gameCount: 45 },
      { id: 'days-of-wonder', name: 'Days of Wonder', gameCount: 25 },
      { id: 'fantasy-flight', name: 'Fantasy Flight Games', gameCount: 150 },
      { id: 'z-man', name: 'Z-Man Games', gameCount: 80 },
      { id: 'kosmos', name: 'Kosmos', gameCount: 60 },
      { id: 'plan-b', name: 'Plan B Games', gameCount: 20 },
    ];

    const filtered = query
      ? publishers.filter((p) => p.name.toLowerCase().includes(query.toLowerCase()))
      : publishers;

    return HttpResponse.json(filtered);
  }),
];

// Export filter data for use in tests
export const mockCategories = categories;
export const mockMechanics = mechanics;
export const mockComplexityRanges = complexityRanges;
export const mockPlayerCounts = playerCounts;
export const mockPlayingTimeRanges = playingTimeRanges;
