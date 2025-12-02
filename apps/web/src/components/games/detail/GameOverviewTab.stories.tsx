import type { Meta, StoryObj } from '@storybook/react';
import { GameOverviewTab } from './GameOverviewTab';
import { api } from '@/lib/api';
import type { Game, BggGameDetails } from '@/lib/api';

// Mock API for Storybook
const mockBggDetails: BggGameDetails = {
  id: 13,
  name: 'Catan',
  imageUrl:
    'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__imagepage/img/8a9HeqFydO7Uun_le9bXWPnidcA=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2419375.jpg',
  description:
    'In Catan, players try to be the dominant force on the island of Catan by building settlements, cities, and roads.',
  averageRating: 7.2,
  averageWeight: 2.32,
  usersRated: 97000,
  minAge: 10,
  categories: ['Negotiation', 'Economic'],
  mechanics: ['Dice Rolling', 'Trading', 'Network Building'],
  designers: ['Klaus Teuber'],
  publishers: ['Catan Studio', 'Kosmos', 'Filosofia'],
};

// Mock API for Storybook - use plain function instead of vi.fn() which is Vitest-only
(api.bgg.getGameDetails as unknown) = () => Promise.resolve(mockBggDetails);

const mockGame: Game = {
  id: 'game-1',
  title: 'Catan',
  publisher: 'Catan Studio',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  createdAt: new Date('2024-01-15').toISOString(),
  imageUrl: null,
  faqCount: 42,
  averageRating: 7.2,
};

const meta = {
  title: 'Games/Detail/GameOverviewTab',
  component: GameOverviewTab,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof GameOverviewTab>;

export default meta;
type Story = StoryObj<typeof meta>;

export const WithBGG: Story = {
  args: {
    game: mockGame,
  },
};

export const WithoutBGG: Story = {
  args: {
    game: {
      ...mockGame,
      bggId: null,
    },
  },
};

export const MinimalGame: Story = {
  args: {
    game: {
      ...mockGame,
      publisher: null,
      yearPublished: null,
      minPlayers: null,
      maxPlayers: null,
      minPlayTimeMinutes: null,
      maxPlayTimeMinutes: null,
      bggId: null,
    },
  },
};

export const SinglePlayerCount: Story = {
  args: {
    game: {
      ...mockGame,
      minPlayers: 4,
      maxPlayers: 4,
      minPlayTimeMinutes: 90,
      maxPlayTimeMinutes: 90,
    },
  },
};

export const LoadingBGG: Story = {
  args: {
    game: mockGame,
  },
  parameters: {
    docs: {
      description: {
        story: 'Loading state while fetching BoardGameGeek details (skeleton loaders).',
      },
    },
  },
};

export const BGGError: Story = {
  args: {
    game: mockGame,
  },
  decorators: [
    () => {
      // Use plain function mock instead of vi.fn() which is Vitest-only
      (api.bgg.getGameDetails as unknown) = () => Promise.reject(new Error('Network error'));
      return <GameOverviewTab game={mockGame} />;
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Error state when BGG API fetch fails.',
      },
    },
  },
};

export const DarkTheme: Story = {
  args: {
    game: mockGame,
  },
  decorators: [
    Story => (
      <div className="dark bg-background p-6">
        <Story />
      </div>
    ),
  ],
  parameters: {
    backgrounds: { default: 'dark' },
  },
};
