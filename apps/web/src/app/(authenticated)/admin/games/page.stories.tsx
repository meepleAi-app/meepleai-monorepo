import { GamesClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Admin Games Dashboard (Issue #2515)
 *
 * ## Features
 * - **Games List**: Table with filters (Status, Search, Sort) and pagination
 * - **Game Edit Modal**: Configure game details with PDF/image upload
 * - **Approval Review Modal**: Preview, diff view, approve/reject workflow
 * - **3-State Workflow**: Draft → PendingApproval → Published
 *
 * ## Access Control
 * - Requires Admin or Editor role
 * - Approval actions restricted to Admin only
 * - Server-side auth check via middleware
 */
const meta = {
  title: 'Admin/GamesManagement',
  component: GamesClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof GamesClient>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock games data
const mockGames = [
  {
    id: 'game-1',
    title: 'Catan',
    description: 'Trade, build, and settle the island of Catan',
    bggId: 13,
    status: 'Draft',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 120,
    complexityRating: 2.5,
    imageUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__imagepage/img/M_3Vg1j2WV' +
'I6puDcxe_zMW0FwjGY=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2419375.jpg',
    lastModifiedAt: '2026-01-15T10:00:00Z',
  },
  {
    id: 'game-2',
    title: 'Wingspan',
    description: 'Attract birds to your wildlife preserve',
    bggId: 266192,
    status: 'PendingApproval',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
    complexityRating: 2.4,
    imageUrl: 'https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__imagepage/img/uIjeoKgHMcDGg' +
'N-TJ9qPqRJFmGo=/fit-in/900x600/filters:no_upscale():strip_icc()/pic4458123.jpg',
    lastModifiedAt: '2026-01-14T15:30:00Z',
  },
  {
    id: 'game-3',
    title: 'Ticket to Ride',
    description: 'Cross-country train adventure',
    bggId: 9209,
    status: 'Published',
    minPlayers: 2,
    maxPlayers: 5,
    playingTimeMinutes: 60,
    complexityRating: 1.9,
    imageUrl: 'https://cf.geekdo-images.com/ZWJg0dCdrWHxVnc0eFXK8w__imagepage/img/pFnUOw3' +
'P6cbVhJz2FPrtw-FFf1o=/fit-in/900x600/filters:no_upscale():strip_icc()/pic38668.jpg',
    lastModifiedAt: '2026-01-10T08:00:00Z',
  },
  {
    id: 'game-4',
    title: '7 Wonders',
    description: 'Build your civilization through three ages',
    bggId: 68448,
    status: 'Archived',
    minPlayers: 2,
    maxPlayers: 7,
    playingTimeMinutes: 30,
    complexityRating: 2.3,
    imageUrl: 'https://cf.geekdo-images.com/35h9Za_JvMMMtx_92kT0Jg__imagepage/img/yqa73' +
'j7dVCmDGX5retVUNLLpcio=/fit-in/900x600/filters:no_upscale():strip_icc()/pic860217.jpg',
    lastModifiedAt: '2025-12-20T12:00:00Z',
  },
];

/**
 * Default view with games in various statuses.
 * Shows full workflow: Draft, Pending, Published, Archived.
 */
export const Default: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/shared-games',
        method: 'GET',
        status: 200,
        response: {
          items: mockGames,
          total: mockGames.length,
          page: 1,
          pageSize: 20,
        },
      },
    ],
  },
};

/**
 * Loading state while fetching games.
 */
export const Loading: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/shared-games',
        method: 'GET',
        delay: 'infinite',
      },
    ],
  },
};

/**
 * Empty state with no games.
 */
export const Empty: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/shared-games',
        method: 'GET',
        status: 200,
        response: {
          items: [],
          total: 0,
          page: 1,
          pageSize: 20,
        },
      },
    ],
  },
};

/**
 * Filtered view (Pending Approval only).
 */
export const PendingApprovalFilter: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/shared-games?status=1',
        method: 'GET',
        status: 200,
        response: {
          items: mockGames.filter((g) => g.status === 'PendingApproval'),
          total: 1,
          page: 1,
          pageSize: 20,
        },
      },
    ],
  },
};

/**
 * Mobile viewport (375px).
 */
export const Mobile: Story = {
  parameters: {
    viewport: { defaultViewport: 'mobile1' },
    chromatic: { viewports: [375] },
    mockData: [
      {
        url: '/api/v1/admin/shared-games',
        method: 'GET',
        status: 200,
        response: {
          items: mockGames,
          total: mockGames.length,
          page: 1,
          pageSize: 20,
        },
      },
    ],
  },
};

/**
 * Tablet viewport (768px).
 */
export const Tablet: Story = {
  parameters: {
    viewport: { defaultViewport: 'tablet' },
    chromatic: { viewports: [768] },
    mockData: [
      {
        url: '/api/v1/admin/shared-games',
        method: 'GET',
        status: 200,
        response: {
          items: mockGames,
          total: mockGames.length,
          page: 1,
          pageSize: 20,
        },
      },
    ],
  },
};
