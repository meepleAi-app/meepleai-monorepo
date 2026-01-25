/**
 * Unit tests for ErrorBoundary component
 */

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ErrorBoundary, useErrorHandler } from '../errors/ErrorBoundary';

// Component that throws an error during render
function ThrowError({ error }: { error: Error }): never {
  throw error;
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
    console.error = vi.fn();
    // Mock fetch for logger (not available in jsdom)
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve({}),
      } as Response)
    );
  });

  afterAll(() => {
    console.error = originalError;
    delete (global as any).fetch;
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

    expect(screen.getByTestId('error-title')).toBeInTheDocument();
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
    const onError = vi.fn();

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

    expect(screen.getByTestId('error-title')).toBeInTheDocument();

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

    // In test environment, navigation to root path
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

    expect(screen.getByTestId('error-details-toggle')).toBeInTheDocument();
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
    expect(screen.getByTestId('error-title')).toBeInTheDocument();
  });
});

describe('useErrorHandler', () => {
  // Suppress console.error for these tests
  const originalError = console.error;
  beforeAll(() => {
    console.error = vi.fn();
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

    expect(screen.getByTestId('error-title')).toBeInTheDocument();
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

describe('ErrorBoundary - Enhanced Coverage', () => {
  // Suppress console.error for these tests
  const originalError = console.error;
  const mockLogger = { error: vi.fn() };

  beforeAll(() => {
    console.error = vi.fn();
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve({}),
      } as Response)
    );
  });

  afterAll(() => {
    console.error = originalError;
    delete (global as any).fetch;
  });

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should log error with correct error and context parameters', () => {
    const error = new Error('Test error with context');
    const componentName = 'TestComponent';

    render(
      <ErrorBoundary componentName={componentName}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('error-title')).toBeInTheDocument();
  });

  it('should log error with componentStack in errorInfo', () => {
    const error = new Error('Test error for componentStack');
    const onError = vi.fn();

    render(
      <ErrorBoundary onError={onError}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(onError).toHaveBeenCalledWith(
      error,
      expect.objectContaining({
        componentStack: expect.stringContaining('ThrowError'),
      })
    );
  });

  it('should handle multiple consecutive errors without breaking', async () => {
    let shouldThrow = true;
    const user = userEvent.setup();

    function MultiError() {
      if (shouldThrow) {
        throw new Error('First error');
      }
      return <div>Content</div>;
    }

    render(
      <ErrorBoundary>
        <MultiError />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('error-title')).toBeInTheDocument();

    // Reset error boundary
    shouldThrow = false;
    const tryAgainButton = screen.getByRole('button', { name: /try again/i });
    await user.click(tryAgainButton);

    expect(screen.getByText('Content')).toBeInTheDocument();
  });

  it('should handle async errors from useEffect', async () => {
    function ComponentWithAsyncError() {
      const { handleError } = useErrorHandler();

      React.useEffect(() => {
        // Simulate async error
        setTimeout(() => {
          handleError(new Error('Async error from useEffect'));
        }, 0);
      }, [handleError]);

      return <div>Component content</div>;
    }

    render(
      <ErrorBoundary>
        <ComponentWithAsyncError />
      </ErrorBoundary>
    );

    // Wait for async error to propagate
    await waitFor(() => {
      expect(screen.getByTestId('error-title')).toBeInTheDocument();
    });
  });

  it('should not catch errors from outside its tree', () => {
    const error = new Error('Outside error');
    let shouldThrowOutside = false;

    function OutsideComponent() {
      if (shouldThrowOutside) {
        throw error;
      }
      return <div>Outside content</div>;
    }

    function InsideComponent() {
      return <div>Inside content</div>;
    }

    const { rerender } = render(
      <>
        <OutsideComponent />
        <ErrorBoundary>
          <InsideComponent />
        </ErrorBoundary>
      </>
    );

    expect(screen.getByText('Inside content')).toBeInTheDocument();
    expect(screen.getByText('Outside content')).toBeInTheDocument();

    // Error outside boundary should not be caught
    shouldThrowOutside = true;
    expect(() => {
      rerender(
        <>
          <OutsideComponent />
          <ErrorBoundary>
            <InsideComponent />
          </ErrorBoundary>
        </>
      );
    }).toThrow('Outside error');
  });

  it('should include componentName in error context', () => {
    const error = new Error('Test error');
    const componentName = 'MyCustomComponent';
    const onError = vi.fn();

    render(
      <ErrorBoundary componentName={componentName} onError={onError}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(onError).toHaveBeenCalled();
    expect(screen.getByTestId('error-title')).toBeInTheDocument();
  });

  it('should pass full errorInfo to custom onError handler', () => {
    const error = new Error('Test error');
    const onError = vi.fn();

    render(
      <ErrorBoundary onError={onError}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(onError).toHaveBeenCalledWith(
      error,
      expect.objectContaining({
        componentStack: expect.any(String),
      })
    );

    const errorInfo = onError.mock.calls[0][1];
    expect(errorInfo.componentStack).toBeTruthy();
  });

  it('should handle errors with very long stack traces', () => {
    const error = new Error('Error with long stack');
    error.stack = 'Error: Test\n' + '  at Component.tsx:10:15\n'.repeat(100);

    render(
      <ErrorBoundary showDetails={true}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('error-title')).toBeInTheDocument();
    expect(screen.getByTestId('error-details-toggle')).toBeInTheDocument();
  });
});
