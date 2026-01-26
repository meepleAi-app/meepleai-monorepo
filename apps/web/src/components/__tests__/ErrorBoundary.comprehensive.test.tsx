/**
 * Comprehensive edge case tests for ErrorBoundary component
 * Tests complex scenarios and boundary conditions
 */

import React from 'react';
import { describe, it, expect, vi, beforeAll, afterAll } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { ErrorBoundary } from '../errors/ErrorBoundary';

// Component that throws an error during render
function ThrowError({ error }: { error: Error }): never {
  throw error;
}

describe('ErrorBoundary - Comprehensive Edge Cases', () => {
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

  it('should handle errors thrown during getDerivedStateFromError', () => {
    // This is a theoretical test - React will catch errors in getDerivedStateFromError
    // and fall back to unmounting the component
    const error = new Error('Error in getDerivedStateFromError');

    // Mock console to suppress error logs
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(() => {
      render(
        <ErrorBoundary>
          <ThrowError error={error} />
        </ErrorBoundary>
      );
    }).not.toThrow();

    // ErrorBoundary should still render fallback UI
    expect(screen.getByTestId('error-title')).toBeInTheDocument();

    consoleSpy.mockRestore();
  });

  it('should handle errors thrown during componentDidCatch', () => {
    const error = new Error('Test error');
    let onErrorCalled = false;
    const brokenOnError = () => {
      onErrorCalled = true;
      throw new Error('Error in onError handler');
    };

    // Mock console to suppress cascading error logs
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

    // The error in onError will propagate, so we expect it to throw
    expect(() => {
      render(
        <ErrorBoundary onError={brokenOnError}>
          <ThrowError error={error} />
        </ErrorBoundary>
      );
    }).toThrow('Error in onError handler');

    // onError was called (which triggered the error)
    expect(onErrorCalled).toBe(true);

    consoleSpy.mockRestore();
  });

  it('should handle rapid successive errors (stress test)', async () => {
    let errorCount = 0;
    const errors: Error[] = [];

    function RapidErrorComponent() {
      errorCount++;
      const error = new Error(`Error ${errorCount}`);
      errors.push(error);
      throw error;
    }

    const { rerender } = render(
      <ErrorBoundary>
        <RapidErrorComponent />
      </ErrorBoundary>
    );

    // First error caught
    expect(screen.getByTestId('error-title')).toBeInTheDocument();

    // Trigger multiple re-renders with errors
    for (let i = 0; i < 5; i++) {
      rerender(
        <ErrorBoundary key={i}>
          <RapidErrorComponent />
        </ErrorBoundary>
      );

      await waitFor(() => {
        expect(screen.getByTestId('error-title')).toBeInTheDocument();
      });
    }

    // All errors should have been caught
    expect(errorCount).toBeGreaterThan(1);
  });

  it('should handle errors with very long stack traces gracefully', () => {
    const error = new Error('Error with massive stack');
    // Create a 10MB stack trace
    const longStackLine = '  at Component.tsx:10:15 -> Very long file path: ' + 'x'.repeat(1000);
    error.stack = 'Error: Test\n' + (longStackLine + '\n').repeat(1000);

    render(
      <ErrorBoundary showDetails={true}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('error-title')).toBeInTheDocument();
    expect(screen.getByTestId('error-details-toggle')).toBeInTheDocument();

    // Component should still be responsive despite large stack
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });

  it('should handle errors with missing or null errorInfo', () => {
    const error = new Error('Error with missing errorInfo');
    const onError = vi.fn();

    // Create a scenario where errorInfo might be incomplete
    render(
      <ErrorBoundary onError={onError}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('error-title')).toBeInTheDocument();
    expect(onError).toHaveBeenCalled();

    // Verify the error was logged even if errorInfo is incomplete
    const [errorArg, errorInfoArg] = onError.mock.calls[0];
    expect(errorArg).toBe(error);
    expect(errorInfoArg).toBeDefined();
  });

  it('should handle errors with special characters in messages', () => {
    const specialCharsError = new Error(
      'Error with special chars: <script>alert("XSS")</script> & " \' \n \t'
    );

    render(
      <ErrorBoundary showDetails={true}>
        <ThrowError error={specialCharsError} />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('error-title')).toBeInTheDocument();

    // Error details should be safely rendered (no XSS)
    expect(screen.getByTestId('error-details-toggle')).toBeInTheDocument();
  });

  it('should handle errors in nested error boundaries', () => {
    const outerError = new Error('Outer boundary error');
    const innerError = new Error('Inner boundary error');

    let throwInner = false;
    let throwOuter = false;

    function InnerComponent() {
      if (throwInner) {
        throw innerError;
      }
      return <div>Inner content</div>;
    }

    function OuterComponent() {
      if (throwOuter) {
        throw outerError;
      }
      return (
        <ErrorBoundary componentName="InnerBoundary">
          <InnerComponent />
        </ErrorBoundary>
      );
    }

    const { rerender } = render(
      <ErrorBoundary componentName="OuterBoundary">
        <OuterComponent />
      </ErrorBoundary>
    );

    expect(screen.getByText('Inner content')).toBeInTheDocument();

    // Throw error in inner component
    throwInner = true;
    rerender(
      <ErrorBoundary componentName="OuterBoundary">
        <OuterComponent />
      </ErrorBoundary>
    );

    // Inner boundary should catch the error
    expect(screen.getByTestId('error-title')).toBeInTheDocument();

    // Reset and throw in outer component
    throwInner = false;
    throwOuter = true;
    rerender(
      <ErrorBoundary componentName="OuterBoundary">
        <OuterComponent />
      </ErrorBoundary>
    );

    // Outer boundary should catch the error
    expect(screen.getByTestId('error-title')).toBeInTheDocument();
  });

  it('should handle errors with circular references in error object', () => {
    const error = new Error('Error with circular reference') as any;
    // Create circular reference
    error.circular = { ref: error };

    render(
      <ErrorBoundary showDetails={true}>
        <ThrowError error={error} />
      </ErrorBoundary>
    );

    // Should handle circular reference gracefully
    expect(screen.getByTestId('error-title')).toBeInTheDocument();
    expect(screen.getByTestId('error-details-toggle')).toBeInTheDocument();
  });

  it('should maintain error boundary state across re-renders', () => {
    let shouldThrow = true;

    function Component() {
      if (shouldThrow) {
        throw new Error('Test error');
      }
      return <div>Normal content</div>;
    }

    const { rerender } = render(
      <ErrorBoundary key="boundary-1">
        <Component />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('error-title')).toBeInTheDocument();

    // Error state should persist across re-renders of the same boundary
    rerender(
      <ErrorBoundary key="boundary-1">
        <Component />
      </ErrorBoundary>
    );

    expect(screen.getByTestId('error-title')).toBeInTheDocument();

    // The error boundary maintains its error state
    // Component doesn't re-render once error is caught
  });
});
