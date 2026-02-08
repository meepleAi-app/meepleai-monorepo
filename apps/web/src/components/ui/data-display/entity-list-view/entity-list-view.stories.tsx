/**
 * Storybook stories for EntityListView component
 *
 * Phase 1: Grid mode stories only
 * Phase 2+: List, Carousel, Search, Sort, Filter stories
 */

import type { Meta, StoryObj } from '@storybook/react';
import { Users, Clock } from 'lucide-react';
import { EntityListView } from './entity-list-view';
import type { EntityListViewProps } from './entity-list-view.types';

// Mock game data
interface Game {
  id: string;
  title: string;
  publisher: string;
  rating: number;
  minPlayers: number;
  maxPlayers: number;
  playtime: number;
  imageUrl?: string;
}

const mockGames: Game[] = [
  {
    id: '1',
    title: 'Twilight Imperium',
    publisher: 'Fantasy Flight Games',
    rating: 8.7,
    minPlayers: 3,
    maxPlayers: 6,
    playtime: 480,
    imageUrl: 'https://cf.geekdo-images.com/0jySN1LmpUusSZfWwOLI9g__original/img/0dxeEjHJiuYYsOXC4xS5M8cL08Q=/0x0/filters:format(jpeg)/pic7493297.jpg',
  },
  {
    id: '2',
    title: 'Gloomhaven',
    publisher: 'Cephalofair Games',
    rating: 8.8,
    minPlayers: 1,
    maxPlayers: 4,
    playtime: 120,
    imageUrl: 'https://cf.geekdo-images.com/sZYp_3BTDGjh2unaZfZmuA__original/img/pBaOL7vJBzDrs99j04-BLvmS4B8=/0x0/filters:format(jpeg)/pic2437871.jpg',
  },
  {
    id: '3',
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    rating: 8.1,
    minPlayers: 1,
    maxPlayers: 5,
    playtime: 70,
    imageUrl: 'https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__original/img/uIjeoKgHMcRkEjGOQVGLg2JD75E=/0x0/filters:format(jpeg)/pic4458123.jpg',
  },
  {
    id: '4',
    title: 'Azul',
    publisher: 'Plan B Games',
    rating: 7.9,
    minPlayers: 2,
    maxPlayers: 4,
    playtime: 45,
    imageUrl: 'https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__original/img/qIncPzl-00XM5c8dnD9c9HTi-XM=/0x0/filters:format(jpeg)/pic6973671.jpg',
  },
  {
    id: '5',
    title: '7 Wonders Duel',
    publisher: 'Repos Production',
    rating: 8.1,
    minPlayers: 2,
    maxPlayers: 2,
    playtime: 30,
    imageUrl: 'https://cf.geekdo-images.com/WzNs1mA_o22ZXLJ9uS9MOw__original/img/S_1xSDQ82TJ2VuGzf6NfNHJFm_0=/0x0/filters:format(jpeg)/pic2576399.jpg',
  },
  {
    id: '6',
    title: 'Terraforming Mars',
    publisher: 'FryxGames',
    rating: 8.4,
    minPlayers: 1,
    maxPlayers: 5,
    playtime: 120,
    imageUrl: 'https://cf.geekdo-images.com/wg9oOLcsKvDesSUdZQ4rxw__original/img/FS1RE8Ue6nk1pNbPI3l-OSapQGc=/0x0/filters:format(jpeg)/pic3536616.jpg',
  },
  {
    id: '7',
    title: 'Brass: Birmingham',
    publisher: 'Roxley Games',
    rating: 8.6,
    minPlayers: 2,
    maxPlayers: 4,
    playtime: 120,
    imageUrl: 'https://cf.geekdo-images.com/x3zxjr-Vw5iU4yDPg70Jgw__original/img/FpyxH41Y6_ROoePAilPNEhXnzO8=/0x0/filters:format(jpeg)/pic3490053.jpg',
  },
  {
    id: '8',
    title: 'Spirit Island',
    publisher: 'Greater Than Games',
    rating: 8.4,
    minPlayers: 1,
    maxPlayers: 4,
    playtime: 120,
    imageUrl: 'https://cf.geekdo-images.com/0vJwP6NGa7GHPZKC3gplUw__original/img/cI7AHdRqLhx_-o28urDcmXFu3gE=/0x0/filters:format(jpeg)/pic7615963.jpg',
  },
];

// ============================================================================
// Meta Configuration
// ============================================================================

const meta: Meta<typeof EntityListView> = {
  title: 'UI/Data Display/EntityListView',
  component: EntityListView,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Generic component for displaying entity lists in Grid/List/Carousel modes with search, sort, filter capabilities. **Phase 1: Grid mode only**',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    entity: {
      control: 'select',
      options: ['game', 'player', 'collection', 'event', 'custom'],
      description: 'Entity type (determines color scheme)',
    },
    defaultViewMode: {
      control: 'radio',
      options: ['grid', 'list', 'carousel'],
      description: 'Default view mode (Phase 1: grid only)',
    },
    gridGap: {
      control: 'select',
      options: [2, 3, 4, 5, 6, 8],
      description: 'Grid gap spacing (Tailwind scale)',
    },
    loading: {
      control: 'boolean',
      description: 'Loading state',
    },
    showViewSwitcher: {
      control: 'boolean',
      description: 'Show view mode switcher',
    },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Stories
// ============================================================================

/**
 * Default grid layout with 8 game cards
 */
export const Default: Story = {
  args: {
    items: mockGames,
    entity: 'game',
    persistenceKey: 'storybook-default',
    renderItem: (game: Game) => ({
      id: game.id,
      title: game.title,
      subtitle: game.publisher,
      imageUrl: game.imageUrl,
      rating: game.rating,
      ratingMax: 10,
      metadata: [
        { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` },
        { icon: Clock, value: `${game.playtime}m` },
      ],
    }),
    title: 'Featured Games',
    subtitle: 'Explore our collection',
  },
};

/**
 * Grid with 2 columns (tablet-like)
 */
export const TwoColumnGrid: Story = {
  args: {
    ...Default.args,
    gridColumns: { default: 1, sm: 2 },
    title: 'Two Column Grid',
  },
};

/**
 * Grid with larger gap
 */
export const LargeGap: Story = {
  args: {
    ...Default.args,
    gridGap: 8,
    title: 'Large Gap Grid',
  },
};

/**
 * Empty state
 */
export const Empty: Story = {
  args: {
    ...Default.args,
    items: [],
    emptyMessage: 'No games found. Try adjusting your filters.',
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  args: {
    ...Default.args,
    loading: true,
  },
};

/**
 * Without view switcher
 */
export const NoViewSwitcher: Story = {
  args: {
    ...Default.args,
    showViewSwitcher: false,
    title: 'Grid Only (No Switcher)',
  },
};

/**
 * Without title or subtitle
 */
export const NoHeader: Story = {
  args: {
    ...Default.args,
    title: undefined,
    subtitle: undefined,
    showViewSwitcher: true,
  },
};

/**
 * Player entity (purple color)
 */
export const PlayerEntity: Story = {
  args: {
    items: [
      { id: '1', name: 'Marco Rossi', username: '@marco_games', plays: 142 },
      { id: '2', name: 'Anna Bianchi', username: '@anna_board', plays: 89 },
      { id: '3', name: 'Luca Verdi', username: '@luca_tabletop', plays: 201 },
    ],
    entity: 'player',
    persistenceKey: 'storybook-players',
    renderItem: (player: any) => ({
      id: player.id,
      title: player.name,
      subtitle: player.username,
      metadata: [{ value: `${player.plays} plays` }],
    }),
    title: 'Top Players',
  },
};

/**
 * Collection entity (teal color)
 */
export const CollectionEntity: Story = {
  args: {
    items: [
      { id: '1', name: 'Strategy Games', gameCount: 24, description: 'Deep thinkers' },
      { id: '2', name: 'Party Games', gameCount: 12, description: 'Fun for groups' },
      { id: '3', name: 'Two Player', gameCount: 18, description: 'Perfect for couples' },
    ],
    entity: 'collection',
    persistenceKey: 'storybook-collections',
    renderItem: (collection: any) => ({
      id: collection.id,
      title: collection.name,
      subtitle: collection.description,
      metadata: [{ value: `${collection.gameCount} games` }],
    }),
    title: 'My Collections',
  },
};

/**
 * Large dataset (20 items)
 */
export const LargeDataset: Story = {
  args: {
    items: Array.from({ length: 20 }, (_, i) => ({
      id: `game-${i + 1}`,
      title: `Game ${i + 1}`,
      publisher: `Publisher ${i + 1}`,
      rating: 7 + Math.random() * 2,
      minPlayers: 1 + Math.floor(Math.random() * 2),
      maxPlayers: 4 + Math.floor(Math.random() * 4),
      playtime: 30 + Math.floor(Math.random() * 120),
    })),
    entity: 'game',
    persistenceKey: 'storybook-large',
    renderItem: (game: Game) => ({
      id: game.id,
      title: game.title,
      subtitle: game.publisher,
      rating: game.rating,
      ratingMax: 10,
      metadata: [
        { icon: Users, value: `${game.minPlayers}-${game.maxPlayers}` },
        { icon: Clock, value: `${game.playtime}m` },
      ],
    }),
    title: 'Large Dataset (20 items)',
    subtitle: 'Testing performance with many cards',
  },
};

/**
 * Custom className example
 */
export const CustomStyling: Story = {
  args: {
    ...Default.args,
    className: 'bg-accent/5 p-6 rounded-2xl',
    cardClassName: 'hover:scale-105',
    title: 'Custom Styled Grid',
  },
};

/**
 * Interactive example with click handler
 */
export const WithClickHandler: Story = {
  args: {
    ...Default.args,
    onItemClick: (game: Game) => {
      alert(`Clicked: ${game.title}`);
    },
    title: 'Click Cards to Test',
    subtitle: 'Cards are interactive - click to see alert',
  },
};
