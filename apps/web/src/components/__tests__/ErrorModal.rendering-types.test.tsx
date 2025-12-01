/**
 * Unit tests for ErrorModal component - Rendering and Error Types
 *
 * Tests cover:
 * - Modal rendering states (open/closed)
 * - Error type handling (ApiError, NetworkError, ValidationError, Error)
 * - User message display
 * - Technical details display
 * - Edge cases (null errors, long messages, etc.)
 */

import { render, screen } from '@testing-library/react';
import { ErrorModal } from '../modals/ErrorModal';
import {
  createApiError,
  createNetworkError,
  createValidationError,
  HTTP_STATUS,
  USER_MESSAGES,
  defaultModalProps,
} from './ErrorModal.test-helpers';

describe('ErrorModal - Rendering and Error Types', () => {
  const mockOnClose = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering States', () => {
    it('should not render when isOpen is false', () => {
      const error = new Error('Test error');
      render(<ErrorModal isOpen={false} onClose={mockOnClose} error={error} />);

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('should not render when error is null', () => {
      render(<ErrorModal isOpen={true} onClose={mockOnClose} error={null} />);

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('should render when isOpen is true and error is provided', () => {
      const error = new Error('Test error');
      render(<ErrorModal isOpen={true} onClose={mockOnClose} error={error} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('should render with custom title', () => {
      const error = new Error('Test error');
      render(
        <ErrorModal isOpen={true} onClose={mockOnClose} error={error} title="Custom Error Title" />
      );

      expect(screen.getByText('Custom Error Title')).toBeInTheDocument();
    });

    it('should render with default title when not provided', () => {
      const error = new Error('Test error');
      render(<ErrorModal isOpen={true} onClose={mockOnClose} error={error} />);

      expect(screen.getByText('Error')).toBeInTheDocument();
    });
  });

  describe('Error Type Handling - ApiError', () => {
    it('should display ApiError user message for 401 status', () => {
      const error = createApiError('Unauthorized', HTTP_STATUS.UNAUTHORIZED);

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.getByText(USER_MESSAGES.UNAUTHORIZED)).toBeInTheDocument();
    });

    it('should display ApiError user message for 403 status', () => {
      const error = createApiError('Forbidden', HTTP_STATUS.FORBIDDEN);

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.getByText(USER_MESSAGES.FORBIDDEN)).toBeInTheDocument();
    });

    it('should display ApiError user message for 404 status', () => {
      const error = createApiError('Not Found', HTTP_STATUS.NOT_FOUND);

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.getByText(USER_MESSAGES.NOT_FOUND)).toBeInTheDocument();
    });

    it('should display ApiError user message for 429 status', () => {
      const error = createApiError('Too Many Requests', HTTP_STATUS.TOO_MANY_REQUESTS);

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.getByText(USER_MESSAGES.TOO_MANY_REQUESTS)).toBeInTheDocument();
    });

    it('should display ApiError user message for 500 status', () => {
      const error = createApiError('Internal Server Error', HTTP_STATUS.INTERNAL_SERVER_ERROR);

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.getByText(USER_MESSAGES.SERVER_ERROR)).toBeInTheDocument();
    });
  });

  describe('Error Type Handling - Other Types', () => {
    it('should display NetworkError user message', () => {
      const error = createNetworkError();

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.getByText(USER_MESSAGES.NETWORK_ERROR)).toBeInTheDocument();
    });

    it('should display ValidationError message', () => {
      const error = createValidationError('Email is required', 'email');

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.getByText('Email is required')).toBeInTheDocument();
    });

    it('should display generic error message for standard Error', () => {
      const error = new Error('Something went wrong');

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    });

    it('should display fallback message when error has no message', () => {
      const error = new Error('');

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.getByText(USER_MESSAGES.GENERIC_ERROR)).toBeInTheDocument();
    });
  });

  describe('Technical Details', () => {
    it('should not show technical details by default', () => {
      const error = new Error('Test error');

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.queryByText('Technical Details')).not.toBeInTheDocument();
    });

    it('should show technical details when showDetails is true', () => {
      const error = new Error('Test error');

      render(
        <ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} showDetails={true} />
      );

      expect(screen.getByText('Technical Details')).toBeInTheDocument();
    });

    it('should display error type for ApiError in details', () => {
      const error = createApiError('Bad Request', HTTP_STATUS.BAD_REQUEST, '/api/test', 'POST');

      render(
        <ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} showDetails={true} />
      );

      expect(screen.getByText('API Error (400)')).toBeInTheDocument();
    });

    it('should display error type for NetworkError in details', () => {
      const error = createNetworkError();

      render(
        <ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} showDetails={true} />
      );

      expect(screen.getByText('Network Error')).toBeInTheDocument();
    });

    it('should display error type for ValidationError in details', () => {
      const error = createValidationError('Email is required', 'email');

      render(
        <ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} showDetails={true} />
      );

      expect(screen.getByText('Validation Error')).toBeInTheDocument();
    });

    it('should display endpoint for ApiError in details', () => {
      const error = createApiError(
        'Bad Request',
        HTTP_STATUS.BAD_REQUEST,
        '/api/games/123',
        'POST'
      );

      render(
        <ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} showDetails={true} />
      );

      expect(screen.getByText('/api/games/123')).toBeInTheDocument();
    });

    it('should display correlation ID for ApiError when present', () => {
      const error = createApiError(
        'Bad Request',
        HTTP_STATUS.BAD_REQUEST,
        '/api/test',
        'POST',
        'corr-123-456'
      );

      render(
        <ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} showDetails={true} />
      );

      expect(screen.getByText('corr-123-456')).toBeInTheDocument();
    });

    it('should display endpoint for NetworkError in details', () => {
      const error = createNetworkError('Connection failed', '/api/games/456');

      render(
        <ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} showDetails={true} />
      );

      expect(screen.getByText('/api/games/456')).toBeInTheDocument();
    });

    it('should display stack trace when present', () => {
      const error = new Error('Test error');
      error.stack = 'Error: Test error\n  at file.ts:10:15\n  at another.ts:20:30';

      render(
        <ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} showDetails={true} />
      );

      expect(screen.getByText(/file\.ts:10:15/)).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle very long error messages', () => {
      const longMessage = 'A'.repeat(1000);
      const error = new Error(longMessage);

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.getByText(longMessage)).toBeInTheDocument();
    });

    it('should handle error with special characters', () => {
      const error = new Error('Error with <script>alert("xss")</script>');

      render(<ErrorModal {...defaultModalProps} onClose={mockOnClose} error={error} />);

      expect(screen.getByText(/Error with.*script.*alert/)).toBeInTheDocument();
    });
  });
});
