/**
 * Flow Test: Game Night Organization
 *
 * Tests the complete game night journey:
 * 1. User logs in
 * 2. Creates a game night event
 * 3. Adds games to the playlist
 * 4. Invites players
 * 5. Players RSVP
 * 6. Updates game night details
 * 7. Creates a session from the game night
 *
 * Uses MSW stateful handlers - each action creates context for the next.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { server } from '../mocks/server';
import {
  resetGameNightsState,
  getGameNightsState,
  resetSessionsState,
  resetPlayersState,
} from '../mocks/handlers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

describe('Flow: Game Night Organization', () => {
  beforeEach(() => {
    resetGameNightsState();
    resetSessionsState();
    resetPlayersState();
    server.resetHandlers();
  });

  it('should complete full game night flow: create → invite → play', async () => {
    // Step 1: Login
    const loginRes = await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'user@meepleai.dev', password: 'password123' }),
    });
    expect(loginRes.ok).toBe(true);

    // Step 2: Create a game night
    const createRes = await fetch(`${API_BASE}/api/v1/game-nights`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        title: 'Friday Board Games',
        date: '2024-03-15T19:00:00Z',
        location: "Alice's house",
      }),
    });
    expect(createRes.status).toBe(201);
    const gameNight = await createRes.json();
    expect(gameNight.title).toBe('Friday Board Games');
    expect(gameNight.status).toBe('Planned');
    expect(gameNight.playlist).toHaveLength(0);
    expect(gameNight.participants).toHaveLength(0);

    const nightId = gameNight.id;

    // Step 3: Add games to playlist
    const addGame1Res = await fetch(`${API_BASE}/api/v1/game-nights/${nightId}/playlist`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gameId: 'catan-1', gameName: 'Catan' }),
    });
    expect(addGame1Res.status).toBe(201);

    const addGame2Res = await fetch(`${API_BASE}/api/v1/game-nights/${nightId}/playlist`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gameId: 'ticket-1', gameName: 'Ticket to Ride' }),
    });
    expect(addGame2Res.status).toBe(201);

    // Verify playlist has 2 games
    const nightDetailRes = await fetch(`${API_BASE}/api/v1/game-nights/${nightId}`);
    const nightDetail = await nightDetailRes.json();
    expect(nightDetail.playlist).toHaveLength(2);
    expect(nightDetail.playlist[0].gameName).toBe('Catan');
    expect(nightDetail.playlist[1].gameName).toBe('Ticket to Ride');

    // Step 4: Invite players
    const inviteAliceRes = await fetch(`${API_BASE}/api/v1/game-nights/${nightId}/invitations`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userId: 'user-alice', displayName: 'Alice' }),
    });
    expect(inviteAliceRes.status).toBe(201);
    const aliceInvite = await inviteAliceRes.json();
    expect(aliceInvite.rsvpStatus).toBe('Pending');

    const inviteBobRes = await fetch(`${API_BASE}/api/v1/game-nights/${nightId}/invitations`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userId: 'user-bob', displayName: 'Bob' }),
    });
    expect(inviteBobRes.status).toBe(201);
    const bobInvite = await inviteBobRes.json();

    // Step 5: Players RSVP
    const aliceRsvpRes = await fetch(
      `${API_BASE}/api/v1/game-nights/${nightId}/invitations/${aliceInvite.id}/rsvp`,
      {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: 'Accepted' }),
      }
    );
    expect(aliceRsvpRes.ok).toBe(true);
    const aliceRsvp = await aliceRsvpRes.json();
    expect(aliceRsvp.rsvpStatus).toBe('Accepted');

    const bobRsvpRes = await fetch(
      `${API_BASE}/api/v1/game-nights/${nightId}/invitations/${bobInvite.id}/rsvp`,
      {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: 'Declined' }),
      }
    );
    expect(bobRsvpRes.ok).toBe(true);
    const bobRsvp = await bobRsvpRes.json();
    expect(bobRsvp.rsvpStatus).toBe('Declined');

    // Verify final game night state
    const finalRes = await fetch(`${API_BASE}/api/v1/game-nights/${nightId}`);
    const finalNight = await finalRes.json();
    expect(finalNight.participants).toHaveLength(2);
    expect(finalNight.playlist).toHaveLength(2);

    const accepted = finalNight.participants.filter((p: any) => p.rsvpStatus === 'Accepted');
    expect(accepted).toHaveLength(1);
    expect(accepted[0].displayName).toBe('Alice');

    // Step 6: Start a session for the first game on the playlist
    const sessionRes = await fetch(`${API_BASE}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        gameId: finalNight.playlist[0].gameId,
        gameName: finalNight.playlist[0].gameName,
      }),
    });
    expect(sessionRes.status).toBe(201);
    const session = await sessionRes.json();
    expect(session.gameId).toBe('catan-1');
    expect(session.status).toBe('Active');
  });

  it('should handle removing a game from playlist', async () => {
    // Create game night with a game
    const createRes = await fetch(`${API_BASE}/api/v1/game-nights`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title: 'Test Night', date: '2024-04-01T19:00:00Z' }),
    });
    const night = await createRes.json();

    // Add a game
    const addRes = await fetch(`${API_BASE}/api/v1/game-nights/${night.id}/playlist`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gameId: 'catan-1', gameName: 'Catan' }),
    });
    const playlistItem = await addRes.json();

    // Remove the game
    const removeRes = await fetch(
      `${API_BASE}/api/v1/game-nights/${night.id}/playlist/${playlistItem.id}`,
      { method: 'DELETE' }
    );
    expect(removeRes.ok).toBe(true);

    // Verify empty playlist
    const detailRes = await fetch(`${API_BASE}/api/v1/game-nights/${night.id}`);
    const detail = await detailRes.json();
    expect(detail.playlist).toHaveLength(0);
  });

  it('should list game nights and show in overview', async () => {
    // Create two game nights
    await fetch(`${API_BASE}/api/v1/game-nights`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title: 'Night 1', date: '2024-03-01T19:00:00Z' }),
    });
    await fetch(`${API_BASE}/api/v1/game-nights`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title: 'Night 2', date: '2024-03-08T19:00:00Z' }),
    });

    // List all
    const listRes = await fetch(`${API_BASE}/api/v1/game-nights`);
    const list = await listRes.json();
    expect(list.items).toHaveLength(2);
    expect(list.totalCount).toBe(2);
  });

  it('should delete a game night', async () => {
    // Create
    const createRes = await fetch(`${API_BASE}/api/v1/game-nights`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title: 'To Delete', date: '2024-04-01T19:00:00Z' }),
    });
    const night = await createRes.json();

    // Delete
    const deleteRes = await fetch(`${API_BASE}/api/v1/game-nights/${night.id}`, {
      method: 'DELETE',
    });
    expect(deleteRes.ok).toBe(true);

    // Verify gone
    const getRes = await fetch(`${API_BASE}/api/v1/game-nights/${night.id}`);
    expect(getRes.status).toBe(404);
  });
});
