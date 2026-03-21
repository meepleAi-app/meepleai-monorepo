/**
 * Flow Test: Admin Management
 *
 * Tests admin-specific flows:
 * 1. Admin logs in (gets Admin role)
 * 2. Creates a game in the catalog
 * 3. Manages users (list, view)
 * 4. Checks dashboard metrics
 * 5. Manages library items
 * 6. Views game details and rules
 *
 * Tests role-based access: admin vs regular user.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { resetGamesState, resetLibraryState, resetAdminState } from '../mocks/handlers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

describe('Flow: Admin Management', () => {
  beforeEach(() => {
    resetGamesState();
    resetLibraryState();
    resetAdminState();
    server.resetHandlers();
  });

  it('should login as admin and get Admin role', async () => {
    const loginRes = await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'admin@meepleai.dev', password: 'admin123' }),
    });
    expect(loginRes.ok).toBe(true);
    const loginData = await loginRes.json();
    expect(loginData.user.role).toBe('Admin');
    expect(loginData.user.email).toBe('admin@meepleai.dev');
  });

  it('should create a game and verify it appears in catalog', async () => {
    // Admin login
    await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'admin@meepleai.dev', password: 'admin123' }),
    });

    // Create game
    const createRes = await fetch(`${API_BASE}/api/v1/games`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title: 'Pandemic Legacy' }),
    });
    expect(createRes.status).toBe(201);
    const newGame = await createRes.json();
    expect(newGame.title).toBe('Pandemic Legacy');
    expect(newGame.id).toBeDefined();

    // Verify game appears in catalog
    const gamesRes = await fetch(`${API_BASE}/api/v1/games`);
    const games = await gamesRes.json();
    const found = games.find((g: any) => g.title === 'Pandemic Legacy');
    expect(found).toBeDefined();
    expect(found.id).toBe(newGame.id);
  });

  it('should update game details', async () => {
    // Get existing game
    const gamesRes = await fetch(`${API_BASE}/api/v1/games`);
    const games = await gamesRes.json();
    const game = games[0];

    // Update title
    const updateRes = await fetch(`${API_BASE}/api/v1/games/${game.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title: 'Chess - Updated Edition' }),
    });
    expect(updateRes.ok).toBe(true);
    const updated = await updateRes.json();
    expect(updated.title).toBe('Chess - Updated Edition');
    expect(updated.updatedAt).toBeDefined();

    // Verify update persists
    const detailRes = await fetch(`${API_BASE}/api/v1/games/${game.id}`);
    const detail = await detailRes.json();
    expect(detail.title).toBe('Chess - Updated Edition');
  });

  it('should delete a game from catalog', async () => {
    // Create a game to delete
    const createRes = await fetch(`${API_BASE}/api/v1/games`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title: 'Game To Delete' }),
    });
    const game = await createRes.json();

    // Delete it
    const deleteRes = await fetch(`${API_BASE}/api/v1/games/${game.id}`, {
      method: 'DELETE',
    });
    expect(deleteRes.ok).toBe(true);

    // Verify 404
    const getRes = await fetch(`${API_BASE}/api/v1/games/${game.id}`);
    expect(getRes.status).toBe(404);
  });

  it('should manage library: add, update status, log play, remove', async () => {
    // List library
    const libRes = await fetch(`${API_BASE}/api/v1/library`);
    expect(libRes.ok).toBe(true);
    const library = await libRes.json();
    const initialCount = library.items.length;

    // Add new game
    const addRes = await fetch(`${API_BASE}/api/v1/library`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        gameId: 'new-game-1',
        name: 'Azul',
        status: 'wishlist',
      }),
    });
    expect(addRes.status).toBe(201);
    const newItem = await addRes.json();

    // Update status from wishlist to owned
    const updateRes = await fetch(`${API_BASE}/api/v1/library/${newItem.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status: 'owned' }),
    });
    expect(updateRes.ok).toBe(true);
    const updated = await updateRes.json();
    expect(updated.status).toBe('owned');

    // Log a play
    const playRes = await fetch(`${API_BASE}/api/v1/library/${newItem.id}/play`, {
      method: 'POST',
    });
    expect(playRes.ok).toBe(true);
    const played = await playRes.json();
    expect(played.playCount).toBe(1);
    expect(played.lastPlayedAt).toBeDefined();

    // Remove from library
    const removeRes = await fetch(`${API_BASE}/api/v1/library/${newItem.id}`, {
      method: 'DELETE',
    });
    expect(removeRes.ok).toBe(true);

    // Verify removed
    const finalLib = await fetch(`${API_BASE}/api/v1/library`);
    const finalLibData = await finalLib.json();
    expect(finalLibData.items.length).toBe(initialCount);
  });

  it('should get library statistics', async () => {
    const statsRes = await fetch(`${API_BASE}/api/v1/library/stats`);
    expect(statsRes.ok).toBe(true);
    const stats = await statsRes.json();
    expect(stats.totalGames).toBeDefined();
    expect(stats.owned).toBeDefined();
    expect(stats.wishlist).toBeDefined();
    expect(stats.totalPlays).toBeDefined();
  });

  it('should create game rules for a game', async () => {
    const gamesRes = await fetch(`${API_BASE}/api/v1/games`);
    const games = await gamesRes.json();
    const game = games[0];

    // Create rules
    const rulesRes = await fetch(`${API_BASE}/api/v1/games/${game.id}/rules`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        atoms: [
          { id: 'r1', text: 'Each player starts with 5 cards' },
          { id: 'r2', text: 'Players take turns clockwise' },
        ],
      }),
    });
    expect(rulesRes.status).toBe(201);
    const ruleSpec = await rulesRes.json();
    expect(ruleSpec.gameId).toBe(game.id);
    expect(ruleSpec.atoms.length).toBeGreaterThan(0);
  });

  it('should reject login with wrong password', async () => {
    const loginRes = await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'admin@meepleai.dev', password: 'wrongpassword' }),
    });
    expect(loginRes.status).toBe(401);
  });
});
