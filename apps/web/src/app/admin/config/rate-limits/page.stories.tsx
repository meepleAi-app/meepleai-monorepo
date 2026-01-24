import type { Meta, StoryObj } from '@storybook/react';
import { RateLimitConfigClient } from './client';

const meta: Meta<typeof RateLimitConfigClient> = {
  title: 'Admin/Config/RateLimitConfigPage',
  component: RateLimitConfigClient,
  parameters: {
    layout: 'fullscreen',
  },
};

export default meta;

type Story = StoryObj<typeof RateLimitConfigClient>;

export const Default: Story = {};

export const Loading: Story = {
  parameters: {
    mockData: {
      loading: true,
    },
  },
};

export const WithOverrides: Story = {
  parameters: {
    mockData: {
      hasOverrides: true,
    },
  },
};

export const EmptyState: Story = {
  parameters: {
    mockData: {
      hasOverrides: false,
    },
  },
};
