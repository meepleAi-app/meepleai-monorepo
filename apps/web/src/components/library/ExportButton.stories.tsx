/**
 * ExportButton Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for library export button.
 * Tests dropdown menu, format/scope options, and loading states.
 */

import { within, userEvent } from 'storybook/test';

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import { ExportButton } from './ExportButton';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof ExportButton> = {
  title: 'Components/Library/ExportButton',
  component: ExportButton,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 200,
    },
  },
  tags: ['autodocs'],
  argTypes: {
    data: {
      control: 'object',
      description: 'Library data to export',
    },
    filteredCount: {
      control: 'number',
      description: 'Number of filtered items',
    },
    totalCount: {
      control: 'number',
      description: 'Total items in library',
    },
    showAdvanced: {
      control: 'boolean',
      description: 'Show dropdown with advanced options',
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state',
    },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// Mock library data
const mockLibraryData: UserLibraryEntry[] = [
  {
    id: '123e4567-e89b-12d3-a456-426614174000',
    userId: '789e4567-e89b-12d3-a456-426614174000',
    gameId: '456e4567-e89b-12d3-a456-426614174000',
    gameTitle: 'Azul',
    gamePublisher: 'Plan B Games',
    gameYearPublished: 2017,
    gameImageUrl: 'https://example.com/azul.png',
    addedAt: '2024-01-15T10:00:00Z',
    notes: 'Great game!',
    isFavorite: true,
    currentState: 'Owned',
    stateChangedAt: '2024-01-15T10:00:00Z',
    hasPdfDocuments: true,
  },
  {
    id: '223e4567-e89b-12d3-a456-426614174001',
    userId: '789e4567-e89b-12d3-a456-426614174000',
    gameId: '556e4567-e89b-12d3-a456-426614174001',
    gameTitle: 'Wingspan',
    gamePublisher: 'Stonemaier Games',
    gameYearPublished: 2019,
    gameImageUrl: 'https://example.com/wingspan.png',
    addedAt: '2024-01-16T10:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: 'Wishlist',
    stateChangedAt: '2024-01-16T10:00:00Z',
    hasPdfDocuments: false,
  },
];

// ============================================================================
// Default States
// ============================================================================

export const QuickExport: Story = {
  args: {
    data: mockLibraryData,
    showAdvanced: false,
  },
};

export const WithDropdown: Story = {
  args: {
    data: mockLibraryData,
    showAdvanced: true,
    totalCount: mockLibraryData.length,
  },
};

export const WithFilteredCount: Story = {
  args: {
    data: mockLibraryData,
    showAdvanced: true,
    filteredCount: 2,
    totalCount: 10,
  },
};

export const EmptyData: Story = {
  args: {
    data: [],
    showAdvanced: true,
    totalCount: 0,
  },
};

// ============================================================================
// Interaction States
// ============================================================================

export const DropdownOpen: Story = {
  args: {
    ...WithDropdown.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button', { name: /Esporta/i });

    await userEvent.click(button);
  },
};

export const HoverCSVBase: Story = {
  args: {
    ...WithDropdown.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button', { name: /Esporta/i });
    await userEvent.click(button);

    const csvBaseOption = canvas.getByText('CSV - Base');
    await userEvent.hover(csvBaseOption);
  },
};

export const HoverJSONFull: Story = {
  args: {
    ...WithDropdown.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button', { name: /Esporta/i });
    await userEvent.click(button);

    const jsonFullOption = canvas.getByText('JSON - Completo');
    await userEvent.hover(jsonFullOption);
  },
};

export const HoverQuickExport: Story = {
  args: {
    ...QuickExport.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button', { name: /Esporta CSV/i });

    await userEvent.hover(button);
  },
};

// ============================================================================
// Loading State
// ============================================================================

export const ExportingState: Story = {
  args: {
    ...WithDropdown.args,
  },
  parameters: {
    docs: {
      description: {
        story: 'Loading spinner shown during export operation (mocked)',
      },
    },
  },
};

// ============================================================================
// Disabled State
// ============================================================================

export const Disabled: Story = {
  args: {
    data: mockLibraryData,
    showAdvanced: true,
    disabled: true,
    totalCount: mockLibraryData.length,
  },
};

export const DisabledQuick: Story = {
  args: {
    data: mockLibraryData,
    showAdvanced: false,
    disabled: true,
  },
};

// ============================================================================
// Count Variations
// ============================================================================

export const LargeLibrary: Story = {
  args: {
    data: mockLibraryData,
    showAdvanced: true,
    totalCount: 156,
  },
};

export const PartialFilter: Story = {
  args: {
    data: mockLibraryData,
    showAdvanced: true,
    filteredCount: 15,
    totalCount: 50,
  },
};

export const AllFiltered: Story = {
  args: {
    data: mockLibraryData,
    showAdvanced: true,
    filteredCount: 50,
    totalCount: 50,
  },
};

// ============================================================================
// Responsive Preview
// ============================================================================

export const ButtonSizes: Story = {
  render: () => (
    <div className="flex flex-col gap-4">
      <ExportButton data={mockLibraryData} showAdvanced={false} />
      <ExportButton data={mockLibraryData} showAdvanced={true} totalCount={2} />
    </div>
  ),
};
