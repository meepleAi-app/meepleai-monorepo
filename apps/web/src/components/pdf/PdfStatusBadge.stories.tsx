/**
 * PdfStatusBadge Stories (Issue #4217)
 * Showcases all 7 PDF states with variants
 */

import type { Meta, StoryObj } from '@storybook/react';

import { PdfStatusBadge } from './PdfStatusBadge';

const meta: Meta<typeof PdfStatusBadge> = {
  title: 'Components/PDF/PdfStatusBadge',
  component: PdfStatusBadge,
  tags: ['autodocs'],
  argTypes: {
    state: {
      control: 'select',
      options: ['pending', 'uploading', 'extracting', 'chunking', 'embedding', 'indexing', 'ready', 'failed'],
      description: 'Current PDF processing state',
    },
    variant: {
      control: 'radio',
      options: ['default', 'compact'],
      description: 'Size variant',
    },
    showIcon: {
      control: 'boolean',
      description: 'Show icon before label',
    },
  },
};

export default meta;
type Story = StoryObj<typeof PdfStatusBadge>;

// ============================================================================
// All States - Default Variant
// ============================================================================

export const AllStatesDefault: Story = {
  name: 'All States (Default)',
  render: () => (
    <div className="flex flex-wrap gap-3">
      <PdfStatusBadge state="pending" />
      <PdfStatusBadge state="uploading" />
      <PdfStatusBadge state="extracting" />
      <PdfStatusBadge state="chunking" />
      <PdfStatusBadge state="embedding" />
      <PdfStatusBadge state="indexing" />
      <PdfStatusBadge state="ready" />
      <PdfStatusBadge state="failed" />
    </div>
  ),
};

// ============================================================================
// All States - Compact Variant
// ============================================================================

export const AllStatesCompact: Story = {
  name: 'All States (Compact)',
  render: () => (
    <div className="flex flex-wrap gap-2">
      <PdfStatusBadge state="pending" variant="compact" />
      <PdfStatusBadge state="uploading" variant="compact" />
      <PdfStatusBadge state="extracting" variant="compact" />
      <PdfStatusBadge state="chunking" variant="compact" />
      <PdfStatusBadge state="embedding" variant="compact" />
      <PdfStatusBadge state="indexing" variant="compact" />
      <PdfStatusBadge state="ready" variant="compact" />
      <PdfStatusBadge state="failed" variant="compact" />
    </div>
  ),
};

// ============================================================================
// Without Icons
// ============================================================================

export const WithoutIcons: Story = {
  name: 'All States (No Icons)',
  render: () => (
    <div className="flex flex-wrap gap-3">
      <PdfStatusBadge state="pending" showIcon={false} />
      <PdfStatusBadge state="uploading" showIcon={false} />
      <PdfStatusBadge state="extracting" showIcon={false} />
      <PdfStatusBadge state="chunking" showIcon={false} />
      <PdfStatusBadge state="embedding" showIcon={false} />
      <PdfStatusBadge state="indexing" showIcon={false} />
      <PdfStatusBadge state="ready" showIcon={false} />
      <PdfStatusBadge state="failed" showIcon={false} />
    </div>
  ),
};

// ============================================================================
// Individual State Examples
// ============================================================================

export const Pending: Story = {
  args: {
    state: 'pending',
    variant: 'default',
    showIcon: true,
  },
};

export const Uploading: Story = {
  args: {
    state: 'uploading',
    variant: 'default',
    showIcon: true,
  },
};

export const Extracting: Story = {
  args: {
    state: 'extracting',
    variant: 'default',
    showIcon: true,
  },
};

export const Chunking: Story = {
  args: {
    state: 'chunking',
    variant: 'default',
    showIcon: true,
  },
};

export const Embedding: Story = {
  args: {
    state: 'embedding',
    variant: 'default',
    showIcon: true,
  },
};

export const Indexing: Story = {
  args: {
    state: 'indexing',
    variant: 'default',
    showIcon: true,
  },
};

export const Ready: Story = {
  args: {
    state: 'ready',
    variant: 'default',
    showIcon: true,
  },
};

export const Failed: Story = {
  args: {
    state: 'failed',
    variant: 'default',
    showIcon: true,
  },
};

// ============================================================================
// Dark Mode Preview
// ============================================================================

export const DarkMode: Story = {
  name: 'All States (Dark Mode)',
  render: () => (
    <div className="dark bg-gray-900 p-6 rounded-lg">
      <div className="flex flex-wrap gap-3">
        <PdfStatusBadge state="pending" />
        <PdfStatusBadge state="uploading" />
        <PdfStatusBadge state="extracting" />
        <PdfStatusBadge state="chunking" />
        <PdfStatusBadge state="embedding" />
        <PdfStatusBadge state="indexing" />
        <PdfStatusBadge state="ready" />
        <PdfStatusBadge state="failed" />
      </div>
    </div>
  ),
};

// ============================================================================
// Usage in List Context
// ============================================================================

export const InListContext: Story = {
  name: 'In Document List',
  render: () => (
    <div className="border rounded-lg p-4 space-y-3">
      <div className="flex items-center justify-between">
        <span className="text-sm">Catan Rulebook.pdf</span>
        <PdfStatusBadge state="ready" variant="compact" />
      </div>
      <div className="flex items-center justify-between">
        <span className="text-sm">Catan Expansion.pdf</span>
        <PdfStatusBadge state="embedding" variant="compact" />
      </div>
      <div className="flex items-center justify-between">
        <span className="text-sm">Catan FAQ.pdf</span>
        <PdfStatusBadge state="failed" variant="compact" />
      </div>
      <div className="flex items-center justify-between">
        <span className="text-sm">Catan Strategy Guide.pdf</span>
        <PdfStatusBadge state="pending" variant="compact" />
      </div>
    </div>
  ),
};
