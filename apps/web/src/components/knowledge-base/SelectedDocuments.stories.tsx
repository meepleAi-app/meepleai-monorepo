/**
 * SelectedDocuments Stories - Issue #2416
 *
 * Storybook stories demonstrating various states, configurations, and drag-and-drop.
 */

import { useState } from 'react';

import { fn } from 'storybook/test';

import {
  mockSelectedAtLimit,
  mockSelectedEmpty,
  mockSelectedMedium,
  mockSelectedMixedTypes,
  mockSelectedNearLimit,
  mockSelectedSingle,
  mockSelectedSingleGame,
  mockSelectedSmall,
  mockSelectedVariedTags,
} from '@/__tests__/fixtures/mockSelectedDocuments';

import { SelectedDocuments, type SelectedDocument } from './SelectedDocuments';

import type { Meta, StoryObj } from '@storybook/react';

// ========== Meta Configuration ==========

const meta = {
  title: 'Knowledge Base/SelectedDocuments',
  component: SelectedDocuments,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component: `
Displays selected documents with removal and drag-and-drop reordering.

**Features:**
- ✅ Removable badges (X button)
- ✅ Drag-and-drop reorder (grip handle)
- ✅ Max documents limit with alert
- ✅ Selection statistics (by type, by game)
- ✅ Empty state
- ✅ Loading state
- ✅ Disabled state
- ✅ Keyboard accessible drag-and-drop

**Accessibility:**
- Keyboard: Tab to drag handle, Space/Enter to pick up, Arrow keys to move, Space/Enter to drop
- Screen reader: Announces drag state and position

**Issue:** #2416
        `,
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    documents: {
      control: false,
      description: 'Currently selected documents in display order',
    },
    onDocumentsChange: {
      action: 'documentsChanged',
      description: 'Callback when documents are reordered or removed',
    },
    maxDocuments: {
      control: 'number',
      description: 'Maximum number of documents allowed (default: 50)',
    },
    isLoading: {
      control: 'boolean',
      description: 'Whether the component is in loading state',
    },
    disabled: {
      control: 'boolean',
      description: 'Whether the component is disabled',
    },
  },
} satisfies Meta<typeof SelectedDocuments>;

export default meta;
type Story = StoryObj<typeof meta>;

// ========== Interactive Wrapper ==========

function InteractiveSelectedDocuments(props: React.ComponentProps<typeof SelectedDocuments>) {
  const [documents, setDocuments] = useState<SelectedDocument[]>(props.documents);

  return (
    <SelectedDocuments
      {...props}
      documents={documents}
      onDocumentsChange={docs => {
        setDocuments(docs);
        props.onDocumentsChange?.(docs);
      }}
    />
  );
}

// ========== Stories ==========

/**
 * Empty state when no documents are selected
 */
export const Empty: Story = {
  args: {
    documents: mockSelectedEmpty,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Single document selected
 */
export const Single: Story = {
  args: {
    documents: mockSelectedSingle,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Small selection (5 documents)
 */
export const Small: Story = {
  args: {
    documents: mockSelectedSmall,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Medium selection (15 documents) - good for testing scroll
 */
export const Medium: Story = {
  args: {
    documents: mockSelectedMedium,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Mixed document types - tests statistics badges
 */
export const MixedTypes: Story = {
  args: {
    documents: mockSelectedMixedTypes,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Single game documents - tests grouping scenario
 */
export const SingleGame: Story = {
  args: {
    documents: mockSelectedSingleGame,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Varied tags - tests tag display overflow
 */
export const VariedTags: Story = {
  args: {
    documents: mockSelectedVariedTags,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Near limit (45/50) - shows warning alert
 */
export const NearLimit: Story = {
  args: {
    documents: mockSelectedNearLimit,
    maxDocuments: 50,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * At limit (50/50) - shows error alert
 */
export const AtLimit: Story = {
  args: {
    documents: mockSelectedAtLimit,
    maxDocuments: 50,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Custom max limit (10 documents)
 */
export const CustomMaxLimit: Story = {
  args: {
    documents: mockSelectedSmall,
    maxDocuments: 10,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Loading state with spinner
 */
export const Loading: Story = {
  args: {
    documents: mockSelectedSmall,
    isLoading: true,
    disabled: false,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Disabled state (no interactions)
 */
export const Disabled: Story = {
  args: {
    documents: mockSelectedSmall,
    isLoading: false,
    disabled: true,
    onDocumentsChange: fn(),
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Mobile viewport (responsive design)
 */
export const Mobile: Story = {
  args: {
    documents: mockSelectedSmall,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};

/**
 * Dark mode theme
 */
export const DarkMode: Story = {
  args: {
    documents: mockSelectedSmall,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  render: args => (
    <div className="dark">
      <InteractiveSelectedDocuments {...args} />
    </div>
  ),
};

/**
 * Interactive demo - drag and reorder documents
 *
 * Instructions:
 * 1. Click and drag the grip handle (6 dots) to reorder
 * 2. Click X to remove documents
 * 3. Click "Clear All" to remove all documents
 */
export const InteractiveDemo: Story = {
  args: {
    documents: mockSelectedSmall,
    isLoading: false,
    disabled: false,
    onDocumentsChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: `
**Try it out:**
- **Drag to reorder**: Click and hold the grip handle (⋮⋮), then drag up or down
- **Remove document**: Click the X button on any document
- **Clear all**: Click the "Clear All" button at the bottom
- **Keyboard**: Tab to grip handle, press Space to pick up, use Arrow keys to move, press Space to drop
        `,
      },
    },
  },
  render: args => <InteractiveSelectedDocuments {...args} />,
};
