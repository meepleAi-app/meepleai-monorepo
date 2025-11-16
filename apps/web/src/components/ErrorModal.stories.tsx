import type { Meta, StoryObj } from '@storybook/react';
import { ErrorModal } from './ErrorModal';
import { ApiError, NetworkError, ValidationError } from '@/lib/errors';
import { fn } from '@storybook/test';
import React from 'react';

/**
 * ErrorModal - Modal for displaying error details with retry option.
 * Supports ApiError, NetworkError, and ValidationError with technical details.
 * Auto-closes on escape key, prevents body scroll when open.
 */
const meta = {
  title: 'Error/ErrorModal',
  component: ErrorModal,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    isOpen: {
      control: 'boolean',
      description: 'Whether the modal is open',
    },
    showDetails: {
      control: 'boolean',
      description: 'Show technical details section',
    },
    title: {
      control: 'text',
      description: 'Modal title',
    },
  },
  args: {
    onClose: fn(),
    onRetry: fn(),
  },
} satisfies Meta<typeof ErrorModal>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock errors
const apiError500 = new ApiError(
  'Internal server error occurred',
  500,
  '/api/v1/games',
  'GET',
  'corr-1234-5678-9abc-def0',
  true
);

const apiError401 = new ApiError(
  'Unauthorized',
  401,
  '/api/v1/chat',
  'POST',
  'corr-abcd-ef12-3456-7890'
);

const apiError403 = new ApiError(
  'Forbidden',
  403,
  '/api/v1/admin/users',
  'DELETE',
  'corr-9876-5432-10ab-cdef'
);

const apiError404 = new ApiError(
  'Game not found',
  404,
  '/api/v1/games/non-existent-id',
  'GET'
);

const apiError429 = new ApiError(
  'Too many requests',
  429,
  '/api/v1/chat',
  'POST',
  'corr-1a2b-3c4d-5e6f-7g8h'
);

const networkError = new NetworkError(
  'Failed to fetch',
  '/api/v1/games',
  new TypeError('fetch failed')
);

const validationError = new ValidationError(
  'File size exceeds maximum allowed (100MB)',
  'file',
  {
    file: 'File too large',
    maxSize: '100MB',
  }
);

const genericError = new Error('Something went wrong unexpectedly');

/**
 * Closed modal (default state)
 */
export const Closed: Story = {
  args: {
    isOpen: false,
    error: apiError500,
  },
};

/**
 * API error (500 - Server Error)
 */
export const ApiError500: Story = {
  args: {
    isOpen: true,
    error: apiError500,
    title: 'Server Error',
  },
};

/**
 * API error (401 - Unauthorized)
 */
export const ApiError401: Story = {
  args: {
    isOpen: true,
    error: apiError401,
    title: 'Authentication Required',
  },
};

/**
 * API error (403 - Forbidden)
 */
export const ApiError403: Story = {
  args: {
    isOpen: true,
    error: apiError403,
    title: 'Access Denied',
  },
};

/**
 * API error (404 - Not Found)
 */
export const ApiError404: Story = {
  args: {
    isOpen: true,
    error: apiError404,
    title: 'Not Found',
  },
};

/**
 * API error (429 - Too Many Requests)
 */
export const ApiError429: Story = {
  args: {
    isOpen: true,
    error: apiError429,
    title: 'Rate Limit Exceeded',
  },
};

/**
 * Network error (connection failed)
 */
export const NetworkErrorModal: Story = {
  args: {
    isOpen: true,
    error: networkError,
    title: 'Connection Failed',
  },
};

/**
 * Validation error (client-side)
 */
export const ValidationErrorModal: Story = {
  args: {
    isOpen: true,
    error: validationError,
    title: 'Validation Error',
  },
};

/**
 * Generic error (unknown type)
 */
export const GenericError: Story = {
  args: {
    isOpen: true,
    error: genericError,
    title: 'Error',
  },
};

/**
 * Error with technical details shown
 */
export const WithTechnicalDetails: Story = {
  args: {
    isOpen: true,
    error: apiError500,
    title: 'Server Error',
    showDetails: true,
  },
};

/**
 * Error with retry button (retryable API error)
 */
export const WithRetry: Story = {
  args: {
    isOpen: true,
    error: apiError500,
    title: 'Server Error',
  },
};

/**
 * Error without retry button (non-retryable)
 */
export const WithoutRetry: Story = {
  args: {
    isOpen: true,
    error: apiError404,
    title: 'Not Found',
    onRetry: undefined,
  },
};

/**
 * Interactive modal demo
 */
const InteractiveModalComponent = () => {
  const [isOpen, setIsOpen] = React.useState(false);
  const [error, setError] = React.useState<Error | null>(null);

  const errors: Array<{ label: string; error: Error }> = [
    { label: 'Server Error (500)', error: apiError500 },
    { label: 'Unauthorized (401)', error: apiError401 },
    { label: 'Forbidden (403)', error: apiError403 },
    { label: 'Not Found (404)', error: apiError404 },
    { label: 'Rate Limit (429)', error: apiError429 },
    { label: 'Network Error', error: networkError },
    { label: 'Validation Error', error: validationError },
  ];

  const openModal = (errorToShow: Error) => {
    setError(errorToShow);
    setIsOpen(true);
  };

  return (
    <div className="space-y-4">
      <div className="p-4 bg-gray-50 border border-gray-200 rounded">
        <h3 className="font-semibold mb-3">Click to see error modals:</h3>
        <div className="flex flex-wrap gap-2">
          {errors.map(({ label, error }) => (
            <button
              key={label}
              onClick={() => openModal(error)}
              className="px-4 py-2 bg-red-500 text-white rounded hover:bg-red-600 text-sm"
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      <ErrorModal
        isOpen={isOpen}
        onClose={() => setIsOpen(false)}
        error={error}
        title="Error"
        showDetails={true}
        onRetry={() => {
          setIsOpen(false);
          // Simulate retry logic
          setTimeout(() => {
            alert('Retry action triggered!');
          }, 100);
        }}
      />
    </div>
  );
};

export const Interactive: Story = {
  render: () => <InteractiveModalComponent />,
  parameters: {
    layout: 'padded',
  },
};

/**
 * Multiple error types comparison
 */
const ErrorComparisonComponent = () => {
  const [selectedError, setSelectedError] = React.useState<Error>(apiError500);

  const errors = [
    { label: 'API 500', error: apiError500 },
    { label: 'API 401', error: apiError401 },
    { label: 'Network', error: networkError },
    { label: 'Validation', error: validationError },
  ];

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        {errors.map(({ label, error }) => (
          <button
            key={label}
            onClick={() => setSelectedError(error)}
            className={`px-3 py-1 rounded text-sm ${
              selectedError === error
                ? 'bg-blue-500 text-white'
                : 'bg-gray-200 text-gray-700 hover:bg-gray-300'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

      <ErrorModal
        isOpen={true}
        onClose={() => {}}
        error={selectedError}
        title="Error Comparison"
        showDetails={true}
      />
    </div>
  );
};

export const ErrorComparison: Story = {
  render: () => <ErrorComparisonComponent />,
  parameters: {
    layout: 'padded',
  },
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
    isOpen: true,
    error: apiError500,
    title: 'Server Error',
    showDetails: true,
  },
};
