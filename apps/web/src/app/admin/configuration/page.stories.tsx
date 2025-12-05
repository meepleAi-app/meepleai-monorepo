import type { Meta, StoryObj } from '@storybook/react';
import { AdminPageClient } from './client';

/**
 * Admin Configuration Management
 *
 * ## Features
 * - **Tab-based UI**: Feature Flags, Rate Limiting, AI/LLM, RAG
 * - **Real-time Updates**: Configuration changes applied immediately
 * - **Feature Flag Management**: Enable/disable features at runtime
 * - **Export/Import**: Configuration backup and restore
 * - **Validation**: Confirmation dialogs for destructive changes
 */
const meta = {
  title: 'Admin/Configuration',
  component: AdminPageClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024],
      modes: {
        light: {},
        dark: {},
      },
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof AdminPageClient>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock configurations
const mockConfigurations = [
  {
    id: '1',
    key: 'FeatureFlags.ChatEnabled',
    value: 'true',
    category: 'FeatureFlags',
    description: 'Enable chat functionality',
    updatedAt: '2025-12-05T10:00:00Z',
    updatedBy: 'admin@meepleai.com',
  },
  {
    id: '2',
    key: 'RateLimit.ApiRequestsPerMinute',
    value: '100',
    category: 'RateLimit',
    description: 'API requests per minute limit',
    updatedAt: '2025-12-05T09:00:00Z',
    updatedBy: 'admin@meepleai.com',
  },
  {
    id: '3',
    key: 'AI.ModelName',
    value: 'gpt-4',
    category: 'AI',
    description: 'Default AI model',
    updatedAt: '2025-12-05T08:00:00Z',
    updatedBy: 'admin@meepleai.com',
  },
  {
    id: '4',
    key: 'RAG.ChunkSize',
    value: '500',
    category: 'RAG',
    description: 'Text chunk size for vector embedding',
    updatedAt: '2025-12-05T07:00:00Z',
    updatedBy: 'admin@meepleai.com',
  },
];

/**
 * Default configuration panel with data loaded.
 */
export const Default: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/configuration',
        method: 'GET',
        status: 200,
        response: mockConfigurations,
      },
      {
        url: '/api/v1/admin/configuration/categories',
        method: 'GET',
        status: 200,
        response: ['FeatureFlags', 'RateLimit', 'AI', 'RAG'],
      },
    ],
  },
};

/**
 * Loading state while fetching configuration data.
 */
export const Loading: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/configuration',
        method: 'GET',
        delay: 'infinite',
      },
      {
        url: '/api/v1/admin/configuration/categories',
        method: 'GET',
        delay: 'infinite',
      },
    ],
  },
};

/**
 * Error state when data fetch fails.
 */
export const Error: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/configuration',
        method: 'GET',
        status: 500,
        response: { message: 'Internal server error' },
      },
      {
        url: '/api/v1/admin/configuration/categories',
        method: 'GET',
        status: 500,
        response: { message: 'Internal server error' },
      },
    ],
  },
};

/**
 * Empty state with no configurations.
 */
export const Empty: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/admin/configuration',
        method: 'GET',
        status: 200,
        response: [],
      },
      {
        url: '/api/v1/admin/configuration/categories',
        method: 'GET',
        status: 200,
        response: [],
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
        url: '/api/v1/admin/configuration',
        method: 'GET',
        status: 200,
        response: mockConfigurations,
      },
      {
        url: '/api/v1/admin/configuration/categories',
        method: 'GET',
        status: 200,
        response: ['FeatureFlags', 'RateLimit', 'AI', 'RAG'],
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
        url: '/api/v1/admin/configuration',
        method: 'GET',
        status: 200,
        response: mockConfigurations,
      },
      {
        url: '/api/v1/admin/configuration/categories',
        method: 'GET',
        status: 200,
        response: ['FeatureFlags', 'RateLimit', 'AI', 'RAG'],
      },
    ],
  },
};

/**
 * Desktop viewport (1024px).
 */
export const Desktop: Story = {
  parameters: {
    viewport: { defaultViewport: 'desktop' },
    chromatic: { viewports: [1024] },
    mockData: [
      {
        url: '/api/v1/admin/configuration',
        method: 'GET',
        status: 200,
        response: mockConfigurations,
      },
      {
        url: '/api/v1/admin/configuration/categories',
        method: 'GET',
        status: 200,
        response: ['FeatureFlags', 'RateLimit', 'AI', 'RAG'],
      },
    ],
  },
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
    mockData: [
      {
        url: '/api/v1/admin/configuration',
        method: 'GET',
        status: 200,
        response: mockConfigurations,
      },
      {
        url: '/api/v1/admin/configuration/categories',
        method: 'GET',
        status: 200,
        response: ['FeatureFlags', 'RateLimit', 'AI', 'RAG'],
      },
    ],
  },
  decorators: [
    Story => (
      <div className="dark min-h-screen bg-background">
        <Story />
      </div>
    ),
  ],
};
