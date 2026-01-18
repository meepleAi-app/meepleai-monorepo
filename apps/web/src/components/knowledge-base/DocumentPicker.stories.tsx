/**
 * DocumentPicker Stories - Issue #2415
 *
 * Storybook stories demonstrating various states and configurations.
 */

import { useState } from 'react';

import { fn } from 'storybook/test';

import {
  mockDocumentsEmpty,
  mockDocumentsLarge,
  mockDocumentsMedium,
  mockDocumentsSingle,
  mockDocumentsSmall,
} from '@/__tests__/fixtures/mockDocuments';

import { DocumentPicker } from './DocumentPicker';

import type { Meta, StoryObj } from '@storybook/react';

// ========== Meta Configuration ==========

const meta = {
  title: 'Knowledge Base/DocumentPicker',
  component: DocumentPicker,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component: `
Multi-select document picker with search, pagination, and hover preview.

**Features:**
- ✅ Multi-select with checkboxes
- ✅ Real-time search filter
- ✅ Pagination (>100 documents)
- ✅ Hover preview with metadata
- ✅ Select All / Clear All
- ✅ Max selections limit
- ✅ Empty states
- ✅ Loading states
- ✅ Disabled states

**Issue:** #2415
        `,
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    selectedIds: {
      control: false,
      description: 'Currently selected document IDs',
    },
    availableDocuments: {
      control: false,
      description: 'Available documents to select from',
    },
    onSelectionChange: {
      action: 'selectionChanged',
      description: 'Callback when selection changes',
    },
    isLoading: {
      control: 'boolean',
      description: 'Whether the picker is in loading state',
    },
    disabled: {
      control: 'boolean',
      description: 'Whether the picker is disabled',
    },
    maxSelections: {
      control: 'number',
      description: 'Maximum number of documents that can be selected',
    },
    pageSize: {
      control: 'number',
      description: 'Number of documents per page',
    },
  },
} satisfies Meta<typeof DocumentPicker>;

export default meta;
type Story = StoryObj<typeof meta>;

// ========== Interactive Wrapper ==========

function InteractiveDocumentPicker(props: React.ComponentProps<typeof DocumentPicker>) {
  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  return (
    <DocumentPicker
      {...props}
      selectedIds={selectedIds}
      onSelectionChange={ids => {
        setSelectedIds(ids);
        props.onSelectionChange?.(ids);
      }}
    />
  );
}

// ========== Stories ==========

/**
 * Empty state when no documents are available
 */
export const Empty: Story = {
  args: {
    availableDocuments: mockDocumentsEmpty,
    isLoading: false,
    disabled: false,
    onSelectionChange: fn(),
  },
  render: args => <InteractiveDocumentPicker {...args} />,
};

/**
 * Single document available for selection
 */
export const Single: Story = {
  args: {
    availableDocuments: mockDocumentsSingle,
    isLoading: false,
    disabled: false,
    onSelectionChange: fn(),
  },
  render: args => <InteractiveDocumentPicker {...args} />,
};

/**
 * Small dataset (5 documents)
 */
export const Small: Story = {
  args: {
    availableDocuments: mockDocumentsSmall,
    isLoading: false,
    disabled: false,
    onSelectionChange: fn(),
  },
  render: args => <InteractiveDocumentPicker {...args} />,
};

/**
 * Medium dataset (20 documents - fits on 1 page)
 */
export const Medium: Story = {
  args: {
    availableDocuments: mockDocumentsMedium,
    isLoading: false,
    disabled: false,
    pageSize: 20,
    onSelectionChange: fn(),
  },
  render: args => <InteractiveDocumentPicker {...args} />,
};

/**
 * Large dataset (105 documents - multiple pages)
 * Tests pagination with >100 documents as per requirements
 */
export const Large: Story = {
  args: {
    availableDocuments: mockDocumentsLarge,
    isLoading: false,
    disabled: false,
    pageSize: 20,
    onSelectionChange: fn(),
  },
  render: args => <InteractiveDocumentPicker {...args} />,
};

/**
 * Loading state with skeleton/spinner
 */
export const Loading: Story = {
  args: {
    availableDocuments: mockDocumentsMedium,
    isLoading: true,
    disabled: false,
    onSelectionChange: fn(),
  },
  render: args => <InteractiveDocumentPicker {...args} />,
};

/**
 * Disabled state (all interactions disabled)
 */
export const Disabled: Story = {
  args: {
    availableDocuments: mockDocumentsSmall,
    isLoading: false,
    disabled: true,
    onSelectionChange: fn(),
  },
  render: args => <InteractiveDocumentPicker {...args} />,
};

/**
 * Max selections limit (max 5 documents)
 */
export const MaxSelections: Story = {
  args: {
    availableDocuments: mockDocumentsMedium,
    isLoading: false,
    disabled: false,
    maxSelections: 5,
    onSelectionChange: fn(),
  },
  render: args => <InteractiveDocumentPicker {...args} />,
};

/**
 * Pre-selected documents (5 documents already selected)
 */
export const PreSelected: Story = {
  args: {
    availableDocuments: mockDocumentsMedium,
    isLoading: false,
    disabled: false,
    onSelectionChange: fn(),
  },
  render: args => {
    const [selectedIds, setSelectedIds] = useState<string[]>([
      'doc-000',
      'doc-001',
      'doc-002',
      'doc-003',
      'doc-004',
    ]);

    return (
      <DocumentPicker
        {...args}
        selectedIds={selectedIds}
        onSelectionChange={ids => {
          setSelectedIds(ids);
          args.onSelectionChange?.(ids);
        }}
      />
    );
  },
};

/**
 * Custom page size (10 documents per page)
 */
export const CustomPageSize: Story = {
  args: {
    availableDocuments: mockDocumentsLarge,
    isLoading: false,
    disabled: false,
    pageSize: 10,
    onSelectionChange: fn(),
  },
  render: args => <InteractiveDocumentPicker {...args} />,
};

/**
 * Mobile viewport (responsive design test)
 */
export const Mobile: Story = {
  args: {
    availableDocuments: mockDocumentsSmall,
    isLoading: false,
    disabled: false,
    onSelectionChange: fn(),
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
  render: args => <InteractiveDocumentPicker {...args} />,
};

/**
 * Dark mode theme
 */
export const DarkMode: Story = {
  args: {
    availableDocuments: mockDocumentsSmall,
    isLoading: false,
    disabled: false,
    onSelectionChange: fn(),
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  render: args => (
    <div className="dark">
      <InteractiveDocumentPicker {...args} />
    </div>
  ),
};
