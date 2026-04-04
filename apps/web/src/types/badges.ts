/**
 * Badge & Gamification Types
 * Type definitions for badge system and leaderboards (Issue #2747)
 */

/**
 * Badge tier levels (highest to lowest)
 */
export enum BadgeTier {
  Diamond = 'Diamond',
  Platinum = 'Platinum',
  Gold = 'Gold',
  Silver = 'Silver',
  Bronze = 'Bronze',
}

/**
 * Leaderboard time period filter
 */
export type LeaderboardPeriod = 'ThisWeek' | 'ThisMonth' | 'AllTime';

/**
 * User badge DTO (aligned with backend)
 */
export interface UserBadgeDto {
  id: string;
  name: string;
  description: string;
  tier: BadgeTier;
  iconUrl: string;
  earnedAt: string;
  isDisplayed: boolean;
  category?: string;
}

/**
 * Badge notification data for celebratory modal
 */
export interface BadgeNotificationData {
  id: string;
  name: string;
  description: string;
  tier: BadgeTier;
  iconUrl: string;
  earnedAt: string;
}

/**
 * Leaderboard entry DTO
 */
export interface LeaderboardEntryDto {
  userId: string;
  userName: string;
  avatarUrl?: string;
  contributionCount: number;
  topBadges: UserBadgeDto[];
  rank: number;
}

/**
 * Helper: Get tier icon emoji
 */
export function getTierIcon(tier: BadgeTier): string {
  const icons: Record<BadgeTier, string> = {
    [BadgeTier.Diamond]: '💎',
    [BadgeTier.Platinum]: '⭐',
    [BadgeTier.Gold]: '🥇',
    [BadgeTier.Silver]: '🥈',
    [BadgeTier.Bronze]: '🥉',
  };
  return icons[tier];
}

/**
 * Helper: Get celebratory title for modal
 */
export function getCelebratoryTitle(tier: BadgeTier): string {
  const titles: Record<BadgeTier, string> = {
    [BadgeTier.Bronze]: 'Badge Earned! 🥉',
    [BadgeTier.Silver]: 'Badge Earned! 🥈',
    [BadgeTier.Gold]: 'Badge Earned! 🥇',
    [BadgeTier.Platinum]: 'Amazing Achievement! ⭐',
    [BadgeTier.Diamond]: 'Legendary Badge! 💎',
  };
  return titles[tier];
}

/**
 * Helper: Get tier order for sorting (Diamond=0, Bronze=4)
 */
export function getTierOrder(tier: BadgeTier): number {
  const order: Record<BadgeTier, number> = {
    [BadgeTier.Diamond]: 0,
    [BadgeTier.Platinum]: 1,
    [BadgeTier.Gold]: 2,
    [BadgeTier.Silver]: 3,
    [BadgeTier.Bronze]: 4,
  };
  return order[tier];
}

/**
 * Helper: Get tier CSS class for styling
 */
export function getTierClass(tier: BadgeTier): string {
  const classes: Record<BadgeTier, string> = {
    [BadgeTier.Diamond]: 'bg-gradient-to-r from-cyan-500 to-purple-500',
    [BadgeTier.Platinum]: 'bg-gradient-to-r from-slate-300 to-slate-400',
    [BadgeTier.Gold]: 'bg-gradient-to-r from-yellow-400 to-yellow-600',
    [BadgeTier.Silver]: 'bg-gradient-to-r from-gray-300 to-gray-400',
    [BadgeTier.Bronze]: 'bg-gradient-to-r from-amber-600 to-amber-800',
  };
  return classes[tier];
}
