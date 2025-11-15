/**
 * RouteErrorBoundary - Error boundary wrapper for route-level error handling
 *
 * Wraps routes with error boundary that displays user-friendly errors using ErrorDisplay.
 * Integrates with correlation ID tracking and error categorization.
 *
 * Usage:
 * ```tsx
 * <RouteErrorBoundary>
 *   <YourRouteComponent />
 * </RouteErrorBoundary>
 * ```
 */

import React, { ReactNode } from 'react';
import { ErrorBoundary } from './ErrorBoundary';
import { ErrorDisplay } from './ErrorDisplay';
import { categorizeError } from '../lib/errorUtils';
import { logger } from '../lib/logger';

interface RouteErrorBoundaryProps {
  /**
   * Child components to wrap with error boundary
   */
  children: ReactNode;

  /**
   * Optional route name for error tracking
   */
  routeName?: string;

  /**
   * Show technical details in development
   * @default process.env.NODE_ENV === 'development'
   */
  showDetails?: boolean;

  /**
   * Custom fallback render function
   */
  fallbackRender?: (error: Error, reset: () => void) => ReactNode;

  /**
   * Callback when error is caught
   */
  onError?: (error: Error, errorInfo: React.ErrorInfo) => void;
}

/**
 * Route-level error boundary with ErrorDisplay integration
 */
export function RouteErrorBoundary({
  children,
  routeName,
  showDetails = process.env.NODE_ENV === 'development',
  fallbackRender,
  onError
}: RouteErrorBoundaryProps) {
  const handleError = (error: Error, errorInfo: React.ErrorInfo) => {
    // Log to console in development
    if (process.env.NODE_ENV === 'development') {
      logger.error(`Route error in ${routeName || 'unknown route'}`, error);
      console.error('Component stack:', errorInfo.componentStack);
    }

    // Call custom error handler if provided
    onError?.(error, errorInfo);
  };

  const defaultFallback = (error: Error, reset: () => void) => {
    // Categorize the error for user-friendly display
    const categorizedError = categorizeError(error);

    return (
      <div className="min-h-dvh flex items-center justify-center bg-gray-50 dark:bg-gray-900 p-4">
        <div className="max-w-2xl w-full">
          <ErrorDisplay
            error={categorizedError}
            onRetry={reset}
            onDismiss={() => {
              // Navigate to home on dismiss
              window.location.href = '/';
            }}
            showTechnicalDetails={showDetails}
          />
        </div>
      </div>
    );
  };

  return (
    <ErrorBoundary
      componentName={routeName || 'RouteErrorBoundary'}
      onError={handleError}
      showDetails={showDetails}
      fallback={fallbackRender || defaultFallback}
    >
      {children}
    </ErrorBoundary>
  );
}

