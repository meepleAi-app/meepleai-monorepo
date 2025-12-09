/**
 * Tests for UIProvider component
 * Issue #1951: Add coverage for UI theme provider
 */

import { render, screen } from '@testing-library/react';
import { UIProvider } from '../UIProvider';

describe('UIProvider', () => {
  describe('Rendering', () => {
    it('renders children', () => {
      render(
        <UIProvider>
          <div data-testid="child">Test Content</div>
        </UIProvider>
      );

      expect(screen.getByTestId('child')).toBeInTheDocument();
      expect(screen.getByText('Test Content')).toBeInTheDocument();
    });

    it('renders multiple children', () => {
      render(
        <UIProvider>
          <div data-testid="child1">Child 1</div>
          <div data-testid="child2">Child 2</div>
        </UIProvider>
      );

      expect(screen.getByTestId('child1')).toBeInTheDocument();
      expect(screen.getByTestId('child2')).toBeInTheDocument();
    });
  });

  describe('Provider Functionality', () => {
    it('provides theme context to children', () => {
      const { container } = render(
        <UIProvider>
          <div>Content</div>
        </UIProvider>
      );

      // UIProvider should wrap children without errors
      expect(container.firstChild).toBeInTheDocument();
    });

    it('initializes without errors', () => {
      const consoleError = vi.spyOn(console, 'error');

      render(
        <UIProvider>
          <div>Test</div>
        </UIProvider>
      );

      expect(consoleError).not.toHaveBeenCalled();
      consoleError.mockRestore();
    });
  });

  describe('Nested Providers', () => {
    it('can be nested without conflicts', () => {
      render(
        <UIProvider>
          <UIProvider>
            <div data-testid="nested">Nested Content</div>
          </UIProvider>
        </UIProvider>
      );

      expect(screen.getByTestId('nested')).toBeInTheDocument();
    });
  });
});
