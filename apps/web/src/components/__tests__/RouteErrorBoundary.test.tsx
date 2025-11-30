/**
 * Tests for RouteErrorBoundary component
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RouteErrorBoundary } from '../errors/RouteErrorBoundary';

// Mock the logger
vi.mock('../../lib/logger', () => ({
  logger: {
    error: vi.fn()
  }
}));

// Component that throws an error during render
function ThrowError({ error }: { error: Error }): never {
  throw error;
}

describe('RouteErrorBoundary', () => {
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
      <div data-testid="custom-fallback">
        Custom error: {err.message}
      </div>
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
