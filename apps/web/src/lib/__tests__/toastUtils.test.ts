/**
 * Tests for toast utilities
 */

import { toast } from 'sonner';
import {
  shouldShowToast,
  showErrorToast,
  showSuccessToast,
  showInfoToast,
  showWarningToast,
  TOAST_CONFIG
} from '../toastUtils';
import { ErrorCategory, type CategorizedError } from '../errorUtils';

// Mock sonner
vi.mock('sonner', () => ({
  toast: {
    error: vi.fn(),
    warning: vi.fn(),
    success: vi.fn(),
    info: vi.fn()
  }
}));

describe('toastUtils', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('shouldShowToast', () => {
    it('returns true for network errors that can be retried', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Network,
        message: 'Network error',
        canRetry: true,
        suggestions: [],
        correlationId: 'test-123'
      };

      expect(shouldShowToast(error)).toBe(true);
    });

    it('returns true for server errors that can be retried', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Server,
        message: 'Server error',
        canRetry: true,
        suggestions: [],
        statusCode: 500
      };

      expect(shouldShowToast(error)).toBe(true);
    });

    it('returns false for validation errors', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Validation,
        message: 'Validation error',
        canRetry: false,
        suggestions: []
      };

      expect(shouldShowToast(error)).toBe(false);
    });

    it('returns false for processing errors', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Processing,
        message: 'Processing error',
        canRetry: false,
        suggestions: []
      };

      expect(shouldShowToast(error)).toBe(false);
    });

    it('returns false for non-retryable network errors', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Network,
        message: 'Network error',
        canRetry: false,
        suggestions: []
      };

      expect(shouldShowToast(error)).toBe(false);
    });
  });

  describe('showErrorToast', () => {
    it('shows warning toast for network errors', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Network,
        message: 'Connection lost',
        canRetry: true,
        suggestions: [],
        correlationId: 'net-123'
      };

      showErrorToast(error);

      expect(toast.warning).toHaveBeenCalledWith(
        'Connection lost',
        expect.objectContaining({
          description: 'Error ID: net-123',
          duration: TOAST_CONFIG.duration.transient
        })
      );
    });

    it('shows warning toast for rate limit errors (429)', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Server,
        message: 'Too many requests',
        statusCode: 429,
        canRetry: true,
        suggestions: []
      };

      showErrorToast(error);

      expect(toast.warning).toHaveBeenCalledWith(
        'Rate limit exceeded',
        expect.objectContaining({
          description: 'Please wait a moment before trying again.'
        })
      );
    });

    it('shows error toast for server errors', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Server,
        message: 'Internal server error',
        statusCode: 500,
        canRetry: true,
        suggestions: [],
        correlationId: 'srv-456'
      };

      showErrorToast(error);

      expect(toast.error).toHaveBeenCalledWith(
        'Internal server error',
        expect.objectContaining({
          description: 'Error ID: srv-456'
        })
      );
    });

    it('shows error toast for validation errors with persistent duration', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Validation,
        message: 'Invalid input',
        canRetry: false,
        suggestions: ['Check file size', 'Verify format']
      };

      showErrorToast(error);

      expect(toast.error).toHaveBeenCalledWith(
        'Invalid input',
        expect.objectContaining({
          description: 'Check file size',
          duration: TOAST_CONFIG.duration.persistent
        })
      );
    });

    it('shows error toast for processing errors', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Processing,
        message: 'Failed to process file',
        canRetry: false,
        suggestions: []
      };

      showErrorToast(error);

      expect(toast.error).toHaveBeenCalledWith(
        'Failed to process file',
        expect.objectContaining({
          description: 'Please check the file and try again.'
        })
      );
    });

    it('shows error toast for unknown errors', () => {
      const error: CategorizedError = {
        category: ErrorCategory.Unknown,
        message: 'Unknown error',
        canRetry: true,
        suggestions: [],
        correlationId: 'unk-789'
      };

      showErrorToast(error);

      expect(toast.error).toHaveBeenCalledWith(
        'An unexpected error occurred',
        expect.objectContaining({
          description: 'Error ID: unk-789'
        })
      );
    });
  });

  describe('showSuccessToast', () => {
    it('shows success toast with transient duration', () => {
      showSuccessToast('Operation successful', 'File uploaded');

      expect(toast.success).toHaveBeenCalledWith(
        'Operation successful',
        {
          description: 'File uploaded',
          duration: TOAST_CONFIG.duration.transient
        }
      );
    });

    it('shows success toast without description', () => {
      showSuccessToast('Success');

      expect(toast.success).toHaveBeenCalledWith(
        'Success',
        {
          description: undefined,
          duration: TOAST_CONFIG.duration.transient
        }
      );
    });
  });

  describe('showInfoToast', () => {
    it('shows info toast with transient duration', () => {
      showInfoToast('Processing started', 'Please wait...');

      expect(toast.info).toHaveBeenCalledWith(
        'Processing started',
        {
          description: 'Please wait...',
          duration: TOAST_CONFIG.duration.transient
        }
      );
    });
  });

  describe('showWarningToast', () => {
    it('shows warning toast with transient duration', () => {
      showWarningToast('File size large', 'This may take longer');

      expect(toast.warning).toHaveBeenCalledWith(
        'File size large',
        {
          description: 'This may take longer',
          duration: TOAST_CONFIG.duration.transient
        }
      );
    });
  });

  describe('TOAST_CONFIG', () => {
    it('has correct configuration values', () => {
      expect(TOAST_CONFIG.duration.transient).toBe(5000);
      expect(TOAST_CONFIG.duration.persistent).toBe(Infinity);
      expect(TOAST_CONFIG.position).toBe('top-right');
    });
  });
});
