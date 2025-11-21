/**
 * ConfirmDialog - Storybook stories
 * Issue #1435 - Replace window.confirm with custom dialog
 */

import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { ConfirmDialog } from './confirm-dialog';
import { useConfirmDialog } from '@/hooks/useConfirmDialog';
import { Button } from './button';

const meta: Meta<typeof ConfirmDialog> = {
  title: 'UI/ConfirmDialog',
  component: ConfirmDialog,
  tags: ['autodocs'],
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: `
ConfirmDialog is a custom confirmation dialog that replaces \`window.confirm()\`.

## Features
- 🎨 Customizable title, message, and button text
- ⚠️ Destructive variant with warning icon
- ✅ Fully testable (no browser APIs)
- 🌐 SSR-safe
- ♿ Accessible (keyboard navigation, screen readers)

## Usage
Use the \`useConfirmDialog\` hook for a Promise-based API:

\`\`\`tsx
const { confirm, ConfirmDialogComponent } = useConfirmDialog();

const handleDelete = async () => {
  const confirmed = await confirm({
    title: "Delete item?",
    message: "This action cannot be undone.",
    variant: "destructive"
  });

  if (confirmed) {
    // Proceed with deletion
  }
}

return (
  <>
    <button onClick={handleDelete}>Delete</button>
    <ConfirmDialogComponent />
  </>
);
\`\`\`
        `,
      },
    },
  },
  argTypes: {
    open: {
      control: 'boolean',
      description: 'Whether the dialog is open',
    },
    title: {
      control: 'text',
      description: 'Dialog title',
    },
    message: {
      control: 'text',
      description: 'Dialog message/description',
    },
    variant: {
      control: 'radio',
      options: ['default', 'destructive'],
      description: 'Visual variant of the dialog',
    },
    confirmText: {
      control: 'text',
      description: 'Confirm button text',
    },
    cancelText: {
      control: 'text',
      description: 'Cancel button text',
    },
  },
};

export default meta;
type Story = StoryObj<typeof ConfirmDialog>;

/**
 * Default confirmation dialog with standard styling
 */
export const Default: Story = {
  args: {
    open: true,
    title: 'Confirm Action',
    message: 'Are you sure you want to proceed with this action?',
    confirmText: 'Confirm',
    cancelText: 'Cancel',
    variant: 'default',
    onConfirm: () => console.log('Confirmed'),
    onCancel: () => console.log('Cancelled'),
    onOpenChange: (open) => console.log('Open changed:', open),
  },
};

/**
 * Destructive variant with warning icon and red confirm button
 */
export const Destructive: Story = {
  args: {
    open: true,
    title: 'Delete Item',
    message: 'This action cannot be undone. Are you sure you want to permanently delete this item?',
    confirmText: 'Delete',
    cancelText: 'Cancel',
    variant: 'destructive',
    onConfirm: () => console.log('Deleted'),
    onCancel: () => console.log('Cancelled'),
    onOpenChange: (open) => console.log('Open changed:', open),
  },
};

/**
 * Custom button text for specific actions
 */
export const CustomButtonText: Story = {
  args: {
    open: true,
    title: 'Logout',
    message: 'Do you want to logout from your account?',
    confirmText: 'Yes, Logout',
    cancelText: 'Stay Logged In',
    variant: 'default',
    onConfirm: () => console.log('Logged out'),
    onCancel: () => console.log('Stayed'),
    onOpenChange: (open) => console.log('Open changed:', open),
  },
};

/**
 * Long message text with proper wrapping
 */
export const LongMessage: Story = {
  args: {
    open: true,
    title: 'Warning: Destructive Configuration Change',
    message:
      'Changing this configuration value may require re-indexing all vector documents in the database. This operation may take significant time and could impact system performance. Additionally, all cached data will be invalidated, and users may experience temporary service interruptions. Do you want to continue with this change?',
    confirmText: 'Continue',
    cancelText: 'Cancel',
    variant: 'destructive',
    onConfirm: () => console.log('Confirmed'),
    onCancel: () => console.log('Cancelled'),
    onOpenChange: (open) => console.log('Open changed:', open),
  },
};

/**
 * Interactive example using the hook
 */
export const InteractiveWithHook = () => {
  const { confirm, ConfirmDialogComponent } = useConfirmDialog();
  const [lastAction, setLastAction] = useState<string>('None');

  const handleDefaultConfirm = async () => {
    const confirmed = await confirm({
      title: 'Confirm Action',
      message: 'Are you sure you want to proceed?',
      confirmText: 'Yes',
      cancelText: 'No',
    });
    setLastAction(confirmed ? 'Confirmed (Default)' : 'Cancelled (Default)');
  };

  const handleDestructiveConfirm = async () => {
    const confirmed = await confirm({
      title: 'Delete Item',
      message: 'This action cannot be undone. Are you sure?',
      variant: 'destructive',
      confirmText: 'Delete',
      cancelText: 'Keep',
    });
    setLastAction(confirmed ? 'Deleted' : 'Kept');
  };

  return (
    <div className="space-y-4 p-6">
      <div className="flex flex-col gap-3">
        <Button onClick={handleDefaultConfirm} variant="default">
          Show Default Confirm
        </Button>
        <Button onClick={handleDestructiveConfirm} variant="destructive">
          Show Destructive Confirm
        </Button>
      </div>
      <div className="mt-4 p-4 bg-slate-100 dark:bg-slate-800 rounded-lg">
        <p className="text-sm text-slate-700 dark:text-slate-300">
          <strong>Last Action:</strong> {lastAction}
        </p>
      </div>
      <ConfirmDialogComponent />
    </div>
  );
};

InteractiveWithHook.parameters = {
  docs: {
    description: {
      story: 'Interactive example showing how to use the `useConfirmDialog` hook with different variants.',
    },
  },
};

/**
 * Example with sequential confirmations
 */
export const SequentialConfirmations = () => {
  const { confirm, ConfirmDialogComponent } = useConfirmDialog();
  const [status, setStatus] = useState<string>('Ready');

  const handleSequentialConfirms = async () => {
    setStatus('Step 1: Confirming first action...');
    const first = await confirm({
      title: 'Step 1 of 2',
      message: 'Confirm first action?',
      confirmText: 'Continue',
      cancelText: 'Cancel',
    });

    if (!first) {
      setStatus('Cancelled at step 1');
      return;
    }

    setStatus('Step 2: Confirming second action...');
    const second = await confirm({
      title: 'Step 2 of 2',
      message: 'Confirm final action? This will complete the process.',
      variant: 'destructive',
      confirmText: 'Finish',
      cancelText: 'Cancel',
    });

    setStatus(second ? 'Process completed!' : 'Cancelled at step 2');
  };

  return (
    <div className="space-y-4 p-6">
      <Button onClick={handleSequentialConfirms} variant="default">
        Start Multi-Step Confirmation
      </Button>
      <div className="p-4 bg-slate-100 dark:bg-slate-800 rounded-lg">
        <p className="text-sm text-slate-700 dark:text-slate-300">
          <strong>Status:</strong> {status}
        </p>
      </div>
      <ConfirmDialogComponent />
    </div>
  );
};

SequentialConfirmations.parameters = {
  docs: {
    description: {
      story: 'Example showing how to handle multiple sequential confirmations in a workflow.',
    },
  },
};
