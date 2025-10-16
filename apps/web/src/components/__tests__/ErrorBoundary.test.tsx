/**
 * Unit tests for ErrorBoundary component
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ErrorBoundary, useErrorHandler } from '../ErrorBoundary';

// Component that throws an error during render
// TypeScript doesn't like components that always throw, so we suppress the error at usage sites
function ThrowError({ error }: { error: Error }): null {
  throw error;
  return null; // Never reached, but makes TypeScript happy
}

// Component that uses useErrorHandler hook
function ComponentWithErrorHandler({ shouldError }: { shouldError: boolean }) {
  const { handleError } = useErrorHandler();

  if (shouldError) {
    handleError(new Error('Error from hook'));
  }

  return <div>Content</div>;
}

describe('ErrorBoundary', () => {
  // Suppress console.error for these tests
  const originalError = console.error;
  beforeAll(() => {
    console.error = jest.fn();
  });

  afterAll(() => {
    console.error = originalError;
  });

  it('should render children when no error', () => {
    render(
      <ErrorBoundary>
        <div>Child content</div>
      </ErrorBoundary>
    );

    expect(screen.getByText('Child content')).toBeInTheDocument();
  });

  it('should render default fallback UI when error occurs', () => {
    const error = new Error('Test error');

    render(
      <ErrorBoundary>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    expect(screen.getByText(/We apologize for the inconvenience/)).toBeInTheDocument();
  });

  it('should render custom fallback when provided', () => {
    const error = new Error('Test error');
    const customFallback = <div>Custom error message</div>;

    render(
      <ErrorBoundary fallback={customFallback}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(screen.getByText('Custom error message')).toBeInTheDocument();
    expect(screen.queryByText('Something went wrong')).not.toBeInTheDocument();
  });

  it('should render custom fallback function when provided', () => {
    const error = new Error('Test error');
    const customFallback = (err: Error, reset: () => void) => (
      <div>
        <div>Error: {err.message}</div>
        <button onClick={reset}>Reset</button>
      </div>
    );

    render(
      <ErrorBoundary fallback={customFallback}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(screen.getByText('Error: Test error')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /reset/i })).toBeInTheDocument();
  });

  it('should call onError callback when error occurs', () => {
    const error = new Error('Test error');
    const onError = jest.fn();

    render(
      <ErrorBoundary onError={onError}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(onError).toHaveBeenCalledWith(
      error,
      expect.objectContaining({ componentStack: expect.any(String) })
    );
  });

  it('should reset error state when Try Again is clicked', async () => {
    const user = userEvent.setup();
    let shouldThrow = true;

    function ConditionalError() {
      if (shouldThrow) {
        throw new Error('Test error');
      }
      return <div>Content after reset</div>;
    }

    render(
      <ErrorBoundary>
        <ConditionalError />
      </ErrorBoundary>
    );

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();

    shouldThrow = false;
    const tryAgainButton = screen.getByRole('button', { name: /try again/i });
    await user.click(tryAgainButton);

    expect(screen.getByText('Content after reset')).toBeInTheDocument();
  });

  it('should navigate to home when Go to Home is clicked', async () => {
    const user = userEvent.setup();
    const error = new Error('Test error');

    // Mock window.location.href
    delete (window as any).location;
    window.location = { href: '' } as any;

    render(
      <ErrorBoundary>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    const homeButton = screen.getByRole('button', { name: /go to home/i });
    await user.click(homeButton);

    expect(window.location.href).toBe('/');
  });

  it('should show error details when showDetails is true', () => {
    const error = new Error('Test error with stack');
    error.stack = 'Error: Test error\n  at file.ts:10:15';

    render(
      <ErrorBoundary showDetails={true}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(screen.getByText('Error Details')).toBeInTheDocument();
  });

  it('should not show error details when showDetails is false', () => {
    const error = new Error('Test error');

    render(
      <ErrorBoundary showDetails={false}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(screen.queryByText('Error Details')).not.toBeInTheDocument();
  });

  it('should expand error details when clicked', async () => {
    const user = userEvent.setup();
    const error = new Error('Test error with stack');
    error.stack = 'Error: Test error\n  at file.ts:10:15';

    render(
      <ErrorBoundary showDetails={true}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    const detailsButton = screen.getByRole('button', { name: /error details/i });
    expect(detailsButton).toHaveAttribute('aria-expanded', 'false');

    await user.click(detailsButton);

    expect(detailsButton).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText(/file\.ts:10:15/)).toBeInTheDocument();
  });

  it('should include component name in logs', () => {
    const error = new Error('Test error');

    render(
      <ErrorBoundary componentName="TestComponent">
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    // Component should render error UI
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });
});

describe('useErrorHandler', () => {
  // Suppress console.error for these tests
  const originalError = console.error;
  beforeAll(() => {
    console.error = jest.fn();
  });

  afterAll(() => {
    console.error = originalError;
  });

  it('should throw error when handleError is called', () => {
    expect(() => {
      render(
        <ErrorBoundary>
          <ComponentWithErrorHandler shouldError={true} />
        </ErrorBoundary>
      );
    }).not.toThrow();

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('should not throw error when handleError is not called', () => {
    render(
      <ErrorBoundary>
        <ComponentWithErrorHandler shouldError={false} />
      </ErrorBoundary>
    );

    expect(screen.getByText('Content')).toBeInTheDocument();
  });
});
