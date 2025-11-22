/**
 * AlertDialog - Storybook stories
 * Follow-up to Issue #1435 - Replace window.alert with custom dialog
 */

import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { AlertDialog } from './alert-dialog';
import { useAlertDialog } from '@/hooks/useAlertDialog';
import { Button } from './button';

const meta: Meta<typeof AlertDialog> = {
  title: 'UI/AlertDialog',
  component: AlertDialog,
  tags: ['autodocs'],
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: `
AlertDialog is a custom alert dialog that replaces \`window.alert()\`.

## Features
- 🎨 Five variants: info, success, warning, error, loading
- ⚠️ Color-coded icons for each variant (with animated spinner for loading)
- ✅ Fully testable (no browser APIs)
- 🌐 SSR-safe
- ♿ Accessible (keyboard navigation, screen readers, ARIA labels)

## Usage
Use the \`useAlertDialog\` hook for a Promise-based API:

\`\`\`tsx
const { alert, AlertDialogComponent } = useAlertDialog();

const handleSave = async () => {
  try {
    await saveData();
    await alert({
      title: "Success",
      message: "Your changes have been saved.",
      variant: "success"
    });
  } catch (error) {
    await alert({
      title: "Error",
      message: "Failed to save changes.",
      variant: "error"
    });
  }
}

return (
  <>
    <button onClick={handleSave}>Save</button>
    <AlertDialogComponent />
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
      options: ['info', 'success', 'warning', 'error', 'loading'],
      description: 'Visual variant of the dialog',
    },
    buttonText: {
      control: 'text',
      description: 'OK button text',
    },
  },
};

export default meta;
type Story = StoryObj<typeof AlertDialog>;

/**
 * Info variant for informational messages
 */
export const Info: Story = {
  args: {
    open: true,
    title: 'Information',
    message: 'This is an informational message to keep you informed.',
    buttonText: 'OK',
    variant: 'info',
    onClose: () => console.log('Closed'),
    onOpenChange: (open) => console.log('Open changed:', open),
  },
};

/**
 * Success variant for successful operations
 */
export const Success: Story = {
  args: {
    open: true,
    title: 'Success',
    message: 'Your changes have been saved successfully.',
    buttonText: 'Great!',
    variant: 'success',
    onClose: () => console.log('Closed'),
    onOpenChange: (open) => console.log('Open changed:', open),
  },
};

/**
 * Warning variant for cautionary messages
 */
export const Warning: Story = {
  args: {
    open: true,
    title: 'Warning',
    message: 'This action may have unintended consequences. Please review before proceeding.',
    buttonText: 'Understood',
    variant: 'warning',
    onClose: () => console.log('Closed'),
    onOpenChange: (open) => console.log('Open changed:', open),
  },
};

/**
 * Error variant for error messages
 */
export const Error: Story = {
  args: {
    open: true,
    title: 'Error',
    message: 'An unexpected error occurred. Please try again later.',
    buttonText: 'Dismiss',
    variant: 'error',
    onClose: () => console.log('Closed'),
    onOpenChange: (open) => console.log('Open changed:', open),
  },
};

/**
 * Loading variant for long-running operations
 */
export const Loading: Story = {
  args: {
    open: true,
    title: 'Processing',
    message: 'Please wait while we process your request...',
    buttonText: 'OK',
    variant: 'loading',
    onClose: () => console.log('Closed'),
    onOpenChange: (open) => console.log('Open changed:', open),
  },
};

/**
 * Long message text with proper wrapping
 */
export const LongMessage: Story = {
  args: {
    open: true,
    title: 'System Notification',
    message:
      'Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.',
    buttonText: 'OK',
    variant: 'info',
    onClose: () => console.log('Closed'),
    onOpenChange: (open) => console.log('Open changed:', open),
  },
};

/**
 * Interactive example using the hook with all variants
 */
export const InteractiveWithHook = () => {
  const { alert, AlertDialogComponent } = useAlertDialog();

  const handleInfo = async () => {
    await alert({
      title: 'Information',
      message: 'This is an informational message.',
      variant: 'info',
    });
  };

  const handleSuccess = async () => {
    await alert({
      title: 'Success!',
      message: 'Operation completed successfully.',
      variant: 'success',
      buttonText: 'Awesome!',
    });
  };

  const handleWarning = async () => {
    await alert({
      title: 'Warning',
      message: 'Please proceed with caution.',
      variant: 'warning',
      buttonText: 'Got it',
    });
  };

  const handleError = async () => {
    await alert({
      title: 'Error',
      message: 'Something went wrong. Please try again.',
      variant: 'error',
      buttonText: 'Dismiss',
    });
  };

  const handleLoading = async () => {
    await alert({
      title: 'Processing',
      message: 'Please wait while we process your request...',
      variant: 'loading',
      buttonText: 'OK',
    });
  };

  return (
    <div className="space-y-4 p-6">
      <div className="grid grid-cols-2 gap-3">
        <Button onClick={handleInfo} variant="default">
          Show Info
        </Button>
        <Button onClick={handleSuccess} variant="default">
          Show Success
        </Button>
        <Button onClick={handleWarning} variant="default">
          Show Warning
        </Button>
        <Button onClick={handleError} variant="destructive">
          Show Error
        </Button>
        <Button onClick={handleLoading} variant="outline" className="col-span-2">
          Show Loading
        </Button>
      </div>
      <AlertDialogComponent />
    </div>
  );
};

InteractiveWithHook.parameters = {
  docs: {
    description: {
      story: 'Interactive example showing how to use the `useAlertDialog` hook with all five variants (info, success, warning, error, loading).',
    },
  },
};

/**
 * Example simulating a form save workflow
 */
export const SaveWorkflow = () => {
  const { alert, AlertDialogComponent } = useAlertDialog();
  const [isSaving, setIsSaving] = useState(false);

  const handleSave = async () => {
    setIsSaving(true);

    // Simulate API call
    await new Promise(resolve => setTimeout(resolve, 1000));

    // Random success/failure for demo
    const success = Math.random() > 0.3;

    if (success) {
      await alert({
        title: 'Saved Successfully',
        message: 'Your changes have been saved to the database.',
        variant: 'success',
        buttonText: 'Great!',
      });
    } else {
      await alert({
        title: 'Save Failed',
        message: 'Unable to save changes. Please check your connection and try again.',
        variant: 'error',
        buttonText: 'Try Again',
      });
    }

    setIsSaving(false);
  };

  return (
    <div className="space-y-4 p-6">
      <Button
        onClick={handleSave}
        disabled={isSaving}
        variant="default"
      >
        {isSaving ? 'Saving...' : 'Save Changes'}
      </Button>
      <AlertDialogComponent />
    </div>
  );
};

SaveWorkflow.parameters = {
  docs: {
    description: {
      story: 'Example showing a typical save workflow with success and error handling.',
    },
  },
};

/**
 * Example with sequential alerts
 */
export const SequentialAlerts = () => {
  const { alert, AlertDialogComponent } = useAlertDialog();

  const handleSequence = async () => {
    await alert({
      title: 'Step 1',
      message: 'Processing your request...',
      variant: 'info',
      buttonText: 'Continue',
    });

    await alert({
      title: 'Step 2',
      message: 'Validating data...',
      variant: 'info',
      buttonText: 'Continue',
    });

    await alert({
      title: 'Complete!',
      message: 'All steps completed successfully.',
      variant: 'success',
      buttonText: 'Done',
    });
  };

  return (
    <div className="space-y-4 p-6">
      <Button onClick={handleSequence} variant="default">
        Start Multi-Step Process
      </Button>
      <AlertDialogComponent />
    </div>
  );
};

SequentialAlerts.parameters = {
  docs: {
    description: {
      story: 'Example showing sequential alerts in a multi-step process.',
    },
  },
};
