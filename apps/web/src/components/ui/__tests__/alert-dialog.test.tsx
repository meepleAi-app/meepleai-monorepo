/**
 * AlertDialog component tests
 * Follow-up to Issue #1435 - Replace window.alert with custom dialog
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { AlertDialog } from '../alert-dialog';

describe('AlertDialog', () => {
  const defaultProps = {
    open: true,
    onOpenChange: jest.fn(),
    title: 'Test Alert',
    message: 'This is a test message',
    onClose: jest.fn(),
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should render with title and message', () => {
    render(<AlertDialog {...defaultProps} />);

    expect(screen.getByText('Test Alert')).toBeInTheDocument();
    expect(screen.getByText('This is a test message')).toBeInTheDocument();
  });

  it('should render default OK button', () => {
    render(<AlertDialog {...defaultProps} />);

    expect(screen.getByRole('button', { name: /ok/i })).toBeInTheDocument();
  });

  it('should render custom button text', () => {
    render(<AlertDialog {...defaultProps} buttonText="Got it" />);

    expect(screen.getByRole('button', { name: /got it/i })).toBeInTheDocument();
  });

  it('should call onClose and onOpenChange when button is clicked', () => {
    render(<AlertDialog {...defaultProps} />);

    const button = screen.getByRole('button', { name: /ok/i });
    fireEvent.click(button);

    expect(defaultProps.onClose).toHaveBeenCalledTimes(1);
    expect(defaultProps.onOpenChange).toHaveBeenCalledWith(false);
  });

  it('should render info variant with blue icon', () => {
    render(<AlertDialog {...defaultProps} variant="info" />);

    const icon = screen.getByText('Test Alert').parentElement?.querySelector('svg');
    expect(icon).toBeInTheDocument();
    expect(icon).toHaveClass('text-blue-600');
  });

  it('should render success variant with green icon', () => {
    render(<AlertDialog {...defaultProps} variant="success" />);

    const icon = screen.getByText('Test Alert').parentElement?.querySelector('svg');
    expect(icon).toBeInTheDocument();
    expect(icon).toHaveClass('text-green-600');
  });

  it('should render warning variant with yellow icon', () => {
    render(<AlertDialog {...defaultProps} variant="warning" />);

    const icon = screen.getByText('Test Alert').parentElement?.querySelector('svg');
    expect(icon).toBeInTheDocument();
    expect(icon).toHaveClass('text-yellow-600');
  });

  it('should render error variant with red icon', () => {
    render(<AlertDialog {...defaultProps} variant="error" />);

    const icon = screen.getByText('Test Alert').parentElement?.querySelector('svg');
    expect(icon).toBeInTheDocument();
    expect(icon).toHaveClass('text-red-600');
  });

  it('should render loading variant with spinner icon', () => {
    render(<AlertDialog {...defaultProps} variant="loading" />);

    const icon = screen.getByText('Test Alert').parentElement?.querySelector('svg');
    expect(icon).toBeInTheDocument();
    expect(icon).toHaveClass('text-gray-600');
    expect(icon).toHaveClass('animate-spin');
  });

  it('should not render when open is false', () => {
    render(<AlertDialog {...defaultProps} open={false} />);

    expect(screen.queryByText('Test Alert')).not.toBeInTheDocument();
  });

  it('should handle keyboard navigation (Escape key)', async () => {
    render(<AlertDialog {...defaultProps} />);

    const dialog = screen.getByRole('dialog');
    fireEvent.keyDown(dialog, { key: 'Escape', code: 'Escape' });

    await waitFor(() => {
      expect(defaultProps.onOpenChange).toHaveBeenCalledWith(false);
    });
  });

  it('should work without onClose callback', () => {
    const propsWithoutOnClose = {
      ...defaultProps,
      onClose: undefined,
    };

    render(<AlertDialog {...propsWithoutOnClose} />);

    const button = screen.getByRole('button', { name: /ok/i });
    fireEvent.click(button);

    // Should still close the dialog
    expect(defaultProps.onOpenChange).toHaveBeenCalledWith(false);
    // Should not throw error
  });

  it('should be accessible with proper ARIA attributes', () => {
    render(<AlertDialog {...defaultProps} />);

    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();

    const title = screen.getByText('Test Alert');
    expect(title).toBeInTheDocument();

    const description = screen.getByText('This is a test message');
    expect(description).toBeInTheDocument();
  });

  it('should have accessible icon with aria-label', () => {
    render(<AlertDialog {...defaultProps} variant="success" />);

    const iconContainer = screen.getByLabelText('Success icon');
    expect(iconContainer).toBeInTheDocument();
    expect(iconContainer).toHaveAttribute('role', 'img');
  });

  it('should have correct aria-label for each variant', () => {
    const variants: Array<{ variant: 'info' | 'success' | 'warning' | 'error' | 'loading'; label: string }> = [
      { variant: 'info', label: 'Information icon' },
      { variant: 'success', label: 'Success icon' },
      { variant: 'warning', label: 'Warning icon' },
      { variant: 'error', label: 'Error icon' },
      { variant: 'loading', label: 'Loading icon' },
    ];

    variants.forEach(({ variant, label }) => {
      const { unmount } = render(<AlertDialog {...defaultProps} variant={variant} />);
      const iconContainer = screen.getByLabelText(label);
      expect(iconContainer).toBeInTheDocument();
      unmount();
    });
  });

  it('should autofocus the OK button', () => {
    render(<AlertDialog {...defaultProps} />);

    const button = screen.getByRole('button', { name: /ok/i });
    expect(button).toHaveAttribute('autoFocus');
  });

  it('should handle long messages properly', () => {
    const longMessage = 'This is a very long message that should still be displayed properly without breaking the layout. '.repeat(10);

    render(<AlertDialog {...defaultProps} message={longMessage} />);

    expect(screen.getByText(longMessage)).toBeInTheDocument();
  });
});
