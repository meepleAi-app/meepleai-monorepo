/**
 * Tests for QueryProvider component
 *
 * Issue #1255: FE-QUALITY — Restore 90% test coverage
 * Issue #1868: TECH-DEBT — Test Suite Failures fix
 *
 * Tests the TanStack Query provider setup for Next.js App Router
 *
 * Note: DevTools testing uses mocked component since process.env.NODE_ENV
 * cannot be reliably changed in Vitest environment.
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

  /**
   * Note: DevTools visibility tests are based on process.env.NODE_ENV
   * In test environment, NODE_ENV is 'test', so DevTools should not render.
   * We verify the core behavior by checking DevTools doesn't appear in test mode.
   */
  it('does not render DevTools in test mode (NODE_ENV=test)', () => {
    render(
      <QueryProvider>
        <div>Content</div>
      </QueryProvider>
    );

    // In test mode (NODE_ENV=test), DevTools should NOT render
    // because the component only shows DevTools when NODE_ENV === 'development'
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
    let queryClient1: unknown;
    let queryClient2: unknown;

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

  it('renders correctly without crashing', () => {
    // Simple smoke test to ensure the provider doesn't crash
    expect(() => {
      render(
        <QueryProvider>
          <div>Test content</div>
        </QueryProvider>
      );
    }).not.toThrow();
  });

  it('allows useQueryClient hook to work in children', () => {
    function ClientAccessor() {
      const client = useQueryClient();
      return <div data-testid="client-info">Client ready: {client ? 'true' : 'false'}</div>;
    }

    render(
      <QueryProvider>
        <ClientAccessor />
      </QueryProvider>
    );

    expect(screen.getByTestId('client-info')).toHaveTextContent('Client ready: true');
  });
});
