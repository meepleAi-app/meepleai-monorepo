import { DocumentBadge } from './DocumentBadge';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * DocumentBadge displays color-coded badges for document types.
 * Issue #2051: Multi-document upload visual components
 */
const meta: Meta<typeof DocumentBadge> = {
  title: 'Documents/DocumentBadge',
  component: DocumentBadge,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    type: {
      control: 'select',
      options: ['base', 'expansion', 'errata', 'homerule'],
      description: 'Document type determines badge color and label',
    },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

export const Base: Story = {
  args: {
    type: 'base',
  },
};

export const Expansion: Story = {
  args: {
    type: 'expansion',
  },
};

export const Errata: Story = {
  args: {
    type: 'errata',
  },
};

export const Homerule: Story = {
  args: {
    type: 'homerule',
  },
};

export const AllTypes: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2">
      <DocumentBadge type="base" />
      <DocumentBadge type="expansion" />
      <DocumentBadge type="errata" />
      <DocumentBadge type="homerule" />
    </div>
  ),
};
