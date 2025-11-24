/**
 * Tests for QueryProvider component
 *
 * Issue #1255: FE-QUALITY — Restore 90% test coverage
 *
 * Tests the TanStack Query provider setup for Next.js App Router
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { QueryProvider } from '../QueryProvider';
import { useQueryClient } from '@tanstack/react-query';

// Mock ReactQueryDevtools to avoid rendering issues in tests
vi.mock('@tanstack/react-query-devtools', () => ({
  ReactQueryDevtools: () => <div data-testid="react-query-devtools">DevTools</div>,
}));

// Test component that accesses QueryClient
function TestComponent() {
  const queryClient = useQueryClient();
  return (
    <div>
      <p>QueryClient exists: {queryClient ? 'yes' : 'no'}</p>
    </div>
  );
}

describe('QueryProvider', () => {
  const originalEnv = process.env.NODE_ENV;

  beforeEach(() => {
    // Make NODE_ENV writable for tests
    Object.defineProperty(process.env, 'NODE_ENV', {
      value: undefined,
      writable: true,
      configurable: true,
    });
  });

  afterEach(() => {
    // Restore original NODE_ENV
    Object.defineProperty(process.env, 'NODE_ENV', {
      value: originalEnv,
      writable: true,
      configurable: true,
    });
  });

  it('renders children correctly', () => {
    render(
      <QueryProvider>
        <div data-testid="test-child">Test Child</div>
      </QueryProvider>
    );

    expect(screen.getByTestId('test-child')).toBeInTheDocument();
    expect(screen.getByTestId('test-child')).toHaveTextContent('Test Child');
  });

  it('provides QueryClient to children', () => {
    render(
      <QueryProvider>
        <TestComponent />
      </QueryProvider>
    );

    expect(screen.getByText('QueryClient exists: yes')).toBeInTheDocument();
  });

  it('renders DevTools in development mode', () => {
    (process.env as any).NODE_ENV = 'development';

    render(
      <QueryProvider>
        <div>Content</div>
      </QueryProvider>
    );

    expect(screen.getByTestId('react-query-devtools')).toBeInTheDocument();
  });

  it('does not render DevTools in production mode', () => {
    (process.env as any).NODE_ENV = 'production';

    render(
      <QueryProvider>
        <div>Content</div>
      </QueryProvider>
    );

    expect(screen.queryByTestId('react-query-devtools')).not.toBeInTheDocument();
  });

  it('does not render DevTools in test mode', () => {
    (process.env as any).NODE_ENV = 'test';

    render(
      <QueryProvider>
        <div>Content</div>
      </QueryProvider>
    );

    expect(screen.queryByTestId('react-query-devtools')).not.toBeInTheDocument();
  });

  it('wraps multiple children correctly', () => {
    render(
      <QueryProvider>
        <div data-testid="child-1">Child 1</div>
        <div data-testid="child-2">Child 2</div>
        <div data-testid="child-3">Child 3</div>
      </QueryProvider>
    );

    expect(screen.getByTestId('child-1')).toBeInTheDocument();
    expect(screen.getByTestId('child-2')).toBeInTheDocument();
    expect(screen.getByTestId('child-3')).toBeInTheDocument();
  });

  it('provides same QueryClient instance to multiple components', () => {
    let queryClient1: any;
    let queryClient2: any;

    function Component1() {
      queryClient1 = useQueryClient();
      return <div>Component 1</div>;
    }

    function Component2() {
      queryClient2 = useQueryClient();
      return <div>Component 2</div>;
    }

    render(
      <QueryProvider>
        <Component1 />
        <Component2 />
      </QueryProvider>
    );

    expect(queryClient1).toBeDefined();
    expect(queryClient2).toBeDefined();
    expect(queryClient1).toBe(queryClient2);
  });
});
