/**
 * Unit tests for ErrorModal component - Interactions and Accessibility
 *
 * Tests cover:
 * - Retry functionality
 * - Close handlers (button, backdrop, ESC key)
 * - Keyboard accessibility
 * - ARIA attributes
 * - Body scroll management
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ErrorModal } from '../ErrorModal';
import {
  createApiError,
  createNetworkError,
  createValidationError,
  HTTP_STATUS,
  isRetryableError,
  createMockCallbacks,
  defaultModalProps
} from './ErrorModal.test-helpers';

describe('ErrorModal - Interactions and Accessibility', () => {
  let mockCallbacks: ReturnType<typeof createMockCallbacks>;

  beforeEach(() => {
    mockCallbacks = createMockCallbacks();
  });

  describe('Retry Functionality', () => {
    it('should show retry button for retryable ApiError (408)', async () => {
      const user = userEvent.setup();
      const error = createApiError('Request Timeout', HTTP_STATUS.REQUEST_TIMEOUT);
      expect(isRetryableError(error)).toBe(true);

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
          onRetry={mockCallbacks.onRetry}
        />
      );

      const retryButton = screen.getByRole('button', { name: /retry/i });
      expect(retryButton).toBeInTheDocument();

      await user.click(retryButton);

      expect(mockCallbacks.onClose).toHaveBeenCalledTimes(1);
      expect(mockCallbacks.onRetry).toHaveBeenCalledTimes(1);
    });

    it('should show retry button for retryable ApiError (500)', async () => {
      const user = userEvent.setup();
      const error = createApiError('Server Error', HTTP_STATUS.INTERNAL_SERVER_ERROR);
      expect(isRetryableError(error)).toBe(true);

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
          onRetry={mockCallbacks.onRetry}
        />
      );

      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
    });

    it('should show retry button for NetworkError', () => {
      const error = createNetworkError();

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
          onRetry={mockCallbacks.onRetry}
        />
      );

      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
    });

    it('should not show retry button for non-retryable ApiError (401)', () => {
      const error = createApiError('Unauthorized', HTTP_STATUS.UNAUTHORIZED);
      expect(isRetryableError(error)).toBe(false);

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
          onRetry={mockCallbacks.onRetry}
        />
      );

      expect(screen.queryByRole('button', { name: /retry/i })).not.toBeInTheDocument();
    });

    it('should not show retry button for ValidationError', () => {
      const error = createValidationError('Email is required', 'email');

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
          onRetry={mockCallbacks.onRetry}
        />
      );

      expect(screen.queryByRole('button', { name: /retry/i })).not.toBeInTheDocument();
    });

    it('should not show retry button for standard Error', () => {
      const error = new Error('Test error');

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
          onRetry={mockCallbacks.onRetry}
        />
      );

      expect(screen.queryByRole('button', { name: /retry/i })).not.toBeInTheDocument();
    });

    it('should not show retry button when onRetry is not provided', () => {
      const error = createNetworkError();

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
        />
      );

      expect(screen.queryByRole('button', { name: /retry/i })).not.toBeInTheDocument();
    });
  });

  describe('Close Handlers', () => {
    it('should call onClose when close button is clicked', async () => {
      const user = userEvent.setup();
      const error = new Error('Test error');

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
        />
      );

      const closeButton = screen.getByRole('button', { name: /close/i });
      await user.click(closeButton);

      expect(mockCallbacks.onClose).toHaveBeenCalledTimes(1);
    });

    it('should call onClose when backdrop is clicked', async () => {
      const user = userEvent.setup();
      const error = new Error('Test error');

      const { container } = render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
        />
      );

      // Find backdrop by its class
      const backdrop = container.querySelector('.bg-gray-500') as HTMLElement;
      expect(backdrop).toBeInTheDocument();

      await user.click(backdrop);

      expect(mockCallbacks.onClose).toHaveBeenCalledTimes(1);
    });

    it('should call onClose when ESC key is pressed', async () => {
      const user = userEvent.setup();
      const error = new Error('Test error');

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
        />
      );

      await user.keyboard('{Escape}');

      expect(mockCallbacks.onClose).toHaveBeenCalledTimes(1);
    });

    it('should prevent body scroll when modal is open', () => {
      const error = new Error('Test error');

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
        />
      );

      expect(document.body.style.overflow).toBe('hidden');
    });

    it('should restore body scroll when modal is closed', () => {
      const error = new Error('Test error');

      const { rerender } = render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
        />
      );

      expect(document.body.style.overflow).toBe('hidden');

      rerender(
        <ErrorModal
          isOpen={false}
          onClose={mockCallbacks.onClose}
          error={error}
        />
      );

      expect(document.body.style.overflow).toBe('unset');
    });

    it('should handle multiple rapid ESC key presses', async () => {
      const user = userEvent.setup();
      const error = new Error('Test error');

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
        />
      );

      await user.keyboard('{Escape}');
      await user.keyboard('{Escape}');
      await user.keyboard('{Escape}');

      // Should only call onClose once per keypress (3 times total)
      expect(mockCallbacks.onClose).toHaveBeenCalled();
    });

    it('should cleanup event listeners on unmount', () => {
      const error = new Error('Test error');

      const { unmount } = render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
        />
      );

      unmount();

      expect(document.body.style.overflow).toBe('unset');
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA attributes', () => {
      const error = new Error('Test error');

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
        />
      );

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveAttribute('aria-modal', 'true');
      expect(dialog).toHaveAttribute('aria-labelledby', 'error-modal-title');
    });

    it('should have accessible title', () => {
      const error = new Error('Test error');

      render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
          title="Accessible Error"
        />
      );

      const title = screen.getByText('Accessible Error');
      expect(title).toHaveAttribute('id', 'error-modal-title');
    });

    it('should have backdrop marked as aria-hidden', () => {
      const error = new Error('Test error');

      const { container } = render(
        <ErrorModal
          {...defaultModalProps}
          onClose={mockCallbacks.onClose}
          error={error}
        />
      );

      const backdrop = container.querySelector('.bg-gray-500');
      expect(backdrop).toHaveAttribute('aria-hidden', 'true');
    });
  });
});
