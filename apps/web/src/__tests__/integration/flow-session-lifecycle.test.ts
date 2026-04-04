/**
 * Flow Test: Session Lifecycle
 *
 * Tests the complete game session journey:
 * 1. User logs in
 * 2. Creates a game session for a specific game
 * 3. Adds players to the session
 * 4. Records scores for each player
 * 5. Uses session tools (dice, cards, notes)
 * 6. Pauses and resumes the session
 * 7. Completes the session with final scores
 *
 * Uses MSW stateful handlers - each action mutates server state.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { server } from '../mocks/server';
import { resetSessionsState, getSessionsState } from '../mocks/handlers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

describe('Flow: Session Lifecycle', () => {
  beforeEach(() => {
    resetSessionsState();
    server.resetHandlers();
  });

  it('should complete full session lifecycle: create → play → complete', async () => {
    // Step 1: Login
    const loginRes = await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'user@meepleai.dev', password: 'password123' }),
    });
    expect(loginRes.ok).toBe(true);

    // Step 2: Create a new session
    const createRes = await fetch(`${API_BASE}/api/v1/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        gameId: 'demo-chess',
        gameName: 'Chess',
      }),
    });
    expect(createRes.status).toBe(201);
    const session = await createRes.json();
    expect(session.id).toBeDefined();
    expect(session.sessionCode).toBeDefined();
    expect(session.status).toBe('Active');
    expect(session.participants).toHaveLength(1); // Owner auto-added

    const sessionId = session.id;

    // Step 3: Add players
    const addPlayerRes = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/participants`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId: 'player-alice', displayName: 'Alice' }),
    });
    expect(addPlayerRes.status).toBe(201);

    const addPlayer2Res = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/participants`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId: 'player-bob', displayName: 'Bob' }),
    });
    expect(addPlayer2Res.status).toBe(201);

    // Verify session now has 3 participants
    const sessionDetailRes = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}`);
    const sessionDetail = await sessionDetailRes.json();
    expect(sessionDetail.participants).toHaveLength(3);

    // Step 4: Record scores
    const scoreRes1 = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/scores`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ participantId: 'player-alice', score: 150 }),
    });
    expect(scoreRes1.ok).toBe(true);
    const score1 = await scoreRes1.json();
    expect(score1.totalScore).toBe(150);

    const scoreRes2 = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/scores`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ participantId: 'player-bob', score: 120 }),
    });
    expect(scoreRes2.ok).toBe(true);

    // Step 5: Use session tools - Roll dice
    const diceRes = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/dice`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ count: 2, sides: 6 }),
    });
    expect(diceRes.ok).toBe(true);
    const dice = await diceRes.json();
    expect(dice.results).toHaveLength(2);
    expect(dice.total).toBe(dice.results[0] + dice.results[1]);
    dice.results.forEach((r: number) => {
      expect(r).toBeGreaterThanOrEqual(1);
      expect(r).toBeLessThanOrEqual(6);
    });

    // Step 6: Draw cards
    const cardsRes = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/cards/draw`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ count: 3 }),
    });
    expect(cardsRes.ok).toBe(true);
    const cards = await cardsRes.json();
    expect(cards.cards).toHaveLength(3);
    cards.cards.forEach((card: any) => {
      expect(card.suit).toBeDefined();
      expect(card.value).toBeDefined();
    });

    // Step 7: Add a note
    const noteRes = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/notes`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text: 'Alice played an aggressive opening' }),
    });
    expect(noteRes.ok).toBe(true);

    // Step 8: Complete the session
    const completeRes = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/complete`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ winnerName: 'Alice' }),
    });
    expect(completeRes.ok).toBe(true);
    const completed = await completeRes.json();
    expect(completed.status).toBe('Finalized');
    expect(completed.completedAt).toBeDefined();
  });

  it('should handle pause and resume flow', async () => {
    // Use existing session from initial state
    const sessionId = 'session-1';

    // Pause
    const pauseRes = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/pause`, {
      method: 'POST',
    });
    expect(pauseRes.ok).toBe(true);
    const paused = await pauseRes.json();
    expect(paused.status).toBe('Paused');

    // Verify paused state persists
    const detailRes = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}`);
    const detail = await detailRes.json();
    expect(detail.status).toBe('Paused');

    // Resume
    const resumeRes = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/resume`, {
      method: 'POST',
    });
    expect(resumeRes.ok).toBe(true);
    const resumed = await resumeRes.json();
    expect(resumed.status).toBe('Active');
  });

  it('should handle removing a participant', async () => {
    const sessionId = 'session-1';

    // Session starts with 2 participants (Alice, Bob)
    const before = await (await fetch(`${API_BASE}/api/v1/sessions/${sessionId}`)).json();
    expect(before.participants).toHaveLength(2);

    // Remove Bob
    const removeRes = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/participants/p2`, {
      method: 'DELETE',
    });
    expect(removeRes.ok).toBe(true);

    // Verify participant removed
    const after = await (await fetch(`${API_BASE}/api/v1/sessions/${sessionId}`)).json();
    expect(after.participants).toHaveLength(1);
    expect(after.participants[0].displayName).toBe('Alice');
  });

  it('should return 404 for non-existent session', async () => {
    const res = await fetch(`${API_BASE}/api/v1/sessions/nonexistent`);
    expect(res.status).toBe(404);
  });

  it('should accumulate scores across multiple rounds', async () => {
    const sessionId = 'session-1';

    // Round 1
    await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/scores`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ participantId: 'p1', score: 50 }),
    });

    // Round 2
    const round2Res = await fetch(`${API_BASE}/api/v1/sessions/${sessionId}/scores`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ participantId: 'p1', score: 75 }),
    });
    const round2 = await round2Res.json();
    expect(round2.totalScore).toBe(125); // 50 + 75
  });
});
