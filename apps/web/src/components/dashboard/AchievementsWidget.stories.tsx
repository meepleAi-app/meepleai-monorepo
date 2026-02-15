/**
 * AchievementsWidget Storybook Stories (Issue #4411)
 *
 * Stories covering all achievement widget states:
 * - Default (mix of unlocked achievements)
 * - All rarity variants (common, rare, epic, legendary)
 * - Empty state (no achievements)
 * - New unlock celebration animation
 * - Loading skeleton
 * - Locked badges with unlock tooltip
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import type { Achievement, NextAchievementProgress } from './AchievementsWidget';
import { AchievementsWidget } from './AchievementsWidget';

import type { Meta, StoryObj } from '@storybook/react';

// ============================================================================
// Mock Data
// ============================================================================

const now = new Date();
const daysAgo = (days: number) => new Date(now.getTime() - days * 86400000).toISOString();

const mockAchievements: Achievement[] = [
  {
    id: 'ach-1',
    name: 'Primo Gioco',
    description: 'Aggiungi il tuo primo gioco alla libreria',
    category: 'collection',
    rarity: 'common',
    points: 10,
    unlockedAt: daysAgo(1),
  },
  {
    id: 'ach-2',
    name: 'Esploratore',
    description: 'Prova 10 giochi diversi',
    category: 'gameplay',
    rarity: 'rare',
    points: 25,
    unlockedAt: daysAgo(3),
  },
  {
    id: 'ach-3',
    name: 'Chat Master',
    description: 'Fai 50 domande al chatbot',
    category: 'chat',
    rarity: 'epic',
    points: 50,
    unlockedAt: daysAgo(5),
  },
  {
    id: 'ach-4',
    name: 'Leggenda Vivente',
    description: 'Completa tutte le sfide settimanali per un mese',
    category: 'streak',
    rarity: 'legendary',
    points: 100,
    unlockedAt: daysAgo(0),
  },
];

const mockLockedAchievements: Achievement[] = [
  {
    id: 'ach-locked-1',
    name: 'Collezionista',
    description: 'Aggiungi 50 giochi alla libreria',
    category: 'collection',
    rarity: 'rare',
    points: 30,
    unlockedAt: '',
    isLocked: true,
    unlockHint: 'Aggiungi altri 35 giochi alla tua libreria',
  },
  {
    id: 'ach-locked-2',
    name: 'Maestro Tattico',
    description: 'Vinci 100 partite di giochi strategici',
    category: 'gameplay',
    rarity: 'epic',
    points: 75,
    unlockedAt: '',
    isLocked: true,
    unlockHint: 'Vinci altre 67 partite strategiche',
  },
  {
    id: 'ach-locked-3',
    name: 'Il Prescelto',
    description: 'Ottieni tutti gli achievement del gioco',
    category: 'milestone',
    rarity: 'legendary',
    points: 200,
    unlockedAt: '',
    isLocked: true,
    unlockHint: 'Sblocca tutti i 49 achievement rimanenti',
  },
];

const mockNextProgress: NextAchievementProgress = {
  achievementName: 'Collezionista Pro',
  current: 15,
  target: 50,
};

// ============================================================================
// Wrapper
// ============================================================================

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false, refetchOnWindowFocus: false },
  },
});

function StoryWrapper({ children }: { children: React.ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <div className="w-[380px]">{children}</div>
    </QueryClientProvider>
  );
}

// ============================================================================
// Meta
// ============================================================================

const meta = {
  title: 'Components/Dashboard/AchievementsWidget',
  component: AchievementsWidget,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: `
Dashboard widget for recent achievements and badges.

**Features:**
- Last 3 unlocked achievements with rarity-based colors
- Celebration animation on new unlock
- Progress bar for next achievement
- Tooltip on locked badges showing unlock requirements
- Category icons (collection, gameplay, chat, streak, milestone)
        `,
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story: React.ComponentType) => (
      <StoryWrapper>
        <Story />
      </StoryWrapper>
    ),
  ],
} satisfies Meta<typeof AchievementsWidget>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Stories
// ============================================================================

/** Default state with a mix of unlocked achievements */
export const Default: Story = {
  args: {
    achievements: mockAchievements.slice(0, 3),
    nextProgress: mockNextProgress,
    isLoading: false,
  },
};

/** All rarity levels displayed: common (gray), rare (blue), epic (purple), legendary (gold) */
export const AllRarities: Story = {
  args: {
    achievements: mockAchievements,
    nextProgress: mockNextProgress,
    isLoading: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows all 4 rarity variants: common (gray), rare (blue), epic (purple), legendary (gold/amber).',
      },
    },
  },
};

/** Empty state - no achievements unlocked yet */
export const EmptyState: Story = {
  args: {
    achievements: [],
    nextProgress: null,
    isLoading: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Displayed when the user has not unlocked any achievements yet.',
      },
    },
  },
};

/** Celebration animation for a newly unlocked achievement */
export const WithNewUnlock: Story = {
  args: {
    achievements: mockAchievements.slice(0, 3),
    nextProgress: mockNextProgress,
    isLoading: false,
    hasNewUnlock: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Spring scale animation + sparkle effect on the first achievement when a new unlock occurs.',
      },
    },
  },
};

/** Loading skeleton state */
export const Loading: Story = {
  args: {
    isLoading: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Skeleton placeholder shown while achievement data is being fetched.',
      },
    },
  },
};

/** Locked badges with hover tooltip showing unlock requirements */
export const LockedWithTooltip: Story = {
  args: {
    achievements: [mockAchievements[0], ...mockLockedAchievements.slice(0, 2)],
    nextProgress: mockNextProgress,
    isLoading: false,
  },
  parameters: {
    docs: {
      description: {
        story: 'Locked badges show a Lock icon and muted colors. Hover to see the unlock hint tooltip.',
      },
    },
  },
};
