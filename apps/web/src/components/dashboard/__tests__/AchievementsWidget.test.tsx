/**
 * AchievementsWidget Tests (Issue #3321)
 *
 * Test coverage for achievements widget with badges.
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import {
  AchievementsWidget,
  type Achievement,
  type AchievementRarity,
  type AchievementCategory,
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
  },
}));

// Mock date-fns
vi.mock('date-fns', () => ({
  formatDistanceToNow: vi.fn(() => '2 ore fa'),
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
    unlockedAt: '2026-02-04T08:00:00Z',
  },
  {
    id: 'ach-2',
    name: 'Collezionista',
    description: '100+ giochi in collezione',
    category: 'collection',
    rarity: 'epic',
    unlockedAt: '2026-02-03T10:00:00Z',
  },
  {
    id: 'ach-3',
    name: 'Esperto AI',
    description: '50+ chat completate',
    category: 'chat',
    rarity: 'common',
    unlockedAt: '2026-02-01T10:00:00Z',
  },
];

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
        { id: 'ach-4', name: 'Extra', description: 'Extra desc', category: 'gameplay', rarity: 'common', unlockedAt: '2026-01-01T10:00:00Z' },
        { id: 'ach-5', name: 'Another', description: 'Another desc', category: 'milestone', rarity: 'legendary', unlockedAt: '2026-01-02T10:00:00Z' },
      ];

      renderComponent({ achievements: manyAchievements });

      const achievementsList = screen.getByTestId('achievements-list');
      const achievements = achievementsList.querySelectorAll('[data-testid^="achievement-card-"]');
      expect(achievements.length).toBe(3);
    });

    it('sorts achievements by unlockedAt (most recent first)', () => {
      const unsortedAchievements: Achievement[] = [
        { id: 'ach-old', name: 'Old', description: 'Old desc', category: 'gameplay', rarity: 'common', unlockedAt: '2026-01-01T10:00:00Z' },
        { id: 'ach-new', name: 'New', description: 'New desc', category: 'streak', rarity: 'rare', unlockedAt: '2026-02-04T10:00:00Z' },
        { id: 'ach-mid', name: 'Mid', description: 'Mid desc', category: 'chat', rarity: 'epic', unlockedAt: '2026-01-15T10:00:00Z' },
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

  describe('Rarity Badges', () => {
    it('shows rarity badge for each achievement', () => {
      renderComponent({ achievements: mockAchievements });

      // Check for at least one of each rarity in the mock data
      expect(screen.getByTestId('rarity-badge-rare')).toBeInTheDocument();
      expect(screen.getByTestId('rarity-badge-epic')).toBeInTheDocument();
      expect(screen.getByTestId('rarity-badge-common')).toBeInTheDocument();
    });

    it('displays correct rarity labels', () => {
      const allRarities: Achievement[] = [
        { id: 'common', name: 'Common Achievement', description: 'desc', category: 'gameplay', rarity: 'common', unlockedAt: '2026-02-04T10:00:00Z' },
        { id: 'rare', name: 'Rare Achievement', description: 'desc', category: 'gameplay', rarity: 'rare', unlockedAt: '2026-02-03T10:00:00Z' },
        { id: 'epic', name: 'Epic Achievement', description: 'desc', category: 'gameplay', rarity: 'epic', unlockedAt: '2026-02-02T10:00:00Z' },
      ];

      renderComponent({ achievements: allRarities });

      expect(screen.getByTestId('rarity-badge-common')).toHaveTextContent('COMMON');
      expect(screen.getByTestId('rarity-badge-rare')).toHaveTextContent('RARE');
      expect(screen.getByTestId('rarity-badge-epic')).toHaveTextContent('EPIC');
    });

    it('shows legendary rarity badge', () => {
      const legendaryAchievement: Achievement[] = [
        { id: 'legendary', name: 'Legendary Achievement', description: 'desc', category: 'milestone', rarity: 'legendary', unlockedAt: '2026-02-04T10:00:00Z' },
      ];

      renderComponent({ achievements: legendaryAchievement });

      expect(screen.getByTestId('rarity-badge-legendary')).toHaveTextContent('LEGENDARY');
    });
  });

  describe('Category Icons', () => {
    it('shows category icons for achievements', () => {
      const allCategories: Achievement[] = [
        { id: 'coll', name: 'Collection', description: 'desc', category: 'collection', rarity: 'common', unlockedAt: '2026-02-04T10:00:00Z' },
        { id: 'game', name: 'Gameplay', description: 'desc', category: 'gameplay', rarity: 'common', unlockedAt: '2026-02-03T10:00:00Z' },
        { id: 'chat', name: 'Chat', description: 'desc', category: 'chat', rarity: 'common', unlockedAt: '2026-02-02T10:00:00Z' },
      ];

      renderComponent({ achievements: allCategories });

      expect(screen.getByTestId('category-icon-collection')).toBeInTheDocument();
      expect(screen.getByTestId('category-icon-gameplay')).toBeInTheDocument();
      expect(screen.getByTestId('category-icon-chat')).toBeInTheDocument();
    });

    it('shows streak category icon', () => {
      const streakAchievement: Achievement[] = [
        { id: 'streak', name: 'Streak', description: 'desc', category: 'streak', rarity: 'rare', unlockedAt: '2026-02-04T10:00:00Z' },
      ];

      renderComponent({ achievements: streakAchievement });

      expect(screen.getByTestId('category-icon-streak')).toBeInTheDocument();
    });

    it('shows milestone category icon', () => {
      const milestoneAchievement: Achievement[] = [
        { id: 'milestone', name: 'Milestone', description: 'desc', category: 'milestone', rarity: 'legendary', unlockedAt: '2026-02-04T10:00:00Z' },
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
  });
});
