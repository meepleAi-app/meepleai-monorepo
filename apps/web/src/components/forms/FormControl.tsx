/**
 * FormControl Component (FE-IMP-006)
 *
 * Wrapper for form input elements with automatic id/aria attributes.
 * Connects the input to FormLabel and FormError for accessibility.
 *
 * Usage:
 * ```tsx
 * <FormItem>
 *   <FormLabel>Email</FormLabel>
 *   <FormControl>
 *     <Input {...field} type="email" />
 *   </FormControl>
 * </FormItem>
 * ```
 */

import * as React from 'react';

import { Slot } from '@radix-ui/react-slot';

import { useFormField } from './FormField';

export const FormControl = React.forwardRef<
  React.ElementRef<typeof Slot>,
  React.ComponentPropsWithoutRef<typeof Slot>
>(({ ...props }, ref) => {
  const { error, formItemId, formDescriptionId, formMessageId } = useFormField();

  return (
    <Slot
      ref={ref}
      id={formItemId}
      aria-describedby={!error ? `${formDescriptionId}` : `${formDescriptionId} ${formMessageId}`}
      aria-invalid={!!error}
      {...props}
    />
  );
});
FormControl.displayName = 'FormControl';
