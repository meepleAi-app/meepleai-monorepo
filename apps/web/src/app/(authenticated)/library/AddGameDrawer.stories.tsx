/**
 * sp4-add-game-drawer — DS-17-12 #2214 sub-issue.
 *
 * Component-mock embedded in /library route (Add game drawer flow).
 * Mockup parity: `admin-mockups/design_files/sp4-add-game-drawer.{html,jsx}`.
 * ADR-059 docstring preserved (admin-only constraint documentation).
 */

import { AddGameDrawer } from './AddGameDrawer';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AddGameDrawer> = {
  title: 'Component-mocks / sp4-add-game-drawer',
  component: AddGameDrawer,
  parameters: {
    layout: 'padded',
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2214 DS-17-12. Add game drawer embedded in /library route. Component-mock (no standalone route). ADR-059: catalog admin-only constraint.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AddGameDrawer>;

export const Open: Story = {
  args: {
    open: true,
    onClose: () => undefined,
  },
};

export const Closed: Story = {
  args: {
    open: false,
    onClose: () => undefined,
  },
};
