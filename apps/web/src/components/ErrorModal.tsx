/**
 * Error modal component for displaying detailed error information
 */

import React, { useEffect } from 'react';
import { ApiError, NetworkError, ValidationError } from '../lib/errors';

export interface ErrorModalProps {
  isOpen: boolean;
  onClose: () => void;
  error: Error | null;
  title?: string;
  showDetails?: boolean;
  onRetry?: () => void;
}

/**
 * Modal for displaying error details with retry option
 */
export function ErrorModal({
  isOpen,
  onClose,
  error,
  title = 'Error',
  showDetails = false,
  onRetry
}: ErrorModalProps) {
  useEffect(() => {
    if (isOpen) {
      // Prevent body scroll when modal is open
      document.body.style.overflow = 'hidden';

      // Handle escape key
      const handleEscape = (e: KeyboardEvent) => {
        if (e.key === 'Escape') {
          onClose();
        }
      };

      document.addEventListener('keydown', handleEscape);

      return () => {
        document.body.style.overflow = 'unset';
        document.removeEventListener('keydown', handleEscape);
      };
    }
  }, [isOpen, onClose]);

  if (!isOpen || !error) {
    return null;
  }

  const getUserMessage = (): string => {
    if (error instanceof ApiError) {
      return error.getUserMessage();
    }
    if (error instanceof NetworkError) {
      return error.getUserMessage();
    }
    if (error instanceof ValidationError) {
      return error.getUserMessage();
    }
    return error.message || 'An unexpected error occurred';
  };

  const canRetry = (): boolean => {
    if (error instanceof ApiError) {
      return error.isRetryable();
    }
    if (error instanceof NetworkError) {
      return true;
    }
    return false;
  };

  const getErrorType = (): string => {
    if (error instanceof ApiError) {
      return `API Error (${error.statusCode})`;
    }
    if (error instanceof NetworkError) {
      return 'Network Error';
    }
    if (error instanceof ValidationError) {
      return 'Validation Error';
    }
    return 'Error';
  };

  return (
    <div
      className="fixed inset-0 z-50 overflow-y-auto"
      aria-labelledby="error-modal-title"
      role="dialog"
      aria-modal="true"
    >
      {/* Backdrop */}
      <div
        className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
        aria-hidden="true"
        onClick={onClose}
      />

      {/* Modal */}
      <div className="flex items-center justify-center min-h-screen px-4 pt-4 pb-20 text-center sm:block sm:p-0">
        {/* Centering trick */}
        <span className="hidden sm:inline-block sm:align-middle sm:h-screen" aria-hidden="true">
          &#8203;
        </span>

        <div className="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full">
          <div className="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <div className="sm:flex sm:items-start">
              <div className="mx-auto flex-shrink-0 flex items-center justify-center h-12 w-12 rounded-full bg-red-100 sm:mx-0 sm:h-10 sm:w-10">
                <svg
                  className="h-6 w-6 text-red-600"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                  aria-hidden="true"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                  />
                </svg>
              </div>
              <div className="mt-3 text-center sm:mt-0 sm:ml-4 sm:text-left flex-1">
                <h3
                  className="text-lg leading-6 font-medium text-gray-900"
                  id="error-modal-title"
                >
                  {title}
                </h3>
                <div className="mt-2">
                  <p className="text-sm text-gray-500">
                    {getUserMessage()}
                  </p>
                </div>

                {showDetails && (
                  <div className="mt-4">
                    <details className="text-xs">
                      <summary className="cursor-pointer font-medium text-gray-700 hover:text-gray-900">
                        Technical Details
                      </summary>
                      <div className="mt-2 p-3 bg-gray-50 rounded-md">
                        <div className="space-y-2">
                          <div>
                            <span className="font-semibold">Type:</span>{' '}
                            <span className="font-mono">{getErrorType()}</span>
                          </div>
                          <div>
                            <span className="font-semibold">Message:</span>{' '}
                            <span className="font-mono text-gray-700">{error.message}</span>
                          </div>
                          {error instanceof ApiError && (
                            <>
                              <div>
                                <span className="font-semibold">Endpoint:</span>{' '}
                                <span className="font-mono text-gray-700">{error.endpoint}</span>
                              </div>
                              {error.correlationId && (
                                <div>
                                  <span className="font-semibold">Correlation ID:</span>{' '}
                                  <span className="font-mono text-gray-700">{error.correlationId}</span>
                                </div>
                              )}
                            </>
                          )}
                          {error instanceof NetworkError && (
                            <div>
                              <span className="font-semibold">Endpoint:</span>{' '}
                              <span className="font-mono text-gray-700">{error.endpoint}</span>
                            </div>
                          )}
                          {error.stack && (
                            <div>
                              <span className="font-semibold">Stack Trace:</span>
                              <pre className="mt-1 text-xs overflow-auto max-h-32 whitespace-pre-wrap break-words text-gray-600">
                                {error.stack}
                              </pre>
                            </div>
                          )}
                        </div>
                      </div>
                    </details>
                  </div>
                )}
              </div>
            </div>
          </div>

          <div className="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse gap-2">
            {canRetry() && onRetry && (
              <button
                type="button"
                onClick={() => {
                  onClose();
                  onRetry();
                }}
                className="w-full inline-flex justify-center rounded-md border border-transparent shadow-sm px-4 py-2 bg-blue-600 text-base font-medium text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 sm:ml-3 sm:w-auto sm:text-sm"
              >
                Retry
              </button>
            )}
            <button
              type="button"
              onClick={onClose}
              className="mt-3 w-full inline-flex justify-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 sm:mt-0 sm:w-auto sm:text-sm"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
