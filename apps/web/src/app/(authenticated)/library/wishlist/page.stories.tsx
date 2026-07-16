/**
 * sp4-library-wishlist — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-library-wishlist.{html,jsx}`.
 */

import WishlistPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof WishlistPage> = {
  title: 'Authenticated / sp4-library-wishlist',
  component: WishlistPage,
  parameters: {
    layout: 'fullscreen',
    // DS-17 #2342: single Default story renders the default state; declare it so the
    // canonical-state coverage gate detects it (no quoted state literal to scan).
    canonicalStates: ['default'],
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component: '#2214 DS-17-12. Wishlist sub-page of authenticated library.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof WishlistPage>;

export const Default: Story = {};
