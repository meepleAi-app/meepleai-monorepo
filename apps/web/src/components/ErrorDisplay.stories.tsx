import type { Meta, StoryObj } from '@storybook/react';
import { ErrorDisplay } from './ErrorDisplay';
import { ErrorCategory, type CategorizedError } from '@/lib/errorUtils';
import { fn } from '@storybook/test';
import React from 'react';

/**
 * ErrorDisplay - User-friendly error display with correlation ID tracking.
 * Implements PDF-06 error handling with toast integration and retry logic.
 * Supports automatic retry with exponential backoff.
 */
const meta = {
  title: 'Error/ErrorDisplay',
  component: ErrorDisplay,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
  argTypes: {
    showTechnicalDetails: {
      control: 'boolean',
      description: 'Show technical details toggle button',
    },
    showToast: {
      control: 'boolean',
      description: 'Show toast notification for transient errors',
    },
    autoRetry: {
      control: 'boolean',
      description: 'Enable automatic retry with exponential backoff',
    },
    maxRetries: {
      control: 'number',
      description: 'Maximum number of automatic retries',
    },
  },
  args: {
    onRetry: fn(),
    onDismiss: fn(),
  },
} satisfies Meta<typeof ErrorDisplay>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock errors for different categories
const networkError: CategorizedError = {
  category: ErrorCategory.Network,
  message: 'Connection lost. Please check your internet and try again.',
  technicalMessage: 'TypeError: fetch failed',
  correlationId: 'corr-1234-5678-9abc-def0',
  canRetry: true,
  suggestions: [
    'Check your internet connection',
    'Try again in a few moments',
    'Contact support if the problem persists',
  ],
};

const validationError: CategorizedError = {
  category: ErrorCategory.Validation,
  message: 'Invalid file or request.',
  technicalMessage: 'File size exceeds maximum allowed (100MB)',
  correlationId: 'corr-abcd-ef12-3456-7890',
  statusCode: 400,
  canRetry: false,
  suggestions: [
    'Check file size (maximum 100MB)',
    'Ensure file is a valid PDF',
    'Verify all required fields are filled',
  ],
};

const serverError: CategorizedError = {
  category: ErrorCategory.Server,
  message: 'Server error. Please try again in a few minutes.',
  technicalMessage: 'Internal Server Error',
  correlationId: 'corr-9876-5432-10ab-cdef',
  statusCode: 500,
  canRetry: true,
  suggestions: [
    'Wait a few minutes and try again',
    'Contact support with the error ID if the problem persists',
  ],
};

const processingError: CategorizedError = {
  category: ErrorCategory.Processing,
  message: 'Unable to process PDF. The file may be corrupted or in an unsupported format.',
  technicalMessage: 'PDF parsing failed: Invalid PDF structure',
  correlationId: 'corr-1a2b-3c4d-5e6f-7g8h',
  canRetry: false,
  suggestions: [
    'Re-download the original file',
    'Convert to a standard PDF format',
    'Ensure the file is not password-protected',
    'Try a different PDF file',
  ],
};

const unknownError: CategorizedError = {
  category: ErrorCategory.Unknown,
  message: 'An unexpected error occurred. Please try again.',
  technicalMessage: 'Uncaught exception: Something went wrong',
  correlationId: 'corr-xyz1-2345-6789-abcd',
  canRetry: true,
  suggestions: [
    'Try again',
    'Refresh the page',
    'Contact support with the error ID if the problem persists',
  ],
};

/**
 * Network error (connection issues)
 */
export const NetworkError: Story = {
  args: {
    error: networkError,
  },
};

/**
 * Validation error (client-side issues)
 */
export const ValidationError: Story = {
  args: {
    error: validationError,
  },
};

/**
 * Server error (500 status code)
 */
export const ServerError: Story = {
  args: {
    error: serverError,
  },
};

/**
 * Processing error (PDF parsing failed)
 */
export const ProcessingError: Story = {
  args: {
    error: processingError,
  },
};

/**
 * Unknown error (unexpected issues)
 */
export const UnknownError: Story = {
  args: {
    error: unknownError,
  },
};

/**
 * Error with technical details shown
 */
export const WithTechnicalDetails: Story = {
  args: {
    error: serverError,
    showTechnicalDetails: true,
  },
};

/**
 * Error without correlation ID
 */
export const WithoutCorrelationId: Story = {
  args: {
    error: {
      ...networkError,
      correlationId: undefined,
    },
  },
};

/**
 * Error without suggestions
 */
export const WithoutSuggestions: Story = {
  args: {
    error: {
      ...serverError,
      suggestions: [],
    },
  },
};

/**
 * Non-retryable error (no retry button)
 */
export const NonRetryable: Story = {
  args: {
    error: validationError,
  },
};

/**
 * Error with dismiss button only
 */
export const WithDismissOnly: Story = {
  args: {
    error: {
      ...processingError,
      canRetry: false,
    },
    onRetry: undefined,
  },
};

/**
 * Interactive retry demo
 */
const InteractiveRetryComponent = () => {
  const [retryCount, setRetryCount] = React.useState(0);
  const [isError, setIsError] = React.useState(true);

  const handleRetry = () => {
    setRetryCount(prev => prev + 1);
    // Simulate success after 2 retries
    if (retryCount >= 1) {
      setIsError(false);
    }
  };

  if (!isError) {
    return (
      <div className="p-6 bg-green-50 border-2 border-green-500 rounded-lg">
        <p className="text-green-700 font-semibold">✓ Success after {retryCount + 1} attempts!</p>
        <button
          onClick={() => {
            setIsError(true);
            setRetryCount(0);
          }}
          className="mt-4 px-4 py-2 bg-green-500 text-white rounded hover:bg-green-600"
        >
          Reset Demo
        </button>
      </div>
    );
  }

  return (
    <ErrorDisplay
      error={networkError}
      onRetry={handleRetry}
      onDismiss={() => {
        setIsError(false);
        setRetryCount(0);
      }}
    />
  );
};

export const InteractiveRetry: Story = {
  render: () => <InteractiveRetryComponent />,
};

/**
 * Auto-retry with exponential backoff
 */
const AutoRetryComponent = () => {
  const [retryCount, setRetryCount] = React.useState(0);
  const [isError, setIsError] = React.useState(true);

  const handleRetry = () => {
    setRetryCount(prev => prev + 1);
    // Simulate eventual success
    if (retryCount >= 2) {
      setIsError(false);
    }
  };

  if (!isError) {
    return (
      <div className="p-6 bg-green-50 border-2 border-green-500 rounded-lg">
        <p className="text-green-700 font-semibold">
          ✓ Auto-retry successful after {retryCount + 1} attempts!
        </p>
        <button
          onClick={() => {
            setIsError(true);
            setRetryCount(0);
          }}
          className="mt-4 px-4 py-2 bg-green-500 text-white rounded hover:bg-green-600"
        >
          Reset Demo
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="p-4 bg-blue-50 border border-blue-200 rounded">
        <p className="text-sm text-blue-700">
          Auto-retry enabled with exponential backoff (1s, 2s, 4s). Max 3 retries.
        </p>
      </div>
      <ErrorDisplay
        error={networkError}
        onRetry={handleRetry}
        autoRetry={true}
        maxRetries={3}
        showToast={false}
      />
    </div>
  );
};

export const AutoRetry: Story = {
  render: () => <AutoRetryComponent />,
};

/**
 * All error categories showcase
 */
export const AllErrorCategories: Story = {
  render: () => (
    <div className="space-y-4">
      <ErrorDisplay error={networkError} />
      <ErrorDisplay error={validationError} />
      <ErrorDisplay error={serverError} />
      <ErrorDisplay error={processingError} />
      <ErrorDisplay error={unknownError} />
    </div>
  ),
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
  args: {
    error: networkError,
    showTechnicalDetails: true,
  },
};
