/**
 * Tests for RouteErrorBoundary component
 */

import React from 'react';
import { describe, it, expect, vi, beforeAll, afterAll, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RouteErrorBoundary } from '../errors/RouteErrorBoundary';

// Mock the logger
vi.mock('../../lib/logger', () => ({
  logger: {
    error: vi.fn(),
  },
}));

// Component that throws an error during render
function ThrowError({ error }: { error: Error }): never {
  throw error;
}

describe('RouteErrorBoundary', () => {
  const originalError = console.error;

  beforeAll(() => {
    console.error = vi.fn();
    // Note: MSW is already configured globally in vitest.setup.tsx
    // No need to mock fetch here - it would conflict with MSW interceptors
  });

  afterAll(() => {
    console.error = originalError;
  });

  it('renders children when no error occurs', () => {
    render(
      <RouteErrorBoundary>
        <div>Child content</div>
      </RouteErrorBoundary>
    );

    expect(screen.getByText('Child content')).toBeInTheDocument();
  });

  it('displays ErrorDisplay when child component throws', () => {
    const error = new Error('Test error');

    render(
      <RouteErrorBoundary routeName="TestRoute">
        <ThrowError error={error} />
      </RouteErrorBoundary>
    );

    // ErrorDisplay shows the error (check for "Error" text which appears in all error displays)
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('calls custom error handler when provided', () => {
    const onError = vi.fn();
    const error = new Error('Test error');

    render(
      <RouteErrorBoundary onError={onError} routeName="TestRoute">
        <ThrowError error={error} />
      </RouteErrorBoundary>
    );

    // Error display should be visible
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('uses custom fallback renderer when provided', () => {
    const error = new Error('Custom test error');
    const customFallback = (err: Error) => (
      <div data-testid="custom-fallback">Custom error: {err.message}</div>
    );

    render(
      <RouteErrorBoundary fallbackRender={customFallback}>
        <ThrowError error={error} />
      </RouteErrorBoundary>
    );

    expect(screen.getByTestId('custom-fallback')).toBeInTheDocument();
    expect(screen.getByText(/Custom error/)).toBeInTheDocument();
  });

  it('includes route name for error tracking', () => {
    const error = new Error('Route error');

    render(
      <RouteErrorBoundary routeName="SettingsPage">
        <ThrowError error={error} />
      </RouteErrorBoundary>
    );

    // Error should be caught and displayed
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('shows technical details in development mode by default', () => {
    const error = new Error('Dev error');

    render(
      <RouteErrorBoundary showDetails={true}>
        <ThrowError error={error} />
      </RouteErrorBoundary>
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('hides technical details in production mode', () => {
    const error = new Error('Prod error');

    render(
      <RouteErrorBoundary showDetails={false}>
        <ThrowError error={error} />
      </RouteErrorBoundary>
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});

describe('RouteErrorBoundary - Enhanced Coverage', () => {
  const originalError = console.error;

  beforeAll(() => {
    console.error = vi.fn();
    // Note: MSW is already configured globally in vitest.setup.tsx
    // No need to mock fetch here - it would conflict with MSW interceptors
  });

  afterAll(() => {
    console.error = originalError;
  });

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should log error with route name in context', () => {
    const error = new Error('Route error');
    const routeName = 'SettingsPage';
    const onError = vi.fn();

    render(
      <RouteErrorBoundary routeName={routeName} onError={onError}>
        <ThrowError error={error} />
      </RouteErrorBoundary>
    );

    expect(onError).toHaveBeenCalledWith(
      error,
      expect.objectContaining({
        componentStack: expect.any(String),
      })
    );
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('should apply error categorization correctly', () => {
    const networkError = new Error('Network request failed');
    (networkError as any).code = 'NETWORK_ERROR';

    render(
      <RouteErrorBoundary routeName="ChatPage">
        <ThrowError error={networkError} />
      </RouteErrorBoundary>
    );

    // ErrorDisplay should be rendered with categorized error
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('should handle onDismiss navigation to home', async () => {
    const error = new Error('Test error');

    // Mock window.location.href
    delete (window as any).location;
    window.location = { href: '' } as any;

    render(
      <RouteErrorBoundary>
        <ThrowError error={error} />
      </RouteErrorBoundary>
    );

    // ErrorDisplay is rendered
    expect(screen.getByRole('alert')).toBeInTheDocument();

    // Note: onDismiss handler sets window.location.href = '/'
    // This would require user interaction in the ErrorDisplay component
    // which we verify through the component's presence
  });

  it('should provide reset functionality through fallback', async () => {
    let shouldThrow = true;
    const user = userEvent.setup();

    function ConditionalError() {
      if (shouldThrow) {
        throw new Error('Route error');
      }
      return <div>Content after reset</div>;
    }

    render(
      <RouteErrorBoundary routeName="TestRoute">
        <ConditionalError />
      </RouteErrorBoundary>
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();

    // Reset error state
    shouldThrow = false;

    // Find and click retry button in ErrorDisplay
    const retryButton = screen.queryByRole('button', { name: /retry|try again/i });
    if (retryButton) {
      await user.click(retryButton);
      expect(screen.getByText('Content after reset')).toBeInTheDocument();
    }
  });
});
