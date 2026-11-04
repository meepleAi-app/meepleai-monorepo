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

// User Badge DTO — allineato al record UserBadgeDto (#3836)
export const UserBadgeDtoSchema = z.object({
  id: z.string().uuid(),
  code: z.string(),
  name: z.string(),
  description: z.string(),
  tier: z.enum(['Bronze', 'Silver', 'Gold', 'Platinum', 'Diamond']),
  // `IconUrl` è `string?` nel record C#: nullable, non solo assente.
  iconUrl: z.string().nullable(),
  earnedAt: z.string().datetime({ offset: true }),
  isDisplayed: z.boolean(),
  // Il backend non manda `category`: resta opzionale finché BadgeDetailSheet la mostra.
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
  earnedAt: z.string().datetime({ offset: true }),
});

export type BadgeNotificationData = z.infer<typeof BadgeNotificationDataSchema>;

// Leaderboard Entry DTO — allineato al record LeaderboardEntryDto (#3836)
export const LeaderboardEntryDtoSchema = z.object({
  userId: z.string().uuid(),
  userName: z.string(),
  avatarUrl: z.string().nullable().optional(),
  contributionCount: z.number().int().nonnegative(),
  badgeCount: z.number().int().nonnegative(),
  highestBadgeTier: z.enum(['Bronze', 'Silver', 'Gold', 'Platinum', 'Diamond']),
  topBadges: z.array(UserBadgeDtoSchema).max(3), // Max 3 badges displayed
  rank: z.number().int().positive(),
});

export type LeaderboardEntryDto = z.infer<typeof LeaderboardEntryDtoSchema>;

// API Response Schemas
//
// Entrambe le rotte restituiscono una lista NUDA: gli handler passano il risultato di
// `IQuery<List<...>>` a `Results.Ok(...)`, senza contenitore. Gli schemi precedenti
// avvolgevano la lista in `{ badges }` / `{ period, items }` — forme mai emesse dal
// backend, quindi la `.parse()` sollevava su ogni risposta non vuota (#3836).

// My Badges Response — GET /api/v1/users/me/badges
export const MyBadgesResponseSchema = z.array(UserBadgeDtoSchema);

export type MyBadgesResponse = z.infer<typeof MyBadgesResponseSchema>;

// Leaderboard Response — GET /api/v1/badges/leaderboard
export const LeaderboardResponseSchema = z.array(LeaderboardEntryDtoSchema);

export type LeaderboardResponse = z.infer<typeof LeaderboardResponseSchema>;

// Toggle Badge Display Request
export const ToggleBadgeDisplayRequestSchema = z.object({
  isDisplayed: z.boolean(),
});

export type ToggleBadgeDisplayRequest = z.infer<typeof ToggleBadgeDisplayRequestSchema>;
