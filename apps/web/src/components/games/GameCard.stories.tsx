/**
 * GameCard Storybook Stories (Issue #1830: UI-003)
 *
 * Comprehensive stories for both Grid and List variants
 * Covers: ratings, badges, states, responsive behavior
 */

import type { Game } from '@/lib/api';

import { GameCard } from './GameCard';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/Games/GameCard',
  component: GameCard,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['grid', 'list'],
      description: 'Card display variant',
    },
    onClick: { action: 'clicked' },
  },
} satisfies Meta<typeof GameCard>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Mock Data
// ============================================================================

const mockGameBase: Game = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  title: 'Azul',
  publisher: 'Plan B Games',
  yearPublished: 2017,
  minPlayers: 2,
  maxPlayers: 4,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 45,
  bggId: 230802,
  createdAt: '2024-01-15T10:00:00Z',
  imageUrl:
    'https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__imagepage/img/q4uWd2nXGeNcCMz_5sGW4Qsrw6c=/fit-in/900x600/filters:no_upscale():strip_icc()/pic6973671.png',
  averageRating: 7.8,
  faqCount: 12,
};

const mockGameNoImage: Game = {
  ...mockGameBase,
  id: '223e4567-e89b-12d3-a456-426614174001',
  title: 'Carcassonne',
  imageUrl: null,
  averageRating: 7.4,
  faqCount: 8,
};

const mockGameLongTitle: Game = {
  ...mockGameBase,
  id: '323e4567-e89b-12d3-a456-426614174002',
  title: 'Twilight Imperium: Fourth Edition - Prophecy of Kings Expansion',
  publisher: 'Fantasy Flight Games',
  yearPublished: 2020,
  minPlayers: 3,
  maxPlayers: 8,
  minPlayTimeMinutes: 240,
  maxPlayTimeMinutes: 480,
  averageRating: 8.7,
  faqCount: 45,
};

const mockGameMinimalData: Game = {
  id: '423e4567-e89b-12d3-a456-426614174003',
  title: 'Mystery Game',
  publisher: null,
  yearPublished: null,
  minPlayers: null,
  maxPlayers: null,
  minPlayTimeMinutes: null,
  maxPlayTimeMinutes: null,
  bggId: null,
  createdAt: '2024-01-15T10:00:00Z',
  imageUrl: null,
  averageRating: null,
  faqCount: null,
};

const mockGameHighRating: Game = {
  ...mockGameBase,
  id: '523e4567-e89b-12d3-a456-426614174004',
  title: 'Gloomhaven',
  publisher: 'Cephalofair Games',
  yearPublished: 2017,
  minPlayers: 1,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  averageRating: 8.9,
  faqCount: 67,
  imageUrl:
    'https://cf.geekdo-images.com/sZYp_3BTDGjh2unaZfZmuA__imagepage/img/pBaOL7vV402nn1I5dHsdSKsFHqA=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2437871.jpg',
};

// ============================================================================
// Grid Variant Stories
// ============================================================================

export const GridDefault: Story = {
  args: {
    game: mockGameBase,
    variant: 'grid',
  },
};

export const GridNoImage: Story = {
  args: {
    game: mockGameNoImage,
    variant: 'grid',
  },
};

export const GridLongTitle: Story = {
  args: {
    game: mockGameLongTitle,
    variant: 'grid',
  },
};

export const GridMinimalData: Story = {
  args: {
    game: mockGameMinimalData,
    variant: 'grid',
    showRating: false,
  },
};

export const GridHighRating: Story = {
  args: {
    game: mockGameHighRating,
    variant: 'grid',
  },
};

// ============================================================================
// List Variant Stories
// ============================================================================

export const ListDefault: Story = {
  args: {
    game: mockGameBase,
    variant: 'list',
  },
};

export const ListNoImage: Story = {
  args: {
    game: mockGameNoImage,
    variant: 'list',
  },
};

export const ListLongTitle: Story = {
  args: {
    game: mockGameLongTitle,
    variant: 'list',
  },
};

export const ListMinimalData: Story = {
  args: {
    game: mockGameMinimalData,
    variant: 'list',
    showRating: false,
  },
};

export const ListHighRating: Story = {
  args: {
    game: mockGameHighRating,
    variant: 'list',
  },
};

// ============================================================================
// Responsive Grid Layout
// ============================================================================

export const ResponsiveGrid: Story = {
  render: () => (
    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
      <GameCard game={mockGameBase} variant="grid" />
      <GameCard game={mockGameNoImage} variant="grid" />
      <GameCard game={mockGameLongTitle} variant="grid" />
      <GameCard game={mockGameHighRating} variant="grid" />
      <GameCard game={mockGameMinimalData} variant="grid" showRating={false} />
      <GameCard game={{ ...mockGameBase, id: '6', title: 'Wingspan' }} variant="grid" />
      <GameCard game={{ ...mockGameBase, id: '7', title: '7 Wonders' }} variant="grid" />
      <GameCard game={{ ...mockGameBase, id: '8', title: 'Pandemic' }} variant="grid" />
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Responsive grid: 2 columns (mobile) → 3 columns (tablet) → 4 columns (desktop)',
      },
    },
  },
};

// ============================================================================
// List Layout
// ============================================================================

export const ListLayout: Story = {
  render: () => (
    <div className="flex flex-col gap-3 max-w-2xl">
      <GameCard game={mockGameBase} variant="list" />
      <GameCard game={mockGameNoImage} variant="list" />
      <GameCard game={mockGameLongTitle} variant="list" />
      <GameCard game={mockGameHighRating} variant="list" />
      <GameCard game={mockGameMinimalData} variant="list" showRating={false} />
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Full-width list layout with detailed metadata',
      },
    },
  },
};

// ============================================================================
// Interactive States
// ============================================================================

export const WithClickHandler: Story = {
  args: {
    game: mockGameBase,
    variant: 'grid',
    onClick: () => alert('Game card clicked!'),
  },
  parameters: {
    docs: {
      description: {
        story: 'Card with click handler - shows hover effects and keyboard navigation',
      },
    },
  },
};

export const HoverState: Story = {
  args: {
    game: mockGameBase,
    variant: 'grid',
  },
  parameters: {
    pseudo: { hover: true },
    docs: {
      description: {
        story: 'Hover state: translateY(-4px) + shadow-lg + image scale(1.05)',
      },
    },
  },
};
