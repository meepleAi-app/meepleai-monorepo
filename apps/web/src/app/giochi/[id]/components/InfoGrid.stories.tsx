/**
 * InfoGrid Storybook Stories
 *
 * Visual regression testing variants for Chromatic.
 * Issue #1841 (PAGE-005)
 */

import type { Meta, StoryObj } from '@storybook/react';
import { InfoGrid } from './InfoGrid';

const meta: Meta<typeof InfoGrid> = {
  title: 'Game Detail/InfoGrid',
  component: InfoGrid,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof InfoGrid>;

// ============================================================================
// Stories
// ============================================================================

export const Default: Story = {
  args: {
    minPlayers: 3,
    maxPlayers: 5,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 90,
    averageWeight: 2.5,
  },
};

export const SimpleGame: Story = {
  args: {
    minPlayers: 2,
    maxPlayers: 4,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 45,
    averageWeight: 1.8, // Light
  },
};

export const ComplexGame: Story = {
  args: {
    minPlayers: 1,
    maxPlayers: 4,
    minPlayTimeMinutes: 120,
    maxPlayTimeMinutes: 180,
    averageWeight: 4.2, // Heavy
  },
};

export const FixedPlayers: Story = {
  args: {
    minPlayers: 4,
    maxPlayers: 4,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 60,
    averageWeight: 3.0,
  },
};

export const NoComplexityData: Story = {
  args: {
    minPlayers: 2,
    maxPlayers: 6,
    minPlayTimeMinutes: 45,
    maxPlayTimeMinutes: 90,
    averageWeight: null,
  },
};

export const MissingData: Story = {
  args: {
    minPlayers: null,
    maxPlayers: null,
    minPlayTimeMinutes: null,
    maxPlayTimeMinutes: null,
    averageWeight: null,
  },
};

// Mobile viewport (1-col)
export const Mobile: Story = {
  args: {
    minPlayers: 2,
    maxPlayers: 5,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 120,
    averageWeight: 3.5,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

// Tablet viewport (2-col)
export const Tablet: Story = {
  args: {
    minPlayers: 1,
    maxPlayers: 4,
    minPlayTimeMinutes: 90,
    maxPlayTimeMinutes: 150,
    averageWeight: 4.0,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};
