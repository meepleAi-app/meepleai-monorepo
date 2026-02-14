/**
 * Achievements API Client - Issue #3924
 *
 * Client for `/api/v1/achievements/recent` endpoint
 *
 * @see Issue #3922 - Achievement System & Badge Engine (Backend)
 * @see Issue #3924 - Frontend: Achievements Widget Component
 */

import type {
  Achievement,
  AchievementCategory,
  AchievementRarity,
  NextAchievementProgress,
} from '@/components/dashboard/AchievementsWidget';

// ============================================================================
// Backend API Response Types
// ============================================================================

interface AchievementApiResponse {
  id: string;
  code: string;
  name: string;
  description: string;
  iconUrl: string | null;
  points: number;
  rarity: string;
  category: string;
  unlockedAt: string;
}

interface NextProgressApiResponse {
  achievementName: string;
  current: number;
  target: number;
}

interface RecentAchievementsApiResponse {
  achievements: AchievementApiResponse[];
  nextProgress: NextProgressApiResponse | null;
}

// ============================================================================
// Mock Data (Temporary - until backend is accessible)
// ============================================================================

const MOCK_RECENT_ACHIEVEMENTS: Achievement[] = [
  {
    id: 'ach-1',
    name: 'Giocatore Costante',
    description: '7 giorni di streak',
    category: 'streak',
    rarity: 'rare',
    points: 50,
    unlockedAt: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(),
  },
  {
    id: 'ach-2',
    name: 'Collezionista',
    description: '100+ giochi in collezione',
    category: 'collection',
    rarity: 'epic',
    points: 100,
    unlockedAt: new Date(Date.now() - 1000 * 60 * 60 * 24).toISOString(),
  },
  {
    id: 'ach-3',
    name: 'Esperto AI',
    description: '50+ chat completate',
    category: 'chat',
    rarity: 'common',
    points: 25,
    unlockedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 3).toISOString(),
  },
];

const MOCK_NEXT_PROGRESS: NextAchievementProgress = {
  achievementName: 'Maestro Sessioni',
  current: 7,
  target: 10,
};

// ============================================================================
// Configuration
// ============================================================================

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

/**
 * Mock data toggle for development
 * Set `NEXT_PUBLIC_USE_MOCK_ACHIEVEMENTS=false` in `.env.local` to use real API
 */
const USE_MOCK_DATA = process.env.NEXT_PUBLIC_USE_MOCK_ACHIEVEMENTS !== 'false';

// ============================================================================
// API Client
// ============================================================================

/**
 * Fetch recent achievements and next progress
 *
 * @returns Promise with recent achievements and next progress
 */
export async function fetchRecentAchievements(): Promise<{
  achievements: Achievement[];
  nextProgress: NextAchievementProgress | null;
}> {
  if (USE_MOCK_DATA) {
    await new Promise((resolve) => setTimeout(resolve, 200));
    return {
      achievements: MOCK_RECENT_ACHIEVEMENTS,
      nextProgress: MOCK_NEXT_PROGRESS,
    };
  }

  const response = await fetch(
    `${API_BASE_URL}/api/v1/achievements/recent`,
    {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
    }
  );

  if (!response.ok) {
    throw new Error(`Achievements API error: ${response.status} ${response.statusText}`);
  }

  const data: RecentAchievementsApiResponse = await response.json();
  return mapApiResponse(data);
}

// ============================================================================
// Response Mapper
// ============================================================================

function mapApiResponse(data: RecentAchievementsApiResponse): {
  achievements: Achievement[];
  nextProgress: NextAchievementProgress | null;
} {
  return {
    achievements: data.achievements.map((item) => ({
      id: item.id,
      name: item.name,
      description: item.description,
      category: normalizeCategory(item.category),
      rarity: normalizeRarity(item.rarity),
      points: item.points,
      unlockedAt: item.unlockedAt,
      icon: item.iconUrl ?? undefined,
    })),
    nextProgress: data.nextProgress
      ? {
          achievementName: data.nextProgress.achievementName,
          current: data.nextProgress.current,
          target: data.nextProgress.target,
        }
      : null,
  };
}

function normalizeCategory(category: string): AchievementCategory {
  const lower = category.toLowerCase();
  const valid: AchievementCategory[] = ['collection', 'gameplay', 'chat', 'streak', 'milestone'];
  return valid.includes(lower as AchievementCategory)
    ? (lower as AchievementCategory)
    : 'milestone';
}

function normalizeRarity(rarity: string): AchievementRarity {
  const lower = rarity.toLowerCase();
  const valid: AchievementRarity[] = ['common', 'rare', 'epic', 'legendary'];
  return valid.includes(lower as AchievementRarity)
    ? (lower as AchievementRarity)
    : 'common';
}
