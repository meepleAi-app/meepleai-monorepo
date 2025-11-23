/**
 * @jest-environment jsdom
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { SimpleErrorMessage, type SimpleErrorMessageProps } from '../errors/SimpleErrorMessage';

describe('SimpleErrorMessage', () => {
  describe('Rendering', () => {
    it('should render error message when provided', () => {
      render(<SimpleErrorMessage message="Test error message" />);

      const alert = screen.getByRole('alert');
      expect(alert).toBeInTheDocument();
      expect(alert).toHaveTextContent('Test error message');
    });

    it('should not render when message is null', () => {
      const { container } = render(<SimpleErrorMessage message={null} />);
      expect(container.firstChild).toBeNull();
    });

    it('should not render when message is undefined', () => {
      const { container } = render(<SimpleErrorMessage message={undefined} />);
      expect(container.firstChild).toBeNull();
    });

    it('should not render when message is empty string', () => {
      const { container } = render(<SimpleErrorMessage message="" />);
      expect(container.firstChild).toBeNull();
    });
  });

  describe('Accessibility', () => {
    it('should have role="alert" for screen readers', () => {
      render(<SimpleErrorMessage message="Error" />);
      const alert = screen.getByRole('alert');
      expect(alert).toHaveAttribute('role', 'alert');
    });

    it('should have aria-live="polite" for assistive technology', () => {
      render(<SimpleErrorMessage message="Error" />);
      const alert = screen.getByRole('alert');
      expect(alert).toHaveAttribute('aria-live', 'polite');
    });

    it('should have accessible dismiss button label', () => {
      const onDismiss = jest.fn();
      render(<SimpleErrorMessage message="Error" onDismiss={onDismiss} />);

      const dismissButton = screen.getByRole('button', { name: /dismiss message/i });
      expect(dismissButton).toBeInTheDocument();
      expect(dismissButton).toHaveAttribute('aria-label', 'Dismiss message');
    });

    it('should have proper focus management on dismiss button', () => {
      const onDismiss = jest.fn();
      render(<SimpleErrorMessage message="Error" onDismiss={onDismiss} />);

      const dismissButton = screen.getByRole('button', { name: /dismiss message/i });
      expect(dismissButton).toHaveClass('focus:outline-none', 'focus:ring-2');
    });
  });

  describe('Variants', () => {
    it('should apply error variant styles by default', () => {
      render(<SimpleErrorMessage message="Error" />);
      const alert = screen.getByRole('alert');
      expect(alert.className).toMatch(/bg-red-500\/10/);
      expect(alert.className).toMatch(/border-red-500\/30/);
      expect(alert.className).toMatch(/text-red-400/);
    });

    it('should apply warning variant styles', () => {
      render(<SimpleErrorMessage message="Warning" variant="warning" />);
      const alert = screen.getByRole('alert');
      expect(alert.className).toMatch(/bg-yellow-500\/10/);
      expect(alert.className).toMatch(/border-yellow-500\/30/);
      expect(alert.className).toMatch(/text-yellow-400/);
    });

    it('should apply info variant styles', () => {
      render(<SimpleErrorMessage message="Info" variant="info" />);
      const alert = screen.getByRole('alert');
      expect(alert.className).toMatch(/bg-blue-500\/10/);
      expect(alert.className).toMatch(/border-blue-500\/30/);
      expect(alert.className).toMatch(/text-blue-400/);
    });

    it('should apply success variant styles', () => {
      render(<SimpleErrorMessage message="Success" variant="success" />);
      const alert = screen.getByRole('alert');
      expect(alert.className).toMatch(/bg-green-500\/10/);
      expect(alert.className).toMatch(/border-green-500\/30/);
      expect(alert.className).toMatch(/text-green-400/);
    });
  });

  describe('Dismiss Functionality', () => {
    it('should render dismiss button when onDismiss is provided', () => {
      const onDismiss = jest.fn();
      render(<SimpleErrorMessage message="Error" onDismiss={onDismiss} />);

      const dismissButton = screen.getByRole('button', { name: /dismiss message/i });
      expect(dismissButton).toBeInTheDocument();
    });

    it('should not render dismiss button when onDismiss is not provided', () => {
      render(<SimpleErrorMessage message="Error" />);

      const dismissButton = screen.queryByRole('button', { name: /dismiss message/i });
      expect(dismissButton).not.toBeInTheDocument();
    });

    it('should call onDismiss when dismiss button is clicked', () => {
      const onDismiss = jest.fn();
      render(<SimpleErrorMessage message="Error" onDismiss={onDismiss} />);

      const dismissButton = screen.getByRole('button', { name: /dismiss message/i });
      fireEvent.click(dismissButton);

      expect(onDismiss).toHaveBeenCalledTimes(1);
    });

    it('should have proper hover state on dismiss button', () => {
      const onDismiss = jest.fn();
      render(<SimpleErrorMessage message="Error" onDismiss={onDismiss} />);

      const dismissButton = screen.getByRole('button', { name: /dismiss message/i });
      expect(dismissButton).toHaveClass('opacity-70', 'hover:opacity-100');
    });
  });

  describe('Custom Styling', () => {
    it('should apply custom className', () => {
      render(<SimpleErrorMessage message="Error" className="mb-4 mt-2" />);
      const alert = screen.getByRole('alert');
      expect(alert).toHaveClass('mb-4', 'mt-2');
    });

    it('should preserve default classes when custom className is provided', () => {
      render(<SimpleErrorMessage message="Error" className="custom-class" />);
      const alert = screen.getByRole('alert');
      expect(alert).toHaveClass('custom-class');
      expect(alert.className).toMatch(/border/);
      expect(alert.className).toMatch(/px-4/);
      expect(alert.className).toMatch(/py-3/);
    });
  });

  describe('Content', () => {
    it('should render multiline messages correctly', () => {
      const multilineMessage = 'Error line 1\nError line 2\nError line 3';
      render(<SimpleErrorMessage message={multilineMessage} />);

      // Verify the message is in the document (as a single text node)
      const alert = screen.getByRole('alert');
      expect(alert).toHaveTextContent('Error line 1');
      expect(alert).toHaveTextContent('Error line 2');
      expect(alert).toHaveTextContent('Error line 3');
    });

    it('should render long messages correctly', () => {
      const longMessage = 'A'.repeat(500);
      render(<SimpleErrorMessage message={longMessage} />);

      expect(screen.getByText(longMessage)).toBeInTheDocument();
    });

    it('should render messages with special characters', () => {
      const specialMessage = 'Error: <script>alert("XSS")</script>';
      render(<SimpleErrorMessage message={specialMessage} />);

      // Should render as text, not execute script
      expect(screen.getByText(specialMessage)).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle rapid variant changes', () => {
      const { rerender } = render(<SimpleErrorMessage message="Error" variant="error" />);
      let alert = screen.getByRole('alert');
      expect(alert.className).toMatch(/bg-red-500\/10/);

      rerender(<SimpleErrorMessage message="Error" variant="warning" />);
      alert = screen.getByRole('alert');
      expect(alert.className).toMatch(/bg-yellow-500\/10/);

      rerender(<SimpleErrorMessage message="Error" variant="info" />);
      alert = screen.getByRole('alert');
      expect(alert.className).toMatch(/bg-blue-500\/10/);
    });

    it('should handle onDismiss being added/removed dynamically', () => {
      const onDismiss = jest.fn();
      const { rerender } = render(<SimpleErrorMessage message="Error" />);

      expect(screen.queryByRole('button')).not.toBeInTheDocument();

      rerender(<SimpleErrorMessage message="Error" onDismiss={onDismiss} />);
      expect(screen.getByRole('button')).toBeInTheDocument();

      rerender(<SimpleErrorMessage message="Error" />);
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });

    it('should handle message changes without unmounting', () => {
      const { rerender } = render(<SimpleErrorMessage message="First error" />);
      expect(screen.getByText('First error')).toBeInTheDocument();

      rerender(<SimpleErrorMessage message="Second error" />);
      expect(screen.queryByText('First error')).not.toBeInTheDocument();
      expect(screen.getByText('Second error')).toBeInTheDocument();
    });
  });

  describe('Type Safety', () => {
    it('should accept all valid props', () => {
      const props: SimpleErrorMessageProps = {
        message: 'Test',
        variant: 'error',
        onDismiss: jest.fn(),
        className: 'custom'
      };

      render(<SimpleErrorMessage {...props} />);
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    it('should work with minimal props', () => {
      const props: SimpleErrorMessageProps = {
        message: 'Test'
      };

      render(<SimpleErrorMessage {...props} />);
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });
});
