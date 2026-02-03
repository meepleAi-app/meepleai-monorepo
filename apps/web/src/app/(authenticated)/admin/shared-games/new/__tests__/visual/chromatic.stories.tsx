/**
 * New Game Page - Visual Tests
 * Issue #2372 - Phase 3: Frontend Admin UI
 *
 * Chromatic visual regression tests for the New Game admin page.
 * Covers: header, form rendering, responsive design
 */

import type { Meta, StoryObj } from '@storybook/react';

import { NewGameClient } from '../../client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof NewGameClient> = {
  title: 'Admin/SharedGames/NewGame/Visual Tests',
  component: NewGameClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      disableSnapshot: false,
      delay: 500,
    },
  },
  decorators: [
    Story => (
      <AuthProvider>
        <div className="min-h-screen bg-gray-50">
          <Story />
        </div>
      </AuthProvider>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof NewGameClient>;

// ========== Mock Data ==========

const mockCategories = [
  { id: 'cat-1', name: 'Strategia', gameCount: 45 },
  { id: 'cat-2', name: 'Famiglia', gameCount: 32 },
  { id: 'cat-3', name: 'Party', gameCount: 28 },
  { id: 'cat-4', name: 'Cooperativo', gameCount: 19 },
  { id: 'cat-5', name: 'Fantascienza', gameCount: 15 },
];

const mockMechanics = [
  { id: 'mech-1', name: 'Dice Rolling', gameCount: 67 },
  { id: 'mech-2', name: 'Hand Management', gameCount: 54 },
  { id: 'mech-3', name: 'Area Control', gameCount: 38 },
  { id: 'mech-4', name: 'Worker Placement', gameCount: 29 },
  { id: 'mech-5', name: 'Deck Building', gameCount: 24 },
];

// ========== Stories ==========

/**
 * Default View - Empty form for new game
 */
export const DefaultView: Story = {
  parameters: {
    mockData: {
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Mobile View - Responsive form layout
 */
export const MobileView: Story = {
  parameters: {
    mockData: {
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet View - Medium screen form layout
 */
export const TabletView: Story = {
  parameters: {
    mockData: {
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};

/**
 * Categories Loading - Form with categories loading
 */
export const CategoriesLoading: Story = {
  parameters: {
    mockData: {
      '/api/v1/shared-games/categories': { delay: 'infinite' },
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Mechanics Loading - Form with mechanics loading
 */
export const MechanicsLoading: Story = {
  parameters: {
    mockData: {
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': { delay: 'infinite' },
    },
  },
};

/**
 * All Data Loading - Form with all reference data loading
 */
export const AllDataLoading: Story = {
  parameters: {
    mockData: {
      '/api/v1/shared-games/categories': { delay: 'infinite' },
      '/api/v1/shared-games/mechanics': { delay: 'infinite' },
    },
  },
};

/**
 * Many Categories - Form with large category list
 */
export const ManyCategories: Story = {
  parameters: {
    mockData: {
      '/api/v1/shared-games/categories': [
        ...mockCategories,
        { id: 'cat-6', name: 'Avventura', gameCount: 42 },
        { id: 'cat-7', name: 'Horror', gameCount: 18 },
        { id: 'cat-8', name: 'Economia', gameCount: 35 },
        { id: 'cat-9', name: 'Civilizzazione', gameCount: 22 },
        { id: 'cat-10', name: 'Trivia', gameCount: 15 },
        { id: 'cat-11', name: 'Esplorazione', gameCount: 30 },
        { id: 'cat-12', name: 'Combattimento', gameCount: 25 },
        { id: 'cat-13', name: 'Deduzione', gameCount: 20 },
        { id: 'cat-14', name: 'Simulazione', gameCount: 12 },
        { id: 'cat-15', name: 'Astratto', gameCount: 28 },
      ],
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Many Mechanics - Form with large mechanics list
 */
export const ManyMechanics: Story = {
  parameters: {
    mockData: {
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': [
        ...mockMechanics,
        { id: 'mech-6', name: 'Tile Placement', gameCount: 45 },
        { id: 'mech-7', name: 'Set Collection', gameCount: 52 },
        { id: 'mech-8', name: 'Auction/Bidding', gameCount: 33 },
        { id: 'mech-9', name: 'Trading', gameCount: 28 },
        { id: 'mech-10', name: 'Route Building', gameCount: 22 },
        { id: 'mech-11', name: 'Card Drafting', gameCount: 38 },
        { id: 'mech-12', name: 'Cooperative Play', gameCount: 31 },
        { id: 'mech-13', name: 'Hidden Roles', gameCount: 19 },
        { id: 'mech-14', name: 'Push Your Luck', gameCount: 25 },
        { id: 'mech-15', name: 'Resource Management', gameCount: 48 },
      ],
    },
  },
};

/**
 * Empty Reference Data - Form with no categories or mechanics
 */
export const EmptyReferenceData: Story = {
  parameters: {
    mockData: {
      '/api/v1/shared-games/categories': [],
      '/api/v1/shared-games/mechanics': [],
    },
  },
};
