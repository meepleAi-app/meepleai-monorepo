/**
 * FormError Component (FE-IMP-006)
 *
 * Error message display for form fields with accessibility.
 * Automatically shows field-level errors from Zod validation.
 *
 * Features:
 * - ARIA live region for screen readers
 * - Auto-ID association with field
 * - Italian error messages
 *
 * Usage:
 * ```tsx
 * <FormItem>
 *   <FormLabel>Email</FormLabel>
 *   <FormControl>
 *     <Input {...field} />
 *   </FormControl>
 *   <FormError />
 * </FormItem>
 * ```
 */

import * as React from 'react';
import { useFormField } from './FormField';
import { cn } from '@/lib/utils';

export const FormError = React.forwardRef<
  HTMLParagraphElement,
  React.HTMLAttributes<HTMLParagraphElement>
>(({ className, children, ...props }, ref) => {
  const { error, formMessageId } = useFormField();
  const body = error ? String(error?.message) : children;

  if (!body) {
    return null;
  }

  return (
    <p
      ref={ref}
      id={formMessageId}
      className={cn(
        'text-sm text-red-600 dark:text-red-400 mt-1',
        className
      )}
      role="alert"
      aria-live="polite"
      {...props}
    >
      {body}
    </p>
  );
});
FormError.displayName = 'FormError';
