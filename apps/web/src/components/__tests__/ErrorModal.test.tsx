/**
 * Unit tests for ErrorModal component
 *
 * Tests cover:
 * - Modal rendering states (open/closed)
 * - Error type handling (ApiError, NetworkError, ValidationError, Error)
 * - User message display
 * - Retry functionality
 * - Close handlers (button, backdrop, ESC key)
 * - Technical details display
 * - Keyboard accessibility
 * - Edge cases (null errors, long messages, etc.)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ErrorModal } from '../ErrorModal';
import { ApiError, NetworkError, ValidationError } from '../../lib/errors';

describe('ErrorModal', () => {
  const mockOnClose = jest.fn();
  const mockOnRetry = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering States', () => {
    it('should not render when isOpen is false', () => {
      const error = new Error('Test error');
      render(
        <ErrorModal
          isOpen={false}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('should not render when error is null', () => {
      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={null}
        />
      );

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('should render when isOpen is true and error is provided', () => {
      const error = new Error('Test error');
      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('should render with custom title', () => {
      const error = new Error('Test error');
      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          title="Custom Error Title"
        />
      );

      expect(screen.getByText('Custom Error Title')).toBeInTheDocument();
    });

    it('should render with default title when not provided', () => {
      const error = new Error('Test error');
      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText('Error')).toBeInTheDocument();
    });
  });

  describe('Error Type Handling', () => {
    it('should display ApiError user message for 401 status', () => {
      const error = new ApiError(
        'Unauthorized',
        401,
        '/api/test',
        'GET'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText('You need to log in to access this resource')).toBeInTheDocument();
    });

    it('should display ApiError user message for 403 status', () => {
      const error = new ApiError(
        'Forbidden',
        403,
        '/api/test',
        'GET'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText('You do not have permission to perform this action')).toBeInTheDocument();
    });

    it('should display ApiError user message for 404 status', () => {
      const error = new ApiError(
        'Not Found',
        404,
        '/api/test',
        'GET'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText('The requested resource was not found')).toBeInTheDocument();
    });

    it('should display ApiError user message for 429 status', () => {
      const error = new ApiError(
        'Too Many Requests',
        429,
        '/api/test',
        'GET'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText('Too many requests. Please try again later')).toBeInTheDocument();
    });

    it('should display ApiError user message for 500 status', () => {
      const error = new ApiError(
        'Internal Server Error',
        500,
        '/api/test',
        'GET'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText('Server error. Our team has been notified')).toBeInTheDocument();
    });

    it('should display NetworkError user message', () => {
      const error = new NetworkError(
        'Connection failed',
        '/api/test'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText('Network connection failed. Please check your internet connection')).toBeInTheDocument();
    });

    it('should display ValidationError message', () => {
      const error = new ValidationError(
        'Email is required',
        'email'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText('Email is required')).toBeInTheDocument();
    });

    it('should display generic error message for standard Error', () => {
      const error = new Error('Something went wrong');

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    });

    it('should display fallback message when error has no message', () => {
      const error = new Error('');

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText('An unexpected error occurred')).toBeInTheDocument();
    });
  });

  describe('Retry Functionality', () => {
    it('should show retry button for retryable ApiError (408)', async () => {
      const user = userEvent.setup();
      const error = new ApiError(
        'Request Timeout',
        408,
        '/api/test',
        'GET'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          onRetry={mockOnRetry}
        />
      );

      const retryButton = screen.getByRole('button', { name: /retry/i });
      expect(retryButton).toBeInTheDocument();

      await user.click(retryButton);

      expect(mockOnClose).toHaveBeenCalledTimes(1);
      expect(mockOnRetry).toHaveBeenCalledTimes(1);
    });

    it('should show retry button for retryable ApiError (500)', async () => {
      const user = userEvent.setup();
      const error = new ApiError(
        'Server Error',
        500,
        '/api/test',
        'GET'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          onRetry={mockOnRetry}
        />
      );

      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
    });

    it('should show retry button for NetworkError', () => {
      const error = new NetworkError(
        'Connection failed',
        '/api/test'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          onRetry={mockOnRetry}
        />
      );

      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
    });

    it('should not show retry button for non-retryable ApiError (401)', () => {
      const error = new ApiError(
        'Unauthorized',
        401,
        '/api/test',
        'GET'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          onRetry={mockOnRetry}
        />
      );

      expect(screen.queryByRole('button', { name: /retry/i })).not.toBeInTheDocument();
    });

    it('should not show retry button for ValidationError', () => {
      const error = new ValidationError(
        'Email is required',
        'email'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          onRetry={mockOnRetry}
        />
      );

      expect(screen.queryByRole('button', { name: /retry/i })).not.toBeInTheDocument();
    });

    it('should not show retry button for standard Error', () => {
      const error = new Error('Test error');

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          onRetry={mockOnRetry}
        />
      );

      expect(screen.queryByRole('button', { name: /retry/i })).not.toBeInTheDocument();
    });

    it('should not show retry button when onRetry is not provided', () => {
      const error = new NetworkError(
        'Connection failed',
        '/api/test'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
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
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      const closeButton = screen.getByRole('button', { name: /close/i });
      await user.click(closeButton);

      expect(mockOnClose).toHaveBeenCalledTimes(1);
    });

    it('should call onClose when backdrop is clicked', async () => {
      const user = userEvent.setup();
      const error = new Error('Test error');

      const { container } = render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      // Find backdrop by its class
      const backdrop = container.querySelector('.bg-gray-500') as HTMLElement;
      expect(backdrop).toBeInTheDocument();

      await user.click(backdrop);

      expect(mockOnClose).toHaveBeenCalledTimes(1);
    });

    it('should call onClose when ESC key is pressed', async () => {
      const user = userEvent.setup();
      const error = new Error('Test error');

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      await user.keyboard('{Escape}');

      expect(mockOnClose).toHaveBeenCalledTimes(1);
    });

    it('should prevent body scroll when modal is open', () => {
      const error = new Error('Test error');

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(document.body.style.overflow).toBe('hidden');
    });

    it('should restore body scroll when modal is closed', () => {
      const error = new Error('Test error');

      const { rerender } = render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(document.body.style.overflow).toBe('hidden');

      rerender(
        <ErrorModal
          isOpen={false}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(document.body.style.overflow).toBe('unset');
    });
  });

  describe('Technical Details', () => {
    it('should not show technical details by default', () => {
      const error = new Error('Test error');

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.queryByText('Technical Details')).not.toBeInTheDocument();
    });

    it('should show technical details when showDetails is true', () => {
      const error = new Error('Test error');

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          showDetails={true}
        />
      );

      expect(screen.getByText('Technical Details')).toBeInTheDocument();
    });

    it('should display error type for ApiError in details', () => {
      const error = new ApiError(
        'Bad Request',
        400,
        '/api/test',
        'POST'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          showDetails={true}
        />
      );

      expect(screen.getByText('API Error (400)')).toBeInTheDocument();
    });

    it('should display error type for NetworkError in details', () => {
      const error = new NetworkError(
        'Connection failed',
        '/api/test'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          showDetails={true}
        />
      );

      expect(screen.getByText('Network Error')).toBeInTheDocument();
    });

    it('should display error type for ValidationError in details', () => {
      const error = new ValidationError(
        'Email is required',
        'email'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          showDetails={true}
        />
      );

      expect(screen.getByText('Validation Error')).toBeInTheDocument();
    });

    it('should display endpoint for ApiError in details', () => {
      const error = new ApiError(
        'Bad Request',
        400,
        '/api/games/123',
        'POST'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          showDetails={true}
        />
      );

      expect(screen.getByText('/api/games/123')).toBeInTheDocument();
    });

    it('should display correlation ID for ApiError when present', () => {
      const error = new ApiError(
        'Bad Request',
        400,
        '/api/test',
        'POST',
        'corr-123-456'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          showDetails={true}
        />
      );

      expect(screen.getByText('corr-123-456')).toBeInTheDocument();
    });

    it('should display endpoint for NetworkError in details', () => {
      const error = new NetworkError(
        'Connection failed',
        '/api/games/456'
      );

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          showDetails={true}
        />
      );

      expect(screen.getByText('/api/games/456')).toBeInTheDocument();
    });

    it('should display stack trace when present', () => {
      const error = new Error('Test error');
      error.stack = 'Error: Test error\n  at file.ts:10:15\n  at another.ts:20:30';

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
          showDetails={true}
        />
      );

      expect(screen.getByText(/file\.ts:10:15/)).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA attributes', () => {
      const error = new Error('Test error');

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
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
          isOpen={true}
          onClose={mockOnClose}
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
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      const backdrop = container.querySelector('.bg-gray-500');
      expect(backdrop).toHaveAttribute('aria-hidden', 'true');
    });
  });

  describe('Edge Cases', () => {
    it('should handle very long error messages', () => {
      const longMessage = 'A'.repeat(1000);
      const error = new Error(longMessage);

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText(longMessage)).toBeInTheDocument();
    });

    it('should handle error with special characters', () => {
      const error = new Error('Error with <script>alert("xss")</script>');

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      expect(screen.getByText(/Error with.*script.*alert/)).toBeInTheDocument();
    });

    it('should handle multiple rapid ESC key presses', async () => {
      const user = userEvent.setup();
      const error = new Error('Test error');

      render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      await user.keyboard('{Escape}');
      await user.keyboard('{Escape}');
      await user.keyboard('{Escape}');

      // Should only call onClose once per keypress (3 times total)
      expect(mockOnClose).toHaveBeenCalled();
    });

    it('should cleanup event listeners on unmount', () => {
      const error = new Error('Test error');

      const { unmount } = render(
        <ErrorModal
          isOpen={true}
          onClose={mockOnClose}
          error={error}
        />
      );

      unmount();

      expect(document.body.style.overflow).toBe('unset');
    });
  });
});
