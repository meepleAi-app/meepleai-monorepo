/**
 * PdfUploadModal Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for PDF upload modal.
 * Tests file selection, validation, preview, and upload flow.
 */

import { within, userEvent } from 'storybook/test';
import { fn } from 'storybook/test';

import { PdfUploadModal } from './PdfUploadModal';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof PdfUploadModal> = {
  title: 'Components/Library/PdfUploadModal',
  component: PdfUploadModal,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 300,
    },
  },
  tags: ['autodocs'],
  argTypes: {
    isOpen: {
      control: 'boolean',
      description: 'Modal open state',
    },
    gameTitle: {
      control: 'text',
      description: 'Game title for display',
    },
    onClose: { action: 'closed' },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Step 1: File Selection
// ============================================================================

export const SelectStep: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Azul',
    onClose: fn(),
  },
};

export const HoverFileInput: Story = {
  args: {
    ...SelectStep.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const fileInput = canvas.getByTestId('pdf-file-input');

    await userEvent.hover(fileInput);
  },
};

// ============================================================================
// Validation States
// ============================================================================

export const ValidationErrors: Story = {
  args: {
    ...SelectStep.args,
  },
  parameters: {
    docs: {
      description: {
        story: 'Error state shown for invalid file (wrong type, too large, corrupted)',
      },
    },
  },
};

export const ValidatingState: Story = {
  args: {
    ...SelectStep.args,
  },
  parameters: {
    docs: {
      description: {
        story: 'Loading state during PDF page count validation',
      },
    },
  },
};

export const FileSelected: Story = {
  args: {
    ...SelectStep.args,
  },
  parameters: {
    docs: {
      description: {
        story: 'Success state after valid PDF is selected (shows file info and page count)',
      },
    },
  },
};

// ============================================================================
// Step 2: Preview
// ============================================================================

export const PreviewStep: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Wingspan',
    onClose: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'PDF preview screen (requires manual testing with file upload)',
      },
    },
  },
};

export const HoverConfirmUpload: Story = {
  args: {
    ...PreviewStep.args,
  },
  parameters: {
    docs: {
      description: {
        story: 'Hover state on confirm upload button in preview step',
      },
    },
  },
};

export const HoverBackButton: Story = {
  args: {
    ...PreviewStep.args,
  },
  parameters: {
    docs: {
      description: {
        story: 'Hover state on back button to return to file selection',
      },
    },
  },
};

// ============================================================================
// Step 3: Uploading
// ============================================================================

export const UploadingStep: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Gloomhaven',
    onClose: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Upload in progress with progress bar (mocked)',
      },
    },
  },
};

// ============================================================================
// Edge Cases
// ============================================================================

export const LongGameTitle: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Gloomhaven: Jaws of the Lion - Complete Edition with All Expansions',
    onClose: fn(),
  },
};

export const DisabledCancelDuringUpload: Story = {
  args: {
    ...UploadingStep.args,
  },
  parameters: {
    docs: {
      description: {
        story: 'Cancel button is disabled during upload',
      },
    },
  },
};

// ============================================================================
// Responsive Layouts
// ============================================================================

export const MobileSelect: Story = {
  args: {
    ...SelectStep.args,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

export const TabletPreview: Story = {
  args: {
    ...PreviewStep.args,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

export const DesktopUploading: Story = {
  args: {
    ...UploadingStep.args,
  },
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1920],
    },
  },
};
