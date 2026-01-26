/**
 * Unit tests for ErrorDisplay component (PDF-06)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { ErrorDisplay } from '../errors/ErrorDisplay';
import { ErrorCategory, type CategorizedError } from '../../lib/errorUtils';

// Mock clipboard API
Object.assign(navigator, {
  clipboard: {
    writeText: vi.fn().mockResolvedValue(undefined)
  }
});

describe('ErrorDisplay', () => {
  const mockOnRetry = vi.fn();
  const mockOnDismiss = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Basic rendering', () => {
    it('should render error message', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Network,
        message: 'Connection lost',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} />);

      expect(screen.getByText('Connection lost')).toBeInTheDocument();
    });

    it('should render error title based on category', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Server,
        message: 'Server error',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} />);

      expect(screen.getByText('Upload Failed')).toBeInTheDocument();
    });

    it('should render error icon', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Validation,
        message: 'Invalid file',
        canRetry: false,
        suggestions: []
      };

      render(<ErrorDisplay error={error} />);

      expect(screen.getByText('❌')).toBeInTheDocument();
    });
  });

  describe('Suggestions', () => {
    it('should render suggestions list', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Processing,
        message: 'Unable to process PDF',
        canRetry: false,
        suggestions: [
          'Re-download the original file',
          'Convert to standard PDF format',
          'Remove password protection'
        ]
      };

      render(<ErrorDisplay error={error} />);

      expect(screen.getByTestId('suggestions-title')).toBeInTheDocument();
      expect(screen.getByText('Re-download the original file')).toBeInTheDocument();
      expect(screen.getByText('Convert to standard PDF format')).toBeInTheDocument();
      expect(screen.getByText('Remove password protection')).toBeInTheDocument();
    });

    it('should not render suggestions section when empty', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Unknown,
        message: 'Unknown error',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} />);

      expect(screen.queryByTestId('suggestions-title')).not.toBeInTheDocument();
    });
  });

  describe('Correlation ID', () => {
    it('should display correlation ID when provided', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Server,
        message: 'Server error',
        correlationId: 'test-correlation-123',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} />);

      expect(screen.getByTestId('error-id-label')).toBeInTheDocument();
      expect(screen.getByText('test-correlation-123')).toBeInTheDocument();
    });

    it('should not display correlation ID section when not provided', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Network,
        message: 'Network error',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} />);

      expect(screen.queryByTestId('error-id-label')).not.toBeInTheDocument();
    });

    it('should copy correlation ID to clipboard when Copy button clicked', async () => {
      const error: CategorizedError = {
        category: ErrorCategory.Server,
        message: 'Server error',
        correlationId: 'copy-test-123',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} />);

      const copyButton = screen.getByTestId('copy-error-id-button');
      fireEvent.click(copyButton);

      expect(navigator.clipboard.writeText).toHaveBeenCalledWith('copy-test-123');
    });
  });

  describe('Retry button', () => {
    it('should show retry button when error is retryable and onRetry provided', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Network,
        message: 'Network error',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} onRetry={mockOnRetry} />);

      expect(screen.getByTestId('retry-button')).toBeInTheDocument();
    });

    it('should not show retry button when error is not retryable', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Validation,
        message: 'Validation error',
        canRetry: false,
        suggestions: []
      };

      render(<ErrorDisplay error={error} onRetry={mockOnRetry} />);

      expect(screen.queryByTestId('retry-button')).not.toBeInTheDocument();
    });

    it('should not show retry button when onRetry not provided', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Network,
        message: 'Network error',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} />);

      expect(screen.queryByTestId('retry-button')).not.toBeInTheDocument();
    });

    it('should call onRetry when retry button clicked', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Server,
        message: 'Server error',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} onRetry={mockOnRetry} />);

      const retryButton = screen.getByTestId('retry-button');
      fireEvent.click(retryButton);

      expect(mockOnRetry).toHaveBeenCalledTimes(1);
    });
  });

  describe('Dismiss button', () => {
    it('should show dismiss button when onDismiss provided', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Unknown,
        message: 'Error',
        canRetry: false,
        suggestions: []
      };

      render(<ErrorDisplay error={error} onDismiss={mockOnDismiss} />);

      expect(screen.getByTestId('dismiss-button')).toHaveTextContent('Go Back');
    });

    it('should show Cancel text when error is retryable', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Network,
        message: 'Network error',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} onDismiss={mockOnDismiss} />);

      expect(screen.getByTestId('dismiss-button')).toHaveTextContent('Cancel');
    });

    it('should call onDismiss when dismiss button clicked', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Validation,
        message: 'Validation error',
        canRetry: false,
        suggestions: []
      };

      render(<ErrorDisplay error={error} onDismiss={mockOnDismiss} />);

      const dismissButton = screen.getByTestId('dismiss-button');
      fireEvent.click(dismissButton);

      expect(mockOnDismiss).toHaveBeenCalledTimes(1);
    });
  });

  describe('Technical details', () => {
    it('should show technical details button when showTechnicalDetails is true', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Server,
        message: 'Server error',
        technicalMessage: 'Stack trace here',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} showTechnicalDetails={true} />);

      expect(screen.getByText('Show Technical Details')).toBeInTheDocument();
    });

    it('should not show technical details button when showTechnicalDetails is false', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Server,
        message: 'Server error',
        technicalMessage: 'Stack trace here',
        canRetry: true,
        suggestions: []
      };

      render(<ErrorDisplay error={error} showTechnicalDetails={false} />);

      expect(screen.queryByText('Show Technical Details')).not.toBeInTheDocument();
    });

    it('should toggle technical details when button clicked', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Processing,
        message: 'Processing error',
        technicalMessage: 'Detailed error: Invalid PDF structure',
        statusCode: 500,
        canRetry: false,
        suggestions: []
      };

      render(<ErrorDisplay error={error} showTechnicalDetails={true} />);

      // Initially hidden
      expect(screen.queryByText('Detailed error: Invalid PDF structure')).not.toBeInTheDocument();

      // Show details
      const showButton = screen.getByText('Show Technical Details');
      fireEvent.click(showButton);

      expect(screen.getByText(/Detailed error: Invalid PDF structure/)).toBeInTheDocument();
      expect(screen.getByText(/Status Code:/)).toBeInTheDocument();
      expect(screen.getByText(/500/)).toBeInTheDocument();

      // Hide details
      const hideButton = screen.getByText('Hide Technical Details');
      fireEvent.click(hideButton);

      expect(screen.queryByText('Detailed error: Invalid PDF structure')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have role="alert" for screen readers', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Network,
        message: 'Network error',
        canRetry: true,
        suggestions: []
      };

      const { container } = render(<ErrorDisplay error={error} />);

      const alert = container.querySelector('[role="alert"]');
      expect(alert).toBeInTheDocument();
    });

    it('should have aria-live="assertive"', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Server,
        message: 'Server error',
        canRetry: true,
        suggestions: []
      };

      const { container } = render(<ErrorDisplay error={error} />);

      const alert = container.querySelector('[aria-live="assertive"]');
      expect(alert).toBeInTheDocument();
    });
  });

  describe('Different error categories', () => {
    it('should render validation error correctly', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Validation,
        message: 'File is too large',
        canRetry: false,
        suggestions: ['Compress the PDF', 'Split into smaller files']
      };

      render(<ErrorDisplay error={error} />);

      expect(screen.getByText('Invalid File')).toBeInTheDocument();
      expect(screen.getByText('File is too large')).toBeInTheDocument();
    });

    it('should render network error correctly', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Network,
        message: 'Connection lost',
        canRetry: true,
        suggestions: ['Check your internet connection']
      };

      render(<ErrorDisplay error={error} onRetry={mockOnRetry} />);

      expect(screen.getByText('Connection Lost')).toBeInTheDocument();
      expect(screen.getByTestId('retry-button')).toBeInTheDocument();
    });

    it('should render processing error correctly', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Processing,
        message: 'Unable to process PDF',
        canRetry: false,
        suggestions: ['Ensure file is not corrupted']
      };

      render(<ErrorDisplay error={error} />);

      expect(screen.getByText('Unable to Process PDF')).toBeInTheDocument();
    });
  });
});
