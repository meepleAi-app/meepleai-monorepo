/**
 * FormLabel Component (FE-IMP-006)
 *
 * Accessible label for form fields with automatic for/id association.
 *
 * Usage:
 * ```tsx
 * <FormItem>
 *   <FormLabel>Email</FormLabel>
 *   <FormControl>
 *     <Input {...field} />
 *   </FormControl>
 * </FormItem>
 * ```
 */

import * as React from 'react';

import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';

import { useFormField } from './FormField';

export const FormLabel = React.forwardRef<
  React.ElementRef<typeof Label>,
  React.ComponentPropsWithoutRef<typeof Label>
>(({ className, ...props }, ref) => {
  const { error, formItemId } = useFormField();

  return (
    <Label
      ref={ref}
      className={cn(error && 'text-red-600 dark:text-red-400', className)}
      htmlFor={formItemId}
      {...props}
    />
  );
});
FormLabel.displayName = 'FormLabel';
