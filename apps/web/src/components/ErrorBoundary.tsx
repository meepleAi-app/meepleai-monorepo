/**
 * React Error Boundary component with fallback UI
 */

import React, { Component, ErrorInfo, ReactNode } from 'react';
import { logger } from '../lib/logger';
import { createErrorContext } from '../lib/errors';

interface ErrorBoundaryProps {
  children: ReactNode;
  fallback?: ReactNode | ((error: Error, reset: () => void) => ReactNode);
  onError?: (error: Error, errorInfo: ErrorInfo) => void;
  componentName?: string;
  showDetails?: boolean;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
}

/**
 * Error boundary that catches React rendering errors
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
      errorInfo: null
    };
  }

  static getDerivedStateFromError(error: Error): Partial<ErrorBoundaryState> {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    this.setState({ errorInfo });

    // Log error to our logging service
    logger.error(
      'React Error Boundary caught error',
      error,
      createErrorContext(
        this.props.componentName || 'ErrorBoundary',
        'componentDidCatch',
        {
          componentStack: errorInfo.componentStack,
          errorBoundary: this.props.componentName
        }
      )
    );

    // Call custom error handler if provided
    this.props.onError?.(error, errorInfo);
  }

  handleReset = (): void => {
    this.setState({
      hasError: false,
      error: null,
      errorInfo: null
    });
  };

  render(): ReactNode {
    if (this.state.hasError && this.state.error) {
      // Use custom fallback if provided
      if (this.props.fallback) {
        if (typeof this.props.fallback === 'function') {
          return this.props.fallback(this.state.error, this.handleReset);
        }
        return this.props.fallback;
      }

      // Default fallback UI
      return (
        <DefaultErrorFallback
          error={this.state.error}
          errorInfo={this.state.errorInfo}
          onReset={this.handleReset}
          showDetails={this.props.showDetails}
        />
      );
    }

    return this.props.children;
  }
}

interface DefaultErrorFallbackProps {
  error: Error;
  errorInfo: ErrorInfo | null;
  onReset: () => void;
  showDetails?: boolean;
}

/**
 * Default error fallback UI
 */
function DefaultErrorFallback({ error, errorInfo, onReset, showDetails = false }: DefaultErrorFallbackProps) {
  const [detailsExpanded, setDetailsExpanded] = React.useState(false);

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full">
        <div className="bg-white shadow-lg rounded-lg p-6">
          <div className="flex items-center justify-center w-12 h-12 mx-auto bg-red-100 rounded-full">
            <svg
              className="w-6 h-6 text-red-600"
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

          <h2 className="mt-4 text-center text-2xl font-bold text-gray-900">
            Something went wrong
          </h2>

          <p className="mt-2 text-center text-sm text-gray-600">
            We apologize for the inconvenience. The error has been logged and our team has been notified.
          </p>

          {showDetails && (
            <div className="mt-4">
              <button
                onClick={() => setDetailsExpanded(!detailsExpanded)}
                className="w-full flex items-center justify-between px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-md focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
                aria-expanded={detailsExpanded}
              >
                <span>Error Details</span>
                <svg
                  className={`w-5 h-5 transition-transform ${detailsExpanded ? 'transform rotate-180' : ''}`}
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                  aria-hidden="true"
                >
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                </svg>
              </button>

              {detailsExpanded && (
                <div className="mt-2 p-4 bg-gray-50 rounded-md overflow-auto max-h-96">
                  <div className="text-xs font-mono text-gray-800">
                    <div className="font-semibold text-red-600 mb-2">
                      {error.name}: {error.message}
                    </div>
                    {error.stack && (
                      <pre className="whitespace-pre-wrap break-words">
                        {error.stack}
                      </pre>
                    )}
                    {errorInfo?.componentStack && (
                      <div className="mt-4">
                        <div className="font-semibold text-gray-700 mb-1">Component Stack:</div>
                        <pre className="whitespace-pre-wrap break-words text-gray-600">
                          {errorInfo.componentStack}
                        </pre>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          )}

          <div className="mt-6 flex flex-col gap-3">
            <button
              onClick={onReset}
              className="w-full flex justify-center py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
            >
              Try Again
            </button>

            <button
              onClick={() => window.location.href = '/'}
              className="w-full flex justify-center py-2 px-4 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
            >
              Go to Home
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

/**
 * Hook for functional components to handle errors
 */
export function useErrorHandler() {
  const [error, setError] = React.useState<Error | null>(null);

  React.useEffect(() => {
    if (error) {
      throw error;
    }
  }, [error]);

  return {
    handleError: setError,
    clearError: () => setError(null),
    error
  };
}
