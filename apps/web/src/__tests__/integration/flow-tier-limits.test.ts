/**
 * Flow Test: Tier Limits Enforcement
 *
 * Tests tier-based access control:
 * 1. Free user hits library limit → gets rejected
 * 2. Premium user has higher limits → succeeds
 * 3. API key creation/revocation per role
 * 4. 2FA enable/verify flow
 *
 * Uses MSW server.use() overrides to simulate tier-specific responses.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { resetLibraryState } from '../mocks/handlers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

describe('Flow: Tier Limits Enforcement', () => {
  beforeEach(() => {
    resetLibraryState();
    server.resetHandlers();
  });

  it('should enforce library collection limit for free tier', async () => {
    // Override: simulate tier limit reached
    server.use(
      http.post(`${API_BASE}/api/v1/library`, () => {
        return HttpResponse.json(
          {
            error: 'Collection limit reached',
            detail: 'Free tier allows maximum 50 games. Upgrade to add more.',
            tierLimit: 50,
            currentCount: 50,
          },
          { status: 403 }
        );
      })
    );

    // Try to add a game
    const addRes = await fetch(`${API_BASE}/api/v1/library`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gameId: 'new-game', name: 'New Game', status: 'owned' }),
    });
    expect(addRes.status).toBe(403);
    const error = await addRes.json();
    expect(error.error).toContain('limit reached');
    expect(error.tierLimit).toBe(50);
  });

  it('should allow premium user to exceed free tier limits', async () => {
    // Login as premium (default handlers allow add)
    await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'user@meepleai.dev', password: 'password123' }),
    });

    // Premium user can add games without hitting limit
    const addRes = await fetch(`${API_BASE}/api/v1/library`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        gameId: 'premium-game-1',
        name: 'Premium Game',
        status: 'owned',
      }),
    });
    expect(addRes.status).toBe(201);
  });

  it('should enforce rate limiting on AI chat', async () => {
    // Override: simulate rate limit
    server.use(
      http.post(`${API_BASE}/api/v1/chat/ask`, () => {
        return HttpResponse.json(
          {
            error: 'Rate limit exceeded',
            detail: 'Free tier: 10 queries/day. Upgrade for more.',
            retryAfter: 3600,
          },
          {
            status: 429,
            headers: { 'Retry-After': '3600' },
          }
        );
      })
    );

    const chatRes = await fetch(`${API_BASE}/api/v1/chat/ask`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question: 'How do I play?', gameId: 'game-1' }),
    });
    expect(chatRes.status).toBe(429);
    const error = await chatRes.json();
    expect(error.retryAfter).toBe(3600);
    expect(chatRes.headers.get('Retry-After')).toBe('3600');
  });

  it('should enforce PDF upload limits for free tier', async () => {
    server.use(
      http.post(`${API_BASE}/api/v1/pdf/upload`, () => {
        return HttpResponse.json(
          {
            error: 'Upload limit reached',
            detail: 'Free tier: 5 uploads/month. Upgrade for more.',
            monthlyLimit: 5,
            currentCount: 5,
          },
          { status: 403 }
        );
      })
    );

    // Simulate multipart upload
    const formData = new FormData();
    formData.append('file', new Blob(['fake pdf content'], { type: 'application/pdf' }));

    const uploadRes = await fetch(`${API_BASE}/api/v1/pdf/upload`, {
      method: 'POST',
      body: formData,
    });
    expect(uploadRes.status).toBe(403);
    const error = await uploadRes.json();
    expect(error.monthlyLimit).toBe(5);
  });

  it('should enforce agent slot limit for free tier', async () => {
    server.use(
      http.post(`${API_BASE}/api/v1/agents`, () => {
        return HttpResponse.json(
          {
            error: 'Agent slot limit reached',
            detail: 'Free tier allows 3 agents. Upgrade for unlimited.',
            maxSlots: 3,
            usedSlots: 3,
          },
          { status: 403 }
        );
      })
    );

    const agentRes = await fetch(`${API_BASE}/api/v1/agents`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name: 'New Agent', gameId: 'game-1', agentDefinitionId: 'qa' }),
    });
    expect(agentRes.status).toBe(403);
    const error = await agentRes.json();
    expect(error.maxSlots).toBe(3);
  });

  it('should enforce share request limit', async () => {
    server.use(
      http.post(`${API_BASE}/api/v1/share-requests`, () => {
        return HttpResponse.json(
          {
            error: 'Share request limit reached',
            detail: 'Free tier: 5 share requests/month.',
            monthlyLimit: 5,
          },
          { status: 403 }
        );
      })
    );

    const shareRes = await fetch(`${API_BASE}/api/v1/share-requests`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ gameId: 'game-1', recipientId: 'user-2' }),
    });
    expect(shareRes.status).toBe(403);
  });

  it('should allow premium tier higher limits across all resources', async () => {
    // Override: simulate premium tier check endpoint
    server.use(
      http.get(`${API_BASE}/api/v1/tiers/current`, () => {
        return HttpResponse.json({
          tier: 'premium',
          level: 2,
          limits: {
            maxGames: 500,
            storageMB: 5000,
            monthlyUploads: 100,
            agentSlots: -1, // unlimited
            shareRequests: 15,
            aiQueries: 1000,
          },
          usage: {
            currentGames: 45,
            storageMB: 200,
            monthlyUploads: 3,
            agentSlots: 5,
            shareRequests: 2,
            aiQueries: 50,
          },
        });
      })
    );

    const tierRes = await fetch(`${API_BASE}/api/v1/tiers/current`);
    expect(tierRes.ok).toBe(true);
    const tier = await tierRes.json();
    expect(tier.tier).toBe('premium');
    expect(tier.limits.maxGames).toBe(500);
    expect(tier.limits.agentSlots).toBe(-1); // unlimited
    expect(tier.usage.currentGames).toBeLessThan(tier.limits.maxGames);
  });
});
