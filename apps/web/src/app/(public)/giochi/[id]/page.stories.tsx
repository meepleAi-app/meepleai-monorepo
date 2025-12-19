/**
 * Game Detail Page Storybook Stories
 * Full page visual regression testing
 * Issue #1841 (PAGE-005)
 */

import type { Meta, StoryObj } from '@storybook/react';
import { GameDetailClient } from './game-detail-client';

const meta: Meta<typeof GameDetailClient> = {
  title: 'Pages/Game Detail (Giochi)',
  component: GameDetailClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: { viewports: [375, 768, 1920] },
  },
};

export default meta;
type Story = StoryObj<typeof GameDetailClient>;

const mockGame = {
  id: '1',
  title: 'Catan',
  publisher: 'Kosmos',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  imageUrl:
    'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__imagepage/img/M_3Vg1j2HlNgkv7PL2xl2BJE2bw=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2419375.jpg',
  averageRating: 7.2,
  createdAt: '2024-01-01T00:00:00Z',
};

export const Default: Story = {
  args: { game: mockGame },
};

export const WithoutImage: Story = {
  args: {
    game: { ...mockGame, imageUrl: null },
  },
};

export const Mobile: Story = {
  args: { game: mockGame },
  parameters: { viewport: { defaultViewport: 'mobile1' } },
};
