/**
 * Flow Test: Badges & Notifications
 *
 * Tests gamification and notification flows:
 * 1. View available badges
 * 2. Claim earned badge
 * 3. Toggle badge display on profile
 * 4. View leaderboard
 * 5. Check notifications
 * 6. Mark notification as read
 * 7. Mark all as read
 * 8. Manage players (CRUD + soft-delete)
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { server } from '../mocks/server';
import { resetBadgesState, resetNotificationsState, resetPlayersState } from '../mocks/handlers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

describe('Flow: Badges & Notifications', () => {
  beforeEach(() => {
    resetBadgesState();
    resetNotificationsState();
    resetPlayersState();
    server.resetHandlers();
  });

  describe('Badge System', () => {
    it('should view badges and claim earned ones', async () => {
      // List all badges
      const badgesRes = await fetch(`${API_BASE}/api/v1/badges`);
      expect(badgesRes.ok).toBe(true);
      const badges = await badgesRes.json();
      expect(badges.items.length).toBeGreaterThan(0);
      expect(badges.earnedCount).toBeGreaterThan(0);

      // Find unclaimed earned badge
      const unclaimed = badges.items.find((b: any) => b.isEarned && !b.isClaimed);
      expect(unclaimed).toBeDefined();
      expect(unclaimed.name).toBe('First Game');

      // Claim it
      const claimRes = await fetch(`${API_BASE}/api/v1/badges/${unclaimed.id}/claim`, {
        method: 'POST',
      });
      expect(claimRes.ok).toBe(true);
      const claimed = await claimRes.json();
      expect(claimed.isClaimed).toBe(true);
    });

    it('should not claim unearned badge', async () => {
      // badge-2 is not earned
      const claimRes = await fetch(`${API_BASE}/api/v1/badges/badge-2/claim`, {
        method: 'POST',
      });
      expect(claimRes.status).toBe(400);
      const error = await claimRes.json();
      expect(error.error).toContain('not yet earned');
    });

    it('should toggle badge display on profile', async () => {
      // badge-3 is earned, claimed, and displayed
      const toggleRes = await fetch(`${API_BASE}/api/v1/badges/badge-3/display`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isDisplayed: false }),
      });
      expect(toggleRes.ok).toBe(true);
      const toggled = await toggleRes.json();
      expect(toggled.isDisplayed).toBe(false);

      // Toggle back on
      const toggleBackRes = await fetch(`${API_BASE}/api/v1/badges/badge-3/display`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isDisplayed: true }),
      });
      const toggledBack = await toggleBackRes.json();
      expect(toggledBack.isDisplayed).toBe(true);
    });

    it('should view leaderboard', async () => {
      const leaderboardRes = await fetch(`${API_BASE}/api/v1/badges/leaderboard`);
      expect(leaderboardRes.ok).toBe(true);
      const leaderboard = await leaderboardRes.json();
      expect(leaderboard.items.length).toBeGreaterThan(0);
      expect(leaderboard.items[0].rank).toBe(1);
      expect(leaderboard.items[0].totalBadges).toBeGreaterThan(0);
    });
  });

  describe('Notification System', () => {
    it('should list notifications with unread count', async () => {
      const notifRes = await fetch(`${API_BASE}/api/v1/notifications`);
      expect(notifRes.ok).toBe(true);
      const notifs = await notifRes.json();
      expect(notifs.items.length).toBeGreaterThan(0);
      expect(notifs.unreadCount).toBeGreaterThan(0);
    });

    it('should mark single notification as read', async () => {
      // List to get an unread notification
      const listRes = await fetch(`${API_BASE}/api/v1/notifications`);
      const list = await listRes.json();
      const unread = list.items.find((n: any) => !n.isRead);
      expect(unread).toBeDefined();

      // Mark as read
      const readRes = await fetch(`${API_BASE}/api/v1/notifications/${unread.id}/read`, {
        method: 'PUT',
      });
      expect(readRes.ok).toBe(true);
      const read = await readRes.json();
      expect(read.isRead).toBe(true);

      // Verify unread count decreased
      const afterRes = await fetch(`${API_BASE}/api/v1/notifications`);
      const after = await afterRes.json();
      expect(after.unreadCount).toBe(list.unreadCount - 1);
    });

    it('should mark all notifications as read', async () => {
      // Mark all
      const markAllRes = await fetch(`${API_BASE}/api/v1/notifications/read-all`, {
        method: 'PUT',
      });
      expect(markAllRes.ok).toBe(true);

      // Verify all read
      const afterRes = await fetch(`${API_BASE}/api/v1/notifications`);
      const after = await afterRes.json();
      expect(after.unreadCount).toBe(0);
      after.items.forEach((n: any) => {
        expect(n.isRead).toBe(true);
      });
    });
  });

  describe('Player Management', () => {
    it('should complete player lifecycle: create → update → soft-delete', async () => {
      // List existing players
      const listRes = await fetch(`${API_BASE}/api/v1/players`);
      expect(listRes.ok).toBe(true);
      const list = await listRes.json();
      const initialCount = list.items.length;
      expect(initialCount).toBeGreaterThan(0);

      // Create new player
      const createRes = await fetch(`${API_BASE}/api/v1/players`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: 'Charlie' }),
      });
      expect(createRes.status).toBe(201);
      const newPlayer = await createRes.json();
      expect(newPlayer.displayName).toBe('Charlie');
      expect(newPlayer.isActive).toBe(true);
      expect(newPlayer.gamesPlayed).toBe(0);

      // Get player detail
      const detailRes = await fetch(`${API_BASE}/api/v1/players/${newPlayer.id}`);
      expect(detailRes.ok).toBe(true);
      const detail = await detailRes.json();
      expect(detail.displayName).toBe('Charlie');

      // Update player
      const updateRes = await fetch(`${API_BASE}/api/v1/players/${newPlayer.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: 'Charlie Brown' }),
      });
      expect(updateRes.ok).toBe(true);
      const updated = await updateRes.json();
      expect(updated.displayName).toBe('Charlie Brown');

      // Soft-delete player
      const deleteRes = await fetch(`${API_BASE}/api/v1/players/${newPlayer.id}`, {
        method: 'DELETE',
      });
      expect(deleteRes.ok).toBe(true);

      // Verify soft-deleted: still exists but not in active list
      const afterList = await fetch(`${API_BASE}/api/v1/players`);
      const afterData = await afterList.json();
      expect(afterData.items.length).toBe(initialCount); // Back to original (active only)

      // But direct access still works (soft-deleted, not gone)
      const softDeletedRes = await fetch(`${API_BASE}/api/v1/players/${newPlayer.id}`);
      const softDeleted = await softDeletedRes.json();
      expect(softDeleted.isActive).toBe(false);
    });
  });
});
