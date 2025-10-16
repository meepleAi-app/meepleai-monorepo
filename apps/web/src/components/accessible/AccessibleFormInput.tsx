/**
 * AccessibleFormInput Component (UI-05)
 *
 * A fully accessible form input component following WCAG 2.1 AA standards.
 *
 * Features:
 * - Proper label association (htmlFor/id)
 * - Error announcements with aria-live
 * - Hint/description with aria-describedby
 * - Required field indication
 * - Focus indicators
 * - High contrast support
 * - Support for different input types
 *
 * @example
 * ```tsx
 * // Basic input
 * <AccessibleFormInput
 *   label="Email"
 *   type="email"
 *   value={email}
 *   onChange={(e) => setEmail(e.target.value)}
 *   required
 * />
 *
 * // Input with hint
 * <AccessibleFormInput
 *   label="Password"
 *   type="password"
 *   value={password}
 *   onChange={(e) => setPassword(e.target.value)}
 *   hint="Must be at least 8 characters"
 *   required
 * />
 *
 * // Input with error
 * <AccessibleFormInput
 *   label="Username"
 *   value={username}
 *   onChange={(e) => setUsername(e.target.value)}
 *   error="Username is already taken"
 * />
 * ```
 */

import { forwardRef, InputHTMLAttributes, useId } from 'react';

export interface AccessibleFormInputProps
  extends Omit<InputHTMLAttributes<HTMLInputElement>, 'aria-label'> {
  /**
   * Input label text (required for accessibility)
   */
  label: string;

  /**
   * Optional hint/description text
   * Announced by screen readers via aria-describedby
   */
  hint?: string;

  /**
   * Error message to display
   * Announced by screen readers via aria-describedby and aria-invalid
   */
  error?: string;

  /**
   * Whether the label should be visually hidden (still accessible to screen readers)
   * @default false
   */
  hideLabel?: boolean;

  /**
   * Additional CSS classes for the container
   */
  containerClassName?: string;

  /**
   * Additional CSS classes for the input
   */
  inputClassName?: string;
}

/**
 * AccessibleFormInput component with full WCAG 2.1 AA compliance
 */
export const AccessibleFormInput = forwardRef<HTMLInputElement, AccessibleFormInputProps>(
  (
    {
      label,
      hint,
      error,
      hideLabel = false,
      required,
      containerClassName = '',
      inputClassName = '',
      className,
      id: providedId,
      ...props
    },
    ref
  ) => {
    // Generate unique IDs for accessibility
    const generatedId = useId();
    const inputId = providedId || generatedId;
    const hintId = hint ? `${inputId}-hint` : undefined;
    const errorId = error ? `${inputId}-error` : undefined;

    // Combine aria-describedby (hint + error)
    const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined;

    // Base input classes
    const baseInputClasses = [
      'w-full',
      'px-4',
      'py-3',
      'rounded-lg',
      'border',
      'transition-colors',
      'duration-200',
      // Normal state
      'bg-white',
      'dark:bg-slate-800',
      'text-slate-900',
      'dark:text-white',
      'border-slate-300',
      'dark:border-slate-600',
      // Focus state (WCAG 2.1 AA)
      'focus:outline-none',
      'focus:ring-2',
      'focus:ring-primary-500',
      'focus:border-primary-500',
      // Disabled state
      'disabled:bg-slate-100',
      'disabled:dark:bg-slate-900',
      'disabled:cursor-not-allowed',
      'disabled:opacity-60',
      // Error state
      error ? 'border-red-500' : '',
      error ? 'focus:ring-red-500' : '',
      error ? 'focus:border-red-500' : '',
    ];

    const inputClasses = [
      ...baseInputClasses,
      inputClassName || className || '',
    ]
      .filter(Boolean)
      .join(' ');

    return (
      <div className={`space-y-2 ${containerClassName}`}>
        {/* Label */}
        <label
          htmlFor={inputId}
          className={`
            block
            text-sm
            font-medium
            text-slate-700
            dark:text-slate-300
            ${hideLabel ? 'sr-only' : ''}
          `}
        >
          {label}
          {required && (
            <span className="text-red-500 ml-1" aria-label="required">
              *
            </span>
          )}
        </label>

        {/* Input */}
        <input
          ref={ref}
          id={inputId}
          className={inputClasses}
          required={required}
          aria-required={required || undefined}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          {...props}
        />

        {/* Hint Text */}
        {hint && !error && (
          <p id={hintId} className="text-sm text-slate-600 dark:text-slate-400">
            {hint}
          </p>
        )}

        {/* Error Message */}
        {error && (
          <div
            id={errorId}
            role="alert"
            aria-live="polite"
            aria-atomic="true"
            className="flex items-start gap-2 text-sm text-red-600 dark:text-red-400"
          >
            {/* Error icon */}
            <svg
              className="w-5 h-5 flex-shrink-0 mt-0.5"
              fill="currentColor"
              viewBox="0 0 20 20"
              aria-hidden="true"
            >
              <path
                fillRule="evenodd"
                d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                clipRule="evenodd"
              />
            </svg>
            <span>{error}</span>
          </div>
        )}
      </div>
    );
  }
);

AccessibleFormInput.displayName = 'AccessibleFormInput';
