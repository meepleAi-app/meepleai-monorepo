/**
 * MSW handlers for game night endpoints
 *
 * Covers: /api/v1/game-nights/* routes
 * - Game night lifecycle: create, update, delete
 * - Invitations: send, RSVP
 * - Playlist: add/remove/reorder games
 */

import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface GameNight {
  id: string;
  title: string;
  date: string;
  location?: string;
  status: 'Planned' | 'InProgress' | 'Completed' | 'Cancelled';
  organizerId: string;
  participants: Array<{
    id: string;
    userId: string;
    displayName: string;
    rsvpStatus: 'Pending' | 'Accepted' | 'Declined';
  }>;
  playlist: Array<{
    id: string;
    gameId: string;
    gameName: string;
    order: number;
  }>;
  createdAt: string;
}

let gameNights: GameNight[] = [];

export const gameNightsHandlers = [
  // GET /api/v1/game-nights
  http.get(`${API_BASE}/api/v1/game-nights`, () => {
    return HttpResponse.json({
      items: gameNights,
      totalCount: gameNights.length,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  }),

  // GET /api/v1/game-nights/:id
  http.get(`${API_BASE}/api/v1/game-nights/:id`, ({ params }) => {
    const night = gameNights.find(n => n.id === params.id);
    if (!night) {
      return HttpResponse.json({ error: 'Game night not found' }, { status: 404 });
    }
    return HttpResponse.json(night);
  }),

  // POST /api/v1/game-nights - Create
  http.post(`${API_BASE}/api/v1/game-nights`, async ({ request }) => {
    const body = (await request.json()) as {
      title: string;
      date: string;
      location?: string;
    };
    const newNight: GameNight = {
      id: `gn-${Date.now()}`,
      title: body.title,
      date: body.date,
      location: body.location,
      status: 'Planned',
      organizerId: 'user-1',
      participants: [],
      playlist: [],
      createdAt: new Date().toISOString(),
    };
    gameNights.push(newNight);
    return HttpResponse.json(newNight, { status: 201 });
  }),

  // PUT /api/v1/game-nights/:id - Update
  http.put(`${API_BASE}/api/v1/game-nights/:id`, async ({ params, request }) => {
    const body = (await request.json()) as Partial<GameNight>;
    const index = gameNights.findIndex(n => n.id === params.id);
    if (index === -1) {
      return HttpResponse.json({ error: 'Game night not found' }, { status: 404 });
    }
    gameNights[index] = { ...gameNights[index], ...body };
    return HttpResponse.json(gameNights[index]);
  }),

  // DELETE /api/v1/game-nights/:id
  http.delete(`${API_BASE}/api/v1/game-nights/:id`, ({ params }) => {
    const index = gameNights.findIndex(n => n.id === params.id);
    if (index === -1) {
      return HttpResponse.json({ error: 'Game night not found' }, { status: 404 });
    }
    gameNights.splice(index, 1);
    return HttpResponse.json({ success: true });
  }),

  // POST /api/v1/game-nights/:id/invitations - Invite participant
  http.post(`${API_BASE}/api/v1/game-nights/:id/invitations`, async ({ params, request }) => {
    const body = (await request.json()) as { userId: string; displayName: string };
    const night = gameNights.find(n => n.id === params.id);
    if (!night) {
      return HttpResponse.json({ error: 'Game night not found' }, { status: 404 });
    }
    const invitation = {
      id: `inv-${Date.now()}`,
      userId: body.userId,
      displayName: body.displayName,
      rsvpStatus: 'Pending' as const,
    };
    night.participants.push(invitation);
    return HttpResponse.json(invitation, { status: 201 });
  }),

  // PUT /api/v1/game-nights/:nightId/invitations/:invId/rsvp - RSVP
  http.put(
    `${API_BASE}/api/v1/game-nights/:nightId/invitations/:invId/rsvp`,
    async ({ params, request }) => {
      const body = (await request.json()) as { status: 'Accepted' | 'Declined' };
      const night = gameNights.find(n => n.id === params.nightId);
      if (!night) {
        return HttpResponse.json({ error: 'Game night not found' }, { status: 404 });
      }
      const participant = night.participants.find(p => p.id === params.invId);
      if (!participant) {
        return HttpResponse.json({ error: 'Invitation not found' }, { status: 404 });
      }
      participant.rsvpStatus = body.status;
      return HttpResponse.json(participant);
    }
  ),

  // POST /api/v1/game-nights/:id/playlist - Add game to playlist
  http.post(`${API_BASE}/api/v1/game-nights/:id/playlist`, async ({ params, request }) => {
    const body = (await request.json()) as { gameId: string; gameName: string };
    const night = gameNights.find(n => n.id === params.id);
    if (!night) {
      return HttpResponse.json({ error: 'Game night not found' }, { status: 404 });
    }
    const playlistItem = {
      id: `pl-${Date.now()}`,
      gameId: body.gameId,
      gameName: body.gameName,
      order: night.playlist.length,
    };
    night.playlist.push(playlistItem);
    return HttpResponse.json(playlistItem, { status: 201 });
  }),

  // DELETE /api/v1/game-nights/:nightId/playlist/:itemId - Remove from playlist
  http.delete(`${API_BASE}/api/v1/game-nights/:nightId/playlist/:itemId`, ({ params }) => {
    const night = gameNights.find(n => n.id === params.nightId);
    if (!night) {
      return HttpResponse.json({ error: 'Game night not found' }, { status: 404 });
    }
    night.playlist = night.playlist.filter(p => p.id !== params.itemId);
    return HttpResponse.json({ success: true });
  }),
];

export const resetGameNightsState = () => {
  gameNights = [];
};

export const getGameNightsState = () => gameNights;
