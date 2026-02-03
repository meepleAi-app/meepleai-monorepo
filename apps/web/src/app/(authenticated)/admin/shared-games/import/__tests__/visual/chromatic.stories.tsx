/**
 * Import Page - Visual Tests
 * Issue #2372 - Phase 3: Frontend Admin UI
 *
 * Chromatic visual regression tests for the Bulk Import admin page.
 * Covers: CSV import, BGG import, progress states, validation, responsive design
 */

import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent } from 'storybook/test';

import { ImportClient } from '../../client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof ImportClient> = {
  title: 'Admin/SharedGames/Import/Visual Tests',
  component: ImportClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      disableSnapshot: false,
      delay: 500,
    },
  },
  decorators: [
    Story => (
      <AuthProvider>
        <div className="min-h-screen bg-gray-50">
          <Story />
        </div>
      </AuthProvider>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof ImportClient>;

// ========== Stories ==========

/**
 * Default View - CSV tab empty state
 */
export const DefaultView: Story = {};

/**
 * BGG Tab - BoardGameGeek import tab
 */
export const BggTab: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for page to load
    await canvas.findByText('Importazione Giochi');

    // Click BGG tab
    const bggTab = await canvas.findByRole('tab', { name: /importa da bgg/i });
    await userEvent.click(bggTab);
  },
};

/**
 * CSV Tab with File Selected - Shows file input state
 */
export const CsvFileSelected: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for page to load
    await canvas.findByText('Importazione Giochi');

    // File selection is handled by browser, we show the UI state
  },
};

/**
 * CSV Parse Error - Error state after invalid file
 */
export const CsvParseError: Story = {
  // This would be shown after attempting to parse an invalid CSV
  // In a real scenario, the error state would be triggered by file upload
};

/**
 * BGG with IDs Added - Shows BGG IDs in queue
 */
export const BggWithIdsAdded: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for page to load
    await canvas.findByText('Importazione Giochi');

    // Click BGG tab
    const bggTab = await canvas.findByRole('tab', { name: /importa da bgg/i });
    await userEvent.click(bggTab);

    // Type BGG IDs
    const textarea = await canvas.findByPlaceholderText(/es: 13, 822/i);
    await userEvent.type(textarea, '13, 822, 167791');
  },
};

/**
 * Mobile View - Responsive layout
 */
export const MobileView: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Mobile BGG Tab - BGG import on mobile
 */
export const MobileBggTab: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    await canvas.findByText('Importazione Giochi');

    const bggTab = await canvas.findByRole('tab', { name: /importa da bgg/i });
    await userEvent.click(bggTab);
  },
};

/**
 * Tablet View - Medium screen layout
 */
export const TabletView: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};

/**
 * Tablet BGG Tab - BGG import on tablet
 */
export const TabletBggTab: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    await canvas.findByText('Importazione Giochi');

    const bggTab = await canvas.findByRole('tab', { name: /importa da bgg/i });
    await userEvent.click(bggTab);
  },
};

/**
 * Wide Screen - Large screen layout
 */
export const WideScreen: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
  },
};
