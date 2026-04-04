/**
 * MSW handlers for catalog filter endpoints (browser-safe)
 * Covers: /api/v1/games/categories, /api/v1/games/mechanics, etc.
 */
import { http, HttpResponse } from 'msw';

import { HANDLER_BASE } from '../data/factories';

const API_BASE = HANDLER_BASE;

const categories = [
  { id: 'strategy', name: 'Strategy', count: 150 },
  { id: 'family', name: 'Family', count: 120 },
  { id: 'party', name: 'Party Games', count: 80 },
  { id: 'thematic', name: 'Thematic', count: 95 },
  { id: 'cooperative', name: 'Cooperative', count: 70 },
  { id: 'abstract', name: 'Abstract', count: 60 },
  { id: 'card-game', name: 'Card Game', count: 110 },
];

const mechanics = [
  { id: 'worker-placement', name: 'Worker Placement', count: 85 },
  { id: 'deck-building', name: 'Deck Building', count: 65 },
  { id: 'area-control', name: 'Area Control', count: 90 },
  { id: 'dice-rolling', name: 'Dice Rolling', count: 120 },
  { id: 'hand-management', name: 'Hand Management', count: 150 },
  { id: 'engine-building', name: 'Engine Building', count: 60 },
  { id: 'drafting', name: 'Drafting', count: 55 },
];

const complexityRanges = [
  { id: 'light', label: 'Light (1.0–2.0)', min: 1.0, max: 2.0, count: 85 },
  { id: 'medium-light', label: 'Medium Light (2.0–2.5)', min: 2.0, max: 2.5, count: 120 },
  { id: 'medium', label: 'Medium (2.5–3.0)', min: 2.5, max: 3.0, count: 95 },
  { id: 'medium-heavy', label: 'Medium Heavy (3.0–3.5)', min: 3.0, max: 3.5, count: 60 },
  { id: 'heavy', label: 'Heavy (3.5–5.0)', min: 3.5, max: 5.0, count: 40 },
];

const playerCounts = [
  { id: '1', label: '1 Player (Solo)', value: 1, count: 45 },
  { id: '2', label: '2 Players', value: 2, count: 180 },
  { id: '3', label: '3 Players', value: 3, count: 150 },
  { id: '4', label: '4 Players', value: 4, count: 160 },
  { id: '6+', label: '6+ Players', value: 6, count: 55 },
];

const playingTimeRanges = [
  { id: 'quick', label: 'Quick (< 30 min)', min: 0, max: 30, count: 65 },
  { id: 'short', label: 'Short (30–60 min)', min: 30, max: 60, count: 120 },
  { id: 'medium', label: 'Medium (1–2 hours)', min: 60, max: 120, count: 110 },
  { id: 'long', label: 'Long (2–3 hours)', min: 120, max: 180, count: 55 },
  { id: 'epic', label: 'Epic (3+ hours)', min: 180, max: 999, count: 25 },
];

export const catalogHandlers = [
  http.get(`${API_BASE}/api/v1/games/categories`, () => HttpResponse.json(categories)),
  http.get(`${API_BASE}/api/v1/games/mechanics`, () => HttpResponse.json(mechanics)),
  http.get(`${API_BASE}/api/v1/games/complexity-ranges`, () => HttpResponse.json(complexityRanges)),
  http.get(`${API_BASE}/api/v1/games/player-counts`, () => HttpResponse.json(playerCounts)),
  http.get(`${API_BASE}/api/v1/games/playing-time-ranges`, () =>
    HttpResponse.json(playingTimeRanges)
  ),
];
