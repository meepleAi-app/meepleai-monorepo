/**
 * ExportButton Storybook Stories
 *
 * Visual documentation and testing for ExportButton component
 * Issue #2139: Testing Dashboard Export Functionality
 */

import type { Meta, StoryObj } from '@storybook/react';
import { fn } from 'storybook/test';
import { ExportButton } from './ExportButton';

const meta = {
  title: 'Admin/ExportButton',
  component: ExportButton,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    isExporting: {
      control: 'boolean',
      description: 'Whether export is currently in progress',
    },
    className: {
      control: 'text',
      description: 'Optional className for button styling',
    },
  },
  args: {
    onExportCSV: fn(),
    onExportPDF: fn(async () => {
      await new Promise(resolve => setTimeout(resolve, 1000));
    }),
  },
} satisfies Meta<typeof ExportButton>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default state of the export button
 */
export const Default: Story = {
  args: {
    isExporting: false,
  },
};

/**
 * Button in loading state during export
 */
export const Exporting: Story = {
  args: {
    isExporting: true,
  },
};

/**
 * Button with custom styling
 */
export const CustomStyle: Story = {
  args: {
    isExporting: false,
    className: 'bg-blue-500 hover:bg-blue-600 text-white',
  },
};

/**
 * Interactive example showing dropdown menu
 */
export const WithDropdown: Story = {
  args: {
    isExporting: false,
  },
  render: args => (
    <div className="p-8">
      <p className="mb-4 text-sm text-muted-foreground">Click the button to see export options</p>
      <ExportButton {...args} />
    </div>
  ),
};
