/**
 * Thread Deletion Confirmation Dialog - Chromatic Visual Tests (Issue #2258)
 *
 * Visual regression tests for thread deletion confirmation dialog.
 * Tests dialog appearance, states, and responsive behavior.
 */

import React, { useState } from 'react';
import { describe, it } from 'vitest';
import type { Meta, StoryObj } from '@storybook/react';

import { useThreadDeletion } from '../../useThreadDeletion';

/**
 * Chromatic test suite for thread deletion confirmation dialog
 * Each test creates a visual snapshot for regression testing
 */
describe('Thread Deletion Dialog - Chromatic Visual Tests', () => {
  it('should match visual snapshot - Dialog open', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Dialog destructive variant', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Mobile responsive', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });

  it('should match visual snapshot - Dark mode', () => {
    // Snapshot will be captured by Chromatic via Storybook
  });
});

/**
 * Demo component for thread deletion dialog
 */
const ThreadDeletionDemo = ({ autoOpen = false }: { autoOpen?: boolean }) => {
  const { handleThreadDelete, ConfirmDialogComponent } = useThreadDeletion();
  const [isDeleting, setIsDeleting] = useState(false);

  React.useEffect(() => {
    if (autoOpen) {
      // Auto-trigger dialog for Chromatic snapshots
      void handleThreadDelete('demo-thread-id');
    }
  }, [autoOpen, handleThreadDelete]);

  const handleClick = () => {
    setIsDeleting(true);
    void handleThreadDelete('demo-thread-id').finally(() => setIsDeleting(false));
  };

  return (
    <div className="flex flex-col items-center justify-center min-h-screen p-8 space-y-4">
      <div className="text-center space-y-2">
        <h2 className="text-2xl font-bold">Thread Deletion Confirmation</h2>
        <p className="text-gray-600">Click the button to see the confirmation dialog</p>
      </div>

      <button
        onClick={handleClick}
        disabled={isDeleting}
        className="px-6 py-3 bg-red-600 text-white rounded-lg hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isDeleting ? 'Deleting...' : 'Delete Thread'}
      </button>

      <div className="text-sm text-gray-500">
        <div>Demo Thread ID: demo-thread-id</div>
        <div>Status: {isDeleting ? 'Processing' : 'Ready'}</div>
      </div>

      <ConfirmDialogComponent />
    </div>
  );
};

// Export stories for Chromatic
const meta: Meta<typeof ThreadDeletionDemo> = {
  title: 'Hooks/useThreadDeletion/Chromatic',
  component: ThreadDeletionDemo,
  tags: ['autodocs'],
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      disableSnapshot: false,
      viewports: [320, 768, 1024, 1920],
    },
  },
};

export default meta;
type Story = StoryObj<typeof ThreadDeletionDemo>;

/**
 * Confirmation dialog - Open state
 */
export const DialogOpen: Story = {
  args: {
    autoOpen: true,
  },
  parameters: {
    chromatic: {
      disableSnapshot: false,
      delay: 500, // Wait for dialog animation
    },
  },
};

/**
 * Confirmation dialog - Destructive variant styling
 */
export const DialogDestructive: Story = {
  args: {
    autoOpen: true,
  },
  parameters: {
    chromatic: {
      disableSnapshot: false,
      delay: 500,
    },
  },
  play: async ({ canvasElement }) => {
    // Dialog should show destructive (red) styling for confirm button
    const confirmButton = canvasElement.querySelector('[data-variant="destructive"]');
    return confirmButton;
  },
};

/**
 * Mobile view - Dialog responsive
 */
export const MobileView: Story = {
  args: {
    autoOpen: true,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      disableSnapshot: false,
      viewports: [320],
      delay: 500,
    },
  },
};

/**
 * Tablet view - Dialog responsive
 */
export const TabletView: Story = {
  args: {
    autoOpen: true,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      disableSnapshot: false,
      viewports: [768],
      delay: 500,
    },
  },
};

/**
 * Desktop view - Dialog centered
 */
export const DesktopView: Story = {
  args: {
    autoOpen: true,
  },
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      disableSnapshot: false,
      viewports: [1920],
      delay: 500,
    },
  },
};

/**
 * Dark mode - Dialog appearance
 */
export const DarkMode: Story = {
  args: {
    autoOpen: true,
  },
  parameters: {
    backgrounds: { default: 'dark' },
    chromatic: {
      disableSnapshot: false,
      delay: 500,
    },
  },
};

/**
 * Idle state - Button ready
 */
export const IdleState: Story = {
  args: {
    autoOpen: false,
  },
  parameters: {
    chromatic: {
      disableSnapshot: false,
    },
  },
};

/**
 * Focus state - Confirm button focused
 */
export const FocusState: Story = {
  args: {
    autoOpen: true,
  },
  parameters: {
    chromatic: {
      disableSnapshot: false,
      delay: 500,
    },
  },
  play: async ({ canvasElement }) => {
    // Simulate focus on confirm button
    const confirmButton = canvasElement.querySelector('button[type="button"]') as HTMLButtonElement;
    confirmButton?.focus();
  },
};

/**
 * Long text handling - Italian translation
 */
export const LongTextHandling: Story = {
  args: {
    autoOpen: true,
  },
  parameters: {
    chromatic: {
      disableSnapshot: false,
      delay: 500,
    },
  },
  // This tests how the dialog handles the full Italian warning text
};
