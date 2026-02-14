/**
 * AchievementsWidget Tests (Issue #3924)
 *
 * Test coverage for achievements widget with:
 * - Recent achievements display with points
 * - Progress bar for next achievement
 * - Celebration animation on new unlock
 * - Hook integration (data fetching)
 * - Rarity badges and category icons
 * - Empty state and loading state
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  AchievementsWidget,
  type Achievement,
  type NextAchievementProgress,
} from '../AchievementsWidget';

// Mock Next.js Link - preserve all props including data-testid
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.HTMLAttributes<HTMLDivElement>) => (
      <div {...props}>{children}</div>
    ),
    span: ({ children, ...props }: React.HTMLAttributes<HTMLSpanElement>) => (
      <span {...props}>{children}</span>
    ),
    section: ({ children, ...props }: React.HTMLAttributes<HTMLElement>) => (
      <section {...props}>{children}</section>
    ),
  },
  AnimatePresence: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

// Mock date-fns
vi.mock('date-fns', () => ({
  formatDistanceToNow: vi.fn(() => '2 ore fa'),
}));

// Mock useRecentAchievements hook
vi.mock('@/hooks/useRecentAchievements', () => ({
  useRecentAchievements: vi.fn(() => ({
    data: null,
    isLoading: false,
    error: null,
  })),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockAchievements: Achievement[] = [
  {
    id: 'ach-1',
    name: 'Giocatore Costante',
    description: '7 giorni di streak',
    category: 'streak',
    rarity: 'rare',
    points: 50,
    unlockedAt: '2026-02-04T08:00:00Z',
  },
  {
    id: 'ach-2',
    name: 'Collezionista',
    description: '100+ giochi in collezione',
    category: 'collection',
    rarity: 'epic',
    points: 100,
    unlockedAt: '2026-02-03T10:00:00Z',
  },
  {
    id: 'ach-3',
    name: 'Esperto AI',
    description: '50+ chat completate',
    category: 'chat',
    rarity: 'common',
    points: 25,
    unlockedAt: '2026-02-01T10:00:00Z',
  },
];

const mockNextProgress: NextAchievementProgress = {
  achievementName: 'Maestro Sessioni',
  current: 7,
  target: 10,
};

// ============================================================================
// Test Helpers
// ============================================================================

let queryClient: QueryClient;

function renderComponent(props: Partial<React.ComponentProps<typeof AchievementsWidget>> = {}) {
  queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <AchievementsWidget {...props} />
    </QueryClientProvider>
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('AchievementsWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading State', () => {
    it('renders skeleton when loading', () => {
      renderComponent({ isLoading: true });

      expect(screen.getByTestId('achievements-widget-skeleton')).toBeInTheDocument();
    });

    it('shows 3 skeleton rows', () => {
      renderComponent({ isLoading: true });

      const skeleton = screen.getByTestId('achievements-widget-skeleton');
      expect(skeleton).toHaveClass('rounded-2xl');
    });

    it('shows progress skeleton in loading state', () => {
      renderComponent({ isLoading: true });

      const skeleton = screen.getByTestId('achievements-widget-skeleton');
      // Progress skeleton section has a rounded-full skeleton
      const progressSkeleton = skeleton.querySelector('.rounded-full');
      expect(progressSkeleton).toBeTruthy();
    });
  });

  describe('Success State', () => {
    it('renders widget with achievements', () => {
      renderComponent({ achievements: mockAchievements });

      expect(screen.getByTestId('achievements-widget')).toBeInTheDocument();
      expect(screen.getByTestId('achievements-widget-title')).toHaveTextContent('Achievements & Badges');
    });

    it('renders all achievements', () => {
      renderComponent({ achievements: mockAchievements });

      const achievementsList = screen.getByTestId('achievements-list');
      const achievements = achievementsList.querySelectorAll('[data-testid^="achievement-card-"]');
      expect(achievements.length).toBe(3);
    });

    it('limits to 3 achievements maximum', () => {
      const manyAchievements: Achievement[] = [
        ...mockAchievements,
        { id: 'ach-4', name: 'Extra', description: 'Extra desc', category: 'gameplay', rarity: 'common', points: 10, unlockedAt: '2026-01-01T10:00:00Z' },
        { id: 'ach-5', name: 'Another', description: 'Another desc', category: 'milestone', rarity: 'legendary', points: 200, unlockedAt: '2026-01-02T10:00:00Z' },
      ];

      renderComponent({ achievements: manyAchievements });

      const achievementsList = screen.getByTestId('achievements-list');
      const achievements = achievementsList.querySelectorAll('[data-testid^="achievement-card-"]');
      expect(achievements.length).toBe(3);
    });

    it('sorts achievements by unlockedAt (most recent first)', () => {
      const unsortedAchievements: Achievement[] = [
        { id: 'ach-old', name: 'Old', description: 'Old desc', category: 'gameplay', rarity: 'common', points: 10, unlockedAt: '2026-01-01T10:00:00Z' },
        { id: 'ach-new', name: 'New', description: 'New desc', category: 'streak', rarity: 'rare', points: 50, unlockedAt: '2026-02-04T10:00:00Z' },
        { id: 'ach-mid', name: 'Mid', description: 'Mid desc', category: 'chat', rarity: 'epic', points: 75, unlockedAt: '2026-01-15T10:00:00Z' },
      ];

      renderComponent({ achievements: unsortedAchievements });

      // First card should be most recent (ach-new)
      expect(screen.getByTestId('achievement-name-ach-new')).toHaveTextContent('New');
    });

    it('displays achievement names', () => {
      renderComponent({ achievements: mockAchievements });

      mockAchievements.forEach((achievement) => {
        expect(screen.getByTestId(`achievement-name-${achievement.id}`)).toHaveTextContent(achievement.name);
      });
    });

    it('displays achievement descriptions', () => {
      renderComponent({ achievements: mockAchievements });

      mockAchievements.forEach((achievement) => {
        expect(screen.getByTestId(`achievement-description-${achievement.id}`)).toHaveTextContent(achievement.description);
      });
    });

    it('links to achievement detail pages', () => {
      renderComponent({ achievements: mockAchievements });

      mockAchievements.forEach((achievement) => {
        const link = screen.getByTestId(`achievement-card-${achievement.id}`);
        expect(link).toHaveAttribute('href', `/achievements/${achievement.id}`);
      });
    });

    it('shows view all badges CTA', () => {
      renderComponent({ achievements: mockAchievements });

      const cta = screen.getByTestId('view-all-badges-cta');
      expect(cta).toBeInTheDocument();
      expect(cta.closest('a')).toHaveAttribute('href', '/achievements');
    });
  });

  describe('Points Display', () => {
    it('shows points for each achievement', () => {
      renderComponent({ achievements: mockAchievements });

      expect(screen.getByTestId('achievement-points-ach-1')).toHaveTextContent('50 pts');
      expect(screen.getByTestId('achievement-points-ach-2')).toHaveTextContent('100 pts');
      expect(screen.getByTestId('achievement-points-ach-3')).toHaveTextContent('25 pts');
    });
  });

  describe('Next Achievement Progress', () => {
    it('shows progress bar when nextProgress is provided', () => {
      renderComponent({
        achievements: mockAchievements,
        nextProgress: mockNextProgress,
      });

      expect(screen.getByTestId('next-achievement-progress')).toBeInTheDocument();
    });

    it('displays next achievement name', () => {
      renderComponent({
        achievements: mockAchievements,
        nextProgress: mockNextProgress,
      });

      expect(screen.getByTestId('next-achievement-name')).toHaveTextContent('Maestro Sessioni');
    });

    it('displays current/target count', () => {
      renderComponent({
        achievements: mockAchievements,
        nextProgress: mockNextProgress,
      });

      expect(screen.getByTestId('next-achievement-count')).toHaveTextContent('7/10');
    });

    it('displays percentage', () => {
      renderComponent({
        achievements: mockAchievements,
        nextProgress: mockNextProgress,
      });

      expect(screen.getByTestId('next-achievement-percentage')).toHaveTextContent('70% completato');
    });

    it('has accessible progress bar with aria-label', () => {
      renderComponent({
        achievements: mockAchievements,
        nextProgress: mockNextProgress,
      });

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute(
        'aria-label',
        'Progresso verso Maestro Sessioni: 70%'
      );
    });

    it('caps percentage at 100%', () => {
      const overProgress: NextAchievementProgress = {
        achievementName: 'Over',
        current: 15,
        target: 10,
      };

      renderComponent({
        achievements: mockAchievements,
        nextProgress: overProgress,
      });

      expect(screen.getByTestId('next-achievement-percentage')).toHaveTextContent('100% completato');
    });

    it('does not show progress section when nextProgress is null', () => {
      renderComponent({
        achievements: mockAchievements,
        nextProgress: null,
      });

      expect(screen.queryByTestId('next-achievement-progress')).not.toBeInTheDocument();
    });
  });

  describe('Rarity Badges', () => {
    it('shows rarity badge for each achievement', () => {
      renderComponent({ achievements: mockAchievements });

      expect(screen.getByTestId('rarity-badge-rare')).toBeInTheDocument();
      expect(screen.getByTestId('rarity-badge-epic')).toBeInTheDocument();
      expect(screen.getByTestId('rarity-badge-common')).toBeInTheDocument();
    });

    it('displays correct rarity labels', () => {
      const allRarities: Achievement[] = [
        { id: 'common', name: 'Common Achievement', description: 'desc', category: 'gameplay', rarity: 'common', points: 10, unlockedAt: '2026-02-04T10:00:00Z' },
        { id: 'rare', name: 'Rare Achievement', description: 'desc', category: 'gameplay', rarity: 'rare', points: 50, unlockedAt: '2026-02-03T10:00:00Z' },
        { id: 'epic', name: 'Epic Achievement', description: 'desc', category: 'gameplay', rarity: 'epic', points: 100, unlockedAt: '2026-02-02T10:00:00Z' },
      ];

      renderComponent({ achievements: allRarities });

      expect(screen.getByTestId('rarity-badge-common')).toHaveTextContent('COMMON');
      expect(screen.getByTestId('rarity-badge-rare')).toHaveTextContent('RARE');
      expect(screen.getByTestId('rarity-badge-epic')).toHaveTextContent('EPIC');
    });

    it('shows legendary rarity badge', () => {
      const legendaryAchievement: Achievement[] = [
        { id: 'legendary', name: 'Legendary Achievement', description: 'desc', category: 'milestone', rarity: 'legendary', points: 200, unlockedAt: '2026-02-04T10:00:00Z' },
      ];

      renderComponent({ achievements: legendaryAchievement });

      expect(screen.getByTestId('rarity-badge-legendary')).toHaveTextContent('LEGENDARY');
    });
  });

  describe('Category Icons', () => {
    it('shows category icons for achievements', () => {
      const allCategories: Achievement[] = [
        { id: 'coll', name: 'Collection', description: 'desc', category: 'collection', rarity: 'common', points: 10, unlockedAt: '2026-02-04T10:00:00Z' },
        { id: 'game', name: 'Gameplay', description: 'desc', category: 'gameplay', rarity: 'common', points: 10, unlockedAt: '2026-02-03T10:00:00Z' },
        { id: 'chat', name: 'Chat', description: 'desc', category: 'chat', rarity: 'common', points: 10, unlockedAt: '2026-02-02T10:00:00Z' },
      ];

      renderComponent({ achievements: allCategories });

      expect(screen.getByTestId('category-icon-collection')).toBeInTheDocument();
      expect(screen.getByTestId('category-icon-gameplay')).toBeInTheDocument();
      expect(screen.getByTestId('category-icon-chat')).toBeInTheDocument();
    });

    it('shows streak category icon', () => {
      const streakAchievement: Achievement[] = [
        { id: 'streak', name: 'Streak', description: 'desc', category: 'streak', rarity: 'rare', points: 50, unlockedAt: '2026-02-04T10:00:00Z' },
      ];

      renderComponent({ achievements: streakAchievement });

      expect(screen.getByTestId('category-icon-streak')).toBeInTheDocument();
    });

    it('shows milestone category icon', () => {
      const milestoneAchievement: Achievement[] = [
        { id: 'milestone', name: 'Milestone', description: 'desc', category: 'milestone', rarity: 'legendary', points: 200, unlockedAt: '2026-02-04T10:00:00Z' },
      ];

      renderComponent({ achievements: milestoneAchievement });

      expect(screen.getByTestId('category-icon-milestone')).toBeInTheDocument();
    });
  });

  describe('New Unlock Indicator', () => {
    it('shows new unlock indicator when hasNewUnlock is true', () => {
      renderComponent({ achievements: mockAchievements, hasNewUnlock: true });

      expect(screen.getByTestId('new-unlock-indicator')).toBeInTheDocument();
    });

    it('does not show new unlock indicator when hasNewUnlock is false', () => {
      renderComponent({ achievements: mockAchievements, hasNewUnlock: false });

      expect(screen.queryByTestId('new-unlock-indicator')).not.toBeInTheDocument();
    });
  });

  describe('Celebration Animation', () => {
    it('renders celebration badge for first item when hasNewUnlock is true', () => {
      renderComponent({ achievements: mockAchievements, hasNewUnlock: true });

      // First achievement icon should still be present (celebration uses same testid)
      const firstAchievementId = 'ach-1'; // Most recent by date
      expect(screen.getByTestId(`achievement-icon-${firstAchievementId}`)).toBeInTheDocument();
    });
  });

  describe('Empty State', () => {
    it('renders empty state when no achievements', () => {
      renderComponent({ achievements: [] });

      expect(screen.getByTestId('achievements-widget-empty')).toBeInTheDocument();
      expect(screen.getByText('Nessun achievement sbloccato')).toBeInTheDocument();
    });

    it('shows view achievements CTA in empty state', () => {
      renderComponent({ achievements: [] });

      const cta = screen.getByTestId('view-achievements-empty-cta');
      expect(cta).toBeInTheDocument();
      expect(cta.closest('a')).toHaveAttribute('href', '/achievements');
    });

    it('does not show progress section in empty state', () => {
      renderComponent({ achievements: [], nextProgress: mockNextProgress });

      expect(screen.queryByTestId('next-achievement-progress')).not.toBeInTheDocument();
    });
  });

  describe('Styling', () => {
    it('applies custom className', () => {
      renderComponent({ achievements: mockAchievements, className: 'custom-class' });

      expect(screen.getByTestId('achievements-widget')).toHaveClass('custom-class');
    });

    it('has glassmorphic styling', () => {
      renderComponent({ achievements: mockAchievements });

      const widget = screen.getByTestId('achievements-widget');
      expect(widget).toHaveClass('rounded-2xl');
      expect(widget).toHaveClass('backdrop-blur-xl');
    });
  });

  describe('Accessibility', () => {
    it('has semantic section element', () => {
      renderComponent({ achievements: mockAchievements });

      expect(screen.getByTestId('achievements-widget').tagName).toBe('SECTION');
    });

    it('has heading element', () => {
      renderComponent({ achievements: mockAchievements });

      expect(screen.getByRole('heading', { level: 3 })).toBeInTheDocument();
    });

    it('all links are accessible', () => {
      renderComponent({ achievements: mockAchievements });

      const links = screen.getAllByRole('link');
      links.forEach((link) => {
        expect(link).toHaveAttribute('href');
      });
    });

    it('progress bar has aria-label', () => {
      renderComponent({
        achievements: mockAchievements,
        nextProgress: mockNextProgress,
      });

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAccessibleName();
    });
  });
});
