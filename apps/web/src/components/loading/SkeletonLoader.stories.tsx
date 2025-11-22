import type { Meta, StoryObj } from '@storybook/react';
import { SkeletonLoader } from './SkeletonLoader';

/**
 * SkeletonLoader component provides loading placeholders with variant-specific dimensions.
 * Uses CSS-based animation (Tailwind animate-pulse) for performance.
 * Respects user's reduced motion preferences.
 */
const meta = {
  title: 'Loading/SkeletonLoader',
  component: SkeletonLoader,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['games', 'agents', 'message', 'chatHistory', 'uploadQueue', 'processingProgress', 'gameSelection'],
      description: 'Visual variant matching the content type',
    },
    count: {
      control: { type: 'number', min: 1, max: 10, step: 1 },
      description: 'Number of skeleton placeholders to render',
    },
    animate: {
      control: 'boolean',
      description: 'Enable animation (respects prefers-reduced-motion)',
    },
    ariaLabel: {
      control: 'text',
      description: 'Accessible label for screen readers',
    },
  },
} satisfies Meta<typeof SkeletonLoader>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Games grid skeleton (for game cards)
 */
export const Games: Story = {
  args: {
    variant: 'games',
    count: 3,
  },
};

/**
 * Single game card skeleton
 */
export const SingleGame: Story = {
  args: {
    variant: 'games',
    count: 1,
  },
};

/**
 * Agents list skeleton
 */
export const Agents: Story = {
  args: {
    variant: 'agents',
    count: 3,
  },
};

/**
 * Chat messages skeleton
 */
export const Messages: Story = {
  args: {
    variant: 'message',
    count: 4,
  },
};

/**
 * Chat history sidebar skeleton
 */
export const ChatHistory: Story = {
  args: {
    variant: 'chatHistory',
    count: 8,
  },
};

/**
 * Upload queue items skeleton
 */
export const UploadQueue: Story = {
  args: {
    variant: 'uploadQueue',
    count: 2,
  },
};

/**
 * Processing progress skeleton
 */
export const ProcessingProgress: Story = {
  args: {
    variant: 'processingProgress',
    count: 1,
  },
};

/**
 * Game selection dropdown skeleton
 */
export const GameSelection: Story = {
  args: {
    variant: 'gameSelection',
    count: 1,
  },
};

/**
 * Without animation (respecting reduced motion)
 */
export const NoAnimation: Story = {
  args: {
    variant: 'message',
    count: 3,
    animate: false,
  },
};

/**
 * Custom aria label
 */
export const CustomAriaLabel: Story = {
  args: {
    variant: 'games',
    count: 2,
    ariaLabel: 'Loading game cards...',
  },
};

/**
 * Full chat interface skeleton
 */
export const ChatInterface: Story = {
  render: () => (
    <div className="flex gap-4 max-w-5xl">
      {/* Sidebar */}
      <div className="w-64 border-r pr-4">
        <h3 className="font-semibold mb-3">Chat History</h3>
        <SkeletonLoader variant="chatHistory" count={6} />
      </div>

      {/* Main chat area */}
      <div className="flex-1 space-y-4">
        <div>
          <h3 className="font-semibold mb-3">Messages</h3>
          <SkeletonLoader variant="message" count={3} />
        </div>

        <div>
          <h3 className="font-semibold mb-3">Game Selection</h3>
          <SkeletonLoader variant="gameSelection" count={1} />
        </div>
      </div>
    </div>
  ),
};

/**
 * Games grid layout
 */
export const GamesGrid: Story = {
  render: () => (
    <div>
      <h2 className="text-xl font-bold mb-4">Available Games</h2>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <SkeletonLoader variant="games" count={6} />
      </div>
    </div>
  ),
};

/**
 * Upload queue interface
 */
export const UploadInterface: Story = {
  render: () => (
    <div className="max-w-2xl">
      <h2 className="text-xl font-bold mb-4">Upload Queue</h2>
      <SkeletonLoader variant="uploadQueue" count={3} />

      <div className="mt-6">
        <h3 className="font-semibold mb-3">Processing</h3>
        <SkeletonLoader variant="processingProgress" count={1} />
      </div>
    </div>
  ),
};

/**
 * All variants showcase
 */
export const AllVariants: Story = {
  render: () => (
    <div className="space-y-8 max-w-4xl">
      <div>
        <h3 className="font-semibold mb-2">Games</h3>
        <SkeletonLoader variant="games" count={1} />
      </div>

      <div>
        <h3 className="font-semibold mb-2">Agents</h3>
        <SkeletonLoader variant="agents" count={1} />
      </div>

      <div>
        <h3 className="font-semibold mb-2">Message</h3>
        <SkeletonLoader variant="message" count={1} />
      </div>

      <div>
        <h3 className="font-semibold mb-2">Chat History</h3>
        <SkeletonLoader variant="chatHistory" count={2} />
      </div>

      <div>
        <h3 className="font-semibold mb-2">Upload Queue</h3>
        <SkeletonLoader variant="uploadQueue" count={1} />
      </div>

      <div>
        <h3 className="font-semibold mb-2">Processing Progress</h3>
        <SkeletonLoader variant="processingProgress" count={1} />
      </div>

      <div>
        <h3 className="font-semibold mb-2">Game Selection</h3>
        <SkeletonLoader variant="gameSelection" count={1} />
      </div>
    </div>
  ),
};

/**
 * Dark mode example
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
  args: {
    variant: 'message',
    count: 3,
  },
};
