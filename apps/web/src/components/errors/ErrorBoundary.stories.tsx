/**
 * Storybook stories for ErrorBoundary component
 * Visual regression testing with Chromatic
 */

import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { ErrorBoundary } from './ErrorBoundary';

const meta = {
  title: 'Components/Errors/ErrorBoundary',
  component: ErrorBoundary,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Error boundary component that catches React rendering errors and displays fallback UI with optional error details and reset functionality.',
      },
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof ErrorBoundary>;

export default meta;
type Story = StoryObj<typeof meta>;

// Component that throws an error
function ErrorComponent() {
  throw new Error('Something went wrong in the component');
}

// Component that can conditionally throw
function ConditionalErrorComponent({ shouldError }: { shouldError: boolean }) {
  if (shouldError) {
    throw new Error('Conditional error triggered');
  }
  return (
    <div className="p-8 text-center">
      <h2 className="text-2xl font-bold text-gray-900 dark:text-gray-100">
        Component Working Correctly
      </h2>
      <p className="mt-2 text-gray-600 dark:text-gray-400">
        No errors detected. Everything is functioning normally.
      </p>
    </div>
  );
}

/**
 * Default error state with fallback UI
 */
export const DefaultErrorState: Story = {
  render: () => (
    <ErrorBoundary>
      <ErrorComponent />
    </ErrorBoundary>
  ),
};

/**
 * Error state with technical details expanded (development mode)
 */
export const WithDetailsExpanded: Story = {
  render: () => {
    // Create error with detailed stack trace
    const DetailedErrorComponent = () => {
      const error = new Error('Detailed error with stack trace');
      error.stack = `Error: Detailed error with stack trace
    at DetailedErrorComponent (ErrorBoundary.stories.tsx:45:19)
    at renderWithHooks (react-dom.development.js:14985:18)
    at mountIndeterminateComponent (react-dom.development.js:17811:13)
    at beginWork (react-dom.development.js:19049:16)
    at HTMLUnknownElement.callCallback (react-dom.development.js:3945:14)`;
      throw error;
    };

    return (
      <ErrorBoundary showDetails={true}>
        <DetailedErrorComponent />
      </ErrorBoundary>
    );
  },
};

/**
 * Error state with custom fallback UI
 */
export const CustomFallback: Story = {
  render: () => (
    <ErrorBoundary
      fallback={
        <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-red-50 to-red-100 dark:from-red-950 dark:to-red-900">
          <div className="max-w-md p-8 bg-white dark:bg-gray-800 rounded-lg shadow-xl">
            <div className="flex items-center justify-center w-16 h-16 mx-auto bg-red-100 dark:bg-red-900 rounded-full">
              <svg
                className="w-8 h-8 text-red-600 dark:text-red-400"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M6 18L18 6M6 6l12 12"
                />
              </svg>
            </div>
            <h2 className="mt-4 text-2xl font-bold text-center text-gray-900 dark:text-gray-100">
              Custom Error Handler
            </h2>
            <p className="mt-2 text-center text-gray-600 dark:text-gray-400">
              This is a custom fallback UI that provides a branded error experience.
            </p>
            <button
              className="mt-6 w-full px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-md font-medium transition-colors"
              onClick={() => window.location.reload()}
            >
              Refresh Page
            </button>
          </div>
        </div>
      }
    >
      <ErrorComponent />
    </ErrorBoundary>
  ),
};

/**
 * Error state in dark mode
 */
export const DarkMode: Story = {
  render: () => (
    <div className="dark bg-gray-900 min-h-screen">
      <ErrorBoundary showDetails={true}>
        <ErrorComponent />
      </ErrorBoundary>
    </div>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
};

/**
 * Interactive example with reset functionality
 */
export const InteractiveReset: Story = {
  render: () => {
    const InteractiveExample = () => {
      const [shouldError, setShouldError] = useState(true);

      return (
        <div className="min-h-screen bg-gray-50 dark:bg-gray-900 p-4">
          <div className="max-w-4xl mx-auto">
            <div className="mb-4 p-4 bg-white dark:bg-gray-800 rounded-lg shadow">
              <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100 mb-2">
                Error Boundary Controls
              </h3>
              <button
                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-md font-medium transition-colors"
                onClick={() => setShouldError(!shouldError)}
              >
                {shouldError ? 'Fix Error' : 'Trigger Error'}
              </button>
            </div>

            <ErrorBoundary
              fallback={(error, reset) => (
                <div className="p-8 bg-white dark:bg-gray-800 rounded-lg shadow-lg">
                  <h2 className="text-xl font-bold text-red-600 dark:text-red-400">
                    Error Caught!
                  </h2>
                  <p className="mt-2 text-gray-600 dark:text-gray-400">{error.message}</p>
                  <button
                    className="mt-4 px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-md font-medium transition-colors"
                    onClick={() => {
                      setShouldError(false);
                      reset();
                    }}
                  >
                    Reset Error Boundary
                  </button>
                </div>
              )}
            >
              <ConditionalErrorComponent shouldError={shouldError} />
            </ErrorBoundary>
          </div>
        </div>
      );
    };

    return <InteractiveExample />;
  },
};

/**
 * Error with long error message
 */
export const LongErrorMessage: Story = {
  render: () => {
    const LongMessageErrorComponent = () => {
      const longError = new Error(
        'This is a very long error message that demonstrates how the ErrorBoundary handles errors with extensive descriptions. ' +
          'It includes multiple sentences and detailed information about what went wrong in the application. ' +
          'The UI should handle this gracefully without breaking the layout or becoming unreadable. ' +
          'This helps ensure that even in edge cases with verbose error messages, the error boundary provides a good user experience.'
      );
      throw longError;
    };

    return (
      <ErrorBoundary showDetails={true}>
        <LongMessageErrorComponent />
      </ErrorBoundary>
    );
  },
};

/**
 * No error state (normal operation)
 */
export const NoError: Story = {
  render: () => (
    <ErrorBoundary>
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-green-50 to-green-100 dark:from-green-950 dark:to-green-900">
        <div className="max-w-md p-8 bg-white dark:bg-gray-800 rounded-lg shadow-xl">
          <div className="flex items-center justify-center w-16 h-16 mx-auto bg-green-100 dark:bg-green-900 rounded-full">
            <svg
              className="w-8 h-8 text-green-600 dark:text-green-400"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M5 13l4 4L19 7"
              />
            </svg>
          </div>
          <h2 className="mt-4 text-2xl font-bold text-center text-gray-900 dark:text-gray-100">
            Everything Working
          </h2>
          <p className="mt-2 text-center text-gray-600 dark:text-gray-400">
            No errors detected. The ErrorBoundary is monitoring this component tree.
          </p>
        </div>
      </div>
    </ErrorBoundary>
  ),
};
