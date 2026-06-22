/**
 * sp4-game-chat-tab — DS-17-12 #2214 sub-issue.
 *
 * Component-mock embedded in /games/[id] + /library/[gameId]/agent routes.
 * Mockup parity: `admin-mockups/design_files/sp4-game-chat-tab.{html,jsx}`.
 */

import { GameChatTab } from './GameChatTab';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof GameChatTab> = {
  title: 'Component-mocks / sp4-game-chat-tab',
  component: GameChatTab,
  parameters: {
    layout: 'padded',
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Game chat tab embedded in game detail + library agent routes. Component-mock (no standalone route).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof GameChatTab>;

export const Default: Story = {
  args: {
    gameId: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
  },
};
