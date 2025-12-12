/**
 * SharedChatView Storybook Stories (Issue #2052)
 *
 * Visual documentation for shared chat thread page.
 * Note: Full E2E testing handled by Playwright.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { SharedChatView } from './SharedChatView';

const meta: Meta<typeof SharedChatView> = {
  title: 'Pages/SharedChatView',
  component: SharedChatView,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Public page for viewing shared chat threads. Access controlled by JWT token in URL query parameter.',
      },
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Loading state
 * Note: Actual states tested via Playwright E2E
 */
export const Loading: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Loading state while fetching shared thread data.',
      },
    },
  },
};
