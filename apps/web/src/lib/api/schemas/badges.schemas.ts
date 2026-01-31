import { z } from 'zod';

/**
 * Badge & Gamification API Schemas
 * Issue #2747: Frontend - Badge Display Components
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 5 - Gamification
 */

// Re-export BadgeTier from game-contributors (DRY principle)
export { BadgeTierSchema, type BadgeTier } from './game-contributors.schemas';

// Leaderboard Period
export const LeaderboardPeriodSchema = z.enum(['ThisWeek', 'ThisMonth', 'AllTime']);

export type LeaderboardPeriod = z.infer<typeof LeaderboardPeriodSchema>;

// User Badge DTO (full badge details)
export const UserBadgeDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  description: z.string(),
  tier: z.enum(['Bronze', 'Silver', 'Gold', 'Platinum', 'Diamond']),
  iconUrl: z.string(),
  earnedAt: z.string().datetime(),
  isDisplayed: z.boolean(),
  category: z.string().optional(),
});

export type UserBadgeDto = z.infer<typeof UserBadgeDtoSchema>;

// Badge Notification Data (for celebratory modal)
export const BadgeNotificationDataSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  description: z.string(),
  tier: z.enum(['Bronze', 'Silver', 'Gold', 'Platinum', 'Diamond']),
  iconUrl: z.string(),
  earnedAt: z.string().datetime(),
});

export type BadgeNotificationData = z.infer<typeof BadgeNotificationDataSchema>;

// Leaderboard Entry DTO
export const LeaderboardEntryDtoSchema = z.object({
  userId: z.string().uuid(),
  userName: z.string(),
  avatarUrl: z.string().nullable().optional(),
  contributionCount: z.number().int().nonnegative(),
  topBadges: z.array(UserBadgeDtoSchema).max(3), // Max 3 badges displayed
  rank: z.number().int().positive(),
});

export type LeaderboardEntryDto = z.infer<typeof LeaderboardEntryDtoSchema>;

// API Response Schemas

// My Badges Response
export const MyBadgesResponseSchema = z.object({
  badges: z.array(UserBadgeDtoSchema),
});

export type MyBadgesResponse = z.infer<typeof MyBadgesResponseSchema>;

// Leaderboard Response
export const LeaderboardResponseSchema = z.object({
  period: LeaderboardPeriodSchema,
  items: z.array(LeaderboardEntryDtoSchema),
});

export type LeaderboardResponse = z.infer<typeof LeaderboardResponseSchema>;

// Toggle Badge Display Request
export const ToggleBadgeDisplayRequestSchema = z.object({
  isDisplayed: z.boolean(),
});

export type ToggleBadgeDisplayRequest = z.infer<typeof ToggleBadgeDisplayRequestSchema>;
