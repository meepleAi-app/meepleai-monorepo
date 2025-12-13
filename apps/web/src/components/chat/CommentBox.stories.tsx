/**
 * CommentBox Storybook Stories (Issue #2052)
 *
 * Visual testing for comment input in shared chat threads.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { CommentBox } from './CommentBox';
import { fn } from 'storybook/test';

const meta: Meta<typeof CommentBox> = {
  title: 'Chat/CommentBox',
  component: CommentBox,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Comment input for shared chat threads. Rate-limited to 10 comments per hour per share link.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    token: {
      control: 'text',
      description: 'Share link JWT token',
    },
    onCommentAdded: {
      action: 'comment-added',
      description: 'Callback when comment is successfully added',
    },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default state: Empty comment box
 */
export const Default: Story = {
  args: {
    token: 'mock-jwt-token',
    onCommentAdded: fn(),
  },
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  args: {
    token: 'mock-jwt-token',
    onCommentAdded: fn(),
  },
  parameters: {
    backgrounds: {
      default: 'dark',
    },
  },
  decorators: [
    Story => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
};
