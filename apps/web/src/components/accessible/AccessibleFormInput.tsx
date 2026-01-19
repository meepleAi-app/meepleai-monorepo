/**
 * AccessibleFormInput Component (UI-06) - Migrated to shadcn/ui
 *
 * A fully accessible form input component following WCAG 2.1 AA standards.
 * Now uses shadcn Input internally while preserving all accessibility features.
 *
 * Features:
 * - Proper label associations (htmlFor/id)
 * - Error state styling and announcements
 * - Helper text support
 * - Required field indicators
 * - ARIA attributes for screen readers
 * - Focus indicators (WCAG 2.1 AA compliant)
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
 * // Input with error
 * <AccessibleFormInput
 *   label="Password"
 *   type="password"
 *   value={password}
 *   onChange={(e) => setPassword(e.target.value)}
 *   error="Password must be at least 8 characters"
 *   required
 * />
 *
 * // Input with hint
 * <AccessibleFormInput
 *   label="Username"
 *   value={username}
 *   onChange={(e) => setUsername(e.target.value)}
 *   hint="Only letters, numbers, and underscores"
 * />
 * ```
 */

import { forwardRef, InputHTMLAttributes, useId } from 'react';

import { Input } from '@/components/ui/primitives/input';
import { cn } from '@/lib/utils';

export interface AccessibleFormInputProps extends InputHTMLAttributes<HTMLInputElement> {
  /**
   * Label text for the input
   */
  label: string;

  /**
   * Error message to display
   */
  error?: string;

  /**
   * Helper text to display below the input
   */
  hint?: string;

  /**
   * Hide label visually but keep it accessible for screen readers
   * @default false
   */
  hideLabel?: boolean;

  /**
   * Additional CSS classes for the input element
   */
  inputClassName?: string;

  /**
   * Additional CSS classes for the container
   */
  className?: string;
}

/**
 * AccessibleFormInput component with full WCAG 2.1 AA compliance
 * Now powered by shadcn/ui Input
 */
export const AccessibleFormInput = forwardRef<HTMLInputElement, AccessibleFormInputProps>(
  (
    {
      label,
      error,
      hint,
      hideLabel = false,
      id,
      required,
      className = '',
      inputClassName = '',
      type = 'text',
      ...props
    },
    ref
  ) => {
    // Generate unique IDs for accessibility (P1: Use useId() to avoid duplicates)
    const generatedId = useId();
    const inputId = id || generatedId;
    const errorId = `${inputId}-error`;
    const hintId = `${inputId}-hint`;

    // Build aria-describedby string
    const descriptors = [error ? errorId : null, hint ? hintId : null].filter(Boolean).join(' ');

    return (
      <div className={cn('space-y-2', className)}>
        {/* Label */}
        <label
          htmlFor={inputId}
          className={cn('block text-sm font-medium text-foreground', hideLabel && 'sr-only')}
        >
          {label}
          {required && (
            <span className="text-destructive ml-1" aria-label="required">
              *
            </span>
          )}
        </label>

        {/* Input */}
        <Input
          ref={ref}
          id={inputId}
          type={type}
          className={cn(
            'transition-colors',
            error && 'border-destructive focus-visible:ring-destructive',
            inputClassName
          )}
          aria-describedby={descriptors || undefined}
          aria-invalid={!!error}
          aria-required={required}
          {...props}
        />

        {/* Helper text */}
        {hint && !error && (
          <p id={hintId} className="text-sm text-muted-foreground">
            {hint}
          </p>
        )}

        {/* Error message */}
        {error && (
          <div
            id={errorId}
            className="text-sm text-destructive"
            role="alert"
            aria-live="polite"
            aria-atomic="true"
          >
            {error}
          </div>
        )}
      </div>
    );
  }
);

AccessibleFormInput.displayName = 'AccessibleFormInput';
