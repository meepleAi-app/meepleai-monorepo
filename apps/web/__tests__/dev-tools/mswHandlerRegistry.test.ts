import { describe, it, expect } from 'vitest';
import { http, HttpResponse } from 'msw';
import { buildActiveHandlers, type HandlerGroup } from '@/dev-tools/mswHandlerRegistry';
import type { ToggleConfig } from '@/dev-tools/types';

const auth: HandlerGroup = {
  name: 'auth',
  handlers: [http.get('/api/v1/auth/me', () => HttpResponse.json({ id: 'MOCK-u' }))],
};

const games: HandlerGroup = {
  name: 'games',
  handlers: [
    http.get('/api/v1/games', () => HttpResponse.json([])),
    http.post('/api/v1/games', () => HttpResponse.json({ id: 'MOCK-g' })),
  ],
};

describe('buildActiveHandlers', () => {
  it('returns all handlers when all groups enabled', () => {
    const toggles: ToggleConfig = { groups: { auth: true, games: true }, overrides: {} };
    const active = buildActiveHandlers([auth, games], toggles);
    expect(active).toHaveLength(3);
  });

  it('excludes handlers from disabled groups', () => {
    const toggles: ToggleConfig = { groups: { auth: true, games: false }, overrides: {} };
    const active = buildActiveHandlers([auth, games], toggles);
    expect(active).toHaveLength(1);
  });

  it('returns empty array when all groups disabled', () => {
    const toggles: ToggleConfig = { groups: { auth: false, games: false }, overrides: {} };
    const active = buildActiveHandlers([auth, games], toggles);
    expect(active).toEqual([]);
  });

  it('treats missing group in toggles as enabled (default safe)', () => {
    const toggles: ToggleConfig = { groups: {}, overrides: {} };
    const active = buildActiveHandlers([auth, games], toggles);
    expect(active).toHaveLength(3);
  });
});
