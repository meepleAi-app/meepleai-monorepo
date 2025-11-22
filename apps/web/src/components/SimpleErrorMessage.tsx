/**
 * SimpleErrorMessage - Lightweight inline error message component
 *
 * Use this for simple inline error displays. For complex errors with retry
 * functionality and correlation IDs, use ErrorDisplay instead.
 *
 * @example
 * ```tsx
 * <SimpleErrorMessage message={errorMessage} />
 * <SimpleErrorMessage message={warning} variant="warning" onDismiss={() => setWarning(null)} />
 * <SimpleErrorMessage message={info} variant="info" className="mb-4" />
 * ```
 */

import React from 'react';

export interface SimpleErrorMessageProps {
  /**
   * Error message to display. If null/undefined, component renders nothing.
   */
  message: string | null | undefined;

  /**
   * Visual variant of the message
   * @default 'error'
   */
  variant?: 'error' | 'warning' | 'info' | 'success';

  /**
   * Optional callback when dismiss button is clicked
   */
  onDismiss?: () => void;

  /**
   * Additional CSS classes to apply
   */
  className?: string;
}

/**
 * Reusable component for inline error/warning/info messages.
 * Provides consistent styling and accessibility across the application.
 */
export function SimpleErrorMessage({
  message,
  variant = 'error',
  onDismiss,
  className = ''
}: SimpleErrorMessageProps) {
  // Don't render anything if no message
  if (!message) {
    return null;
  }

  // Variant-specific styling
  const variantStyles = {
    error: 'bg-red-500/10 border-red-500/30 text-red-400',
    warning: 'bg-yellow-500/10 border-yellow-500/30 text-yellow-400',
    info: 'bg-blue-500/10 border-blue-500/30 text-blue-400',
    success: 'bg-green-500/10 border-green-500/30 text-green-400'
  };

  const baseStyles = `
    border px-4 py-3 rounded-lg
    ${variantStyles[variant]}
    ${className}
  `.trim().replace(/\s+/g, ' ');

  return (
    <div
      role="alert"
      aria-live="polite"
      className={baseStyles}
    >
      <div className="flex items-start justify-between gap-3">
        <p className="flex-1 text-sm leading-relaxed">{message}</p>
        {onDismiss && (
          <button
            type="button"
            onClick={onDismiss}
            className="flex-shrink-0 opacity-70 hover:opacity-100 transition-opacity focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-current rounded"
            aria-label="Dismiss message"
          >
            <svg
              className="w-5 h-5"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </button>
        )}
      </div>
    </div>
  );
}
