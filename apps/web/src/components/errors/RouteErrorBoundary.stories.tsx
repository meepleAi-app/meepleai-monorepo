import { Button } from '@/components/ui/button';

import { RouteErrorBoundary } from './RouteErrorBoundary';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Route Error Boundary - Error wrapper for route-level error handling
 *
 * ## Features
 * - **Error Categorization**: Network, auth, validation, server errors
 * - **User-friendly Display**: ErrorDisplay integration
 * - **Retry/Dismiss**: Actions for error recovery
 * - **Correlation ID Tracking**: Error tracking and logging
 * - **Development Mode**: Shows technical details in dev
 *
 * ## Usage
 * Wrap routes with this component to handle uncaught errors gracefully.
 */
const meta = {
  title: 'Errors/RouteErrorBoundary',
  component: RouteErrorBoundary,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1024],
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof RouteErrorBoundary>;

export default meta;
type Story = StoryObj<typeof meta>;

// Component that throws error for testing
const ErrorThrower = ({ errorMessage }: { errorMessage: string }) => {
  throw new Error(errorMessage);
};

// Component that works normally
const WorkingComponent = () => (
  <div className="p-8 text-center">
    <h2 className="text-2xl font-bold mb-4">Route Content Loaded Successfully</h2>
    <p className="text-gray-600">No errors detected. Route is functioning normally.</p>
  </div>
);

/**
 * Default error boundary wrapping working content.
 * No error thrown, shows children normally.
 */
export const Default: Story = {
  args: {
    routeName: 'example-route',
    children: <WorkingComponent />,
  },
};

/**
 * Network error caught by boundary.
 * Shows user-friendly network error message with retry.
 */
export const NetworkError: Story = {
  args: {
    routeName: 'api-route',
    children: <ErrorThrower errorMessage="Network request failed: Connection timeout" />,
  },
};

/**
 * Authentication error caught by boundary.
 * Shows 401 error with login suggestion.
 */
export const AuthError: Story = {
  args: {
    routeName: 'protected-route',
    children: <ErrorThrower errorMessage="Unauthorized: Please log in to access this resource" />,
  },
};

/**
 * Validation error caught by boundary.
 * Shows validation failure message.
 */
export const ValidationError: Story = {
  args: {
    routeName: 'form-route',
    children: <ErrorThrower errorMessage="Validation failed: Invalid input format" />,
  },
};

/**
 * Server error (500) caught by boundary.
 * Shows internal server error message.
 */
export const ServerError: Story = {
  args: {
    routeName: 'data-route',
    children: <ErrorThrower errorMessage="Internal Server Error: Database connection failed" />,
  },
};

/**
 * Generic error caught by boundary.
 * Shows generic error message for uncategorized errors.
 */
export const GenericError: Story = {
  args: {
    routeName: 'misc-route',
    children: <ErrorThrower errorMessage="Something unexpected happened" />,
  },
};

/**
 * Error boundary with technical details visible (development mode).
 */
export const WithTechnicalDetails: Story = {
  args: {
    routeName: 'debug-route',
    showDetails: true,
    children: (
      <ErrorThrower errorMessage="TypeError: Cannot read property 'map' of undefined at Component.render" />
    ),
  },
};

/**
 * Custom fallback render function.
 * Shows custom error UI instead of default ErrorDisplay.
 */
export const CustomFallback: Story = {
  args: {
    routeName: 'custom-route',
    fallbackRender: (error, reset) => (
      <div className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900">
        <div className="text-center">
          <h1 className="text-4xl font-bold text-red-600 mb-4">Custom Error UI</h1>
          <p className="text-gray-600 mb-6">{error.message}</p>
          <Button onClick={reset}>Try Again</Button>
        </div>
      </div>
    ),
    children: <ErrorThrower errorMessage="Custom error scenario" />,
  },
};

/**
 * Mobile viewport (375px).
 */
export const Mobile: Story = {
  parameters: {
    viewport: { defaultViewport: 'mobile1' },
    chromatic: { viewports: [375] },
  },
  args: {
    routeName: 'mobile-route',
    children: <ErrorThrower errorMessage="Network error on mobile device" />,
  },
};

/**
 * Tablet viewport (768px).
 */
export const Tablet: Story = {
  parameters: {
    viewport: { defaultViewport: 'tablet' },
    chromatic: { viewports: [768] },
  },
  args: {
    routeName: 'tablet-route',
    children: <ErrorThrower errorMessage="Server error on tablet device" />,
  },
};

/**
 * Desktop viewport (1024px).
 */
export const Desktop: Story = {
  parameters: {
    viewport: { defaultViewport: 'desktop' },
    chromatic: { viewports: [1024] },
  },
  args: {
    routeName: 'desktop-route',
    children: <ErrorThrower errorMessage="Authentication error on desktop" />,
  },
};

/**
 * Dark theme variant.
 */
export const DarkTheme: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  args: {
    routeName: 'dark-route',
    children: <ErrorThrower errorMessage="Error in dark mode" />,
  },
  decorators: [
    Story => (
      <div className="dark min-h-screen bg-background">
        <Story />
      </div>
    ),
  ],
};
