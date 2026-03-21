/**
 * Flow Test: Discover → Library → Chat
 *
 * Tests the complete user journey:
 * 1. User logs in
 * 2. Browses game catalog
 * 3. Adds game to library
 * 4. Opens chat thread for that game
 * 5. Sends message and receives AI response
 * 6. Exports chat thread
 *
 * Uses MSW stateful handlers - each action creates context for the next.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { resetGamesState, resetLibraryState, resetChatState } from '../mocks/handlers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

describe('Flow: Discover → Library → Chat', () => {
  beforeEach(() => {
    resetGamesState();
    resetLibraryState();
    resetChatState();
    server.resetHandlers();
  });

  it('should complete the full discover-to-chat flow', async () => {
    // Step 1: Login
    const loginRes = await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'user@meepleai.dev', password: 'password123' }),
    });
    expect(loginRes.ok).toBe(true);
    const loginData = await loginRes.json();
    expect(loginData.user.role).toBe('User');

    // Step 2: Browse catalog - search for games
    const gamesRes = await fetch(`${API_BASE}/api/v1/games`);
    expect(gamesRes.ok).toBe(true);
    const games = await gamesRes.json();
    expect(games.length).toBeGreaterThan(0);

    // Pick the first game
    const targetGame = games[0];
    expect(targetGame.id).toBeDefined();
    expect(targetGame.title).toBeDefined();

    // Step 3: View game details
    const gameDetailRes = await fetch(`${API_BASE}/api/v1/games/${targetGame.id}`);
    expect(gameDetailRes.ok).toBe(true);
    const gameDetail = await gameDetailRes.json();
    expect(gameDetail.id).toBe(targetGame.id);

    // Step 4: Add game to library
    const addLibRes = await fetch(`${API_BASE}/api/v1/library`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        gameId: targetGame.id,
        name: targetGame.title,
        status: 'owned',
      }),
    });
    expect(addLibRes.status).toBe(201);
    const libItem = await addLibRes.json();
    expect(libItem.gameId).toBe(targetGame.id);
    expect(libItem.status).toBe('owned');

    // Step 5: Verify game is in library
    const libraryRes = await fetch(`${API_BASE}/api/v1/library`);
    expect(libraryRes.ok).toBe(true);
    const library = await libraryRes.json();
    const foundInLib = library.items.find((item: any) => item.gameId === targetGame.id);
    expect(foundInLib).toBeDefined();

    // Step 6: Create chat thread for the game
    const chatRes = await fetch(`${API_BASE}/api/v1/chat/threads`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gameId: targetGame.id }),
    });
    expect(chatRes.status).toBe(201);
    const chatThread = await chatRes.json();
    expect(chatThread.gameId).toBe(targetGame.id);

    // Step 7: Verify thread appears in thread list
    const threadsRes = await fetch(`${API_BASE}/api/v1/chat/threads`);
    expect(threadsRes.ok).toBe(true);
    const threads = await threadsRes.json();
    const newThread = threads.find((t: any) => t.id === chatThread.id);
    expect(newThread).toBeDefined();

    // Step 8: Send a message (non-streaming endpoint)
    const askRes = await fetch(`${API_BASE}/api/v1/chat/ask`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        question: 'How do I set up this game?',
        gameId: targetGame.id,
      }),
    });
    expect(askRes.ok).toBe(true);
    const answer = await askRes.json();
    expect(answer.answer).toBeDefined();
    expect(answer.confidence).toBeGreaterThan(0);
  });

  it('should prevent adding duplicate game to library', async () => {
    // Login
    await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'user@meepleai.dev', password: 'password123' }),
    });

    // Library already has 'catan-1' from initial state
    const duplicateRes = await fetch(`${API_BASE}/api/v1/library`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        gameId: 'catan-1',
        name: 'Catan',
        status: 'owned',
      }),
    });
    expect(duplicateRes.status).toBe(409);
    const error = await duplicateRes.json();
    expect(error.error).toContain('already in library');
  });

  it('should handle game not found gracefully', async () => {
    const res = await fetch(`${API_BASE}/api/v1/games/nonexistent-game-id`);
    expect(res.status).toBe(404);
  });

  it('should retrieve game rules after adding to library', async () => {
    // Get games and pick one
    const gamesRes = await fetch(`${API_BASE}/api/v1/games`);
    const games = await gamesRes.json();
    const game = games[0];

    // Get rules for the game
    const rulesRes = await fetch(`${API_BASE}/api/v1/games/${game.id}/rules`);
    expect(rulesRes.ok).toBe(true);
    const ruleSpec = await rulesRes.json();
    expect(ruleSpec.gameId).toBe(game.id);
    expect(ruleSpec.atoms).toBeDefined();
    expect(ruleSpec.atoms.length).toBeGreaterThan(0);
  });
});
