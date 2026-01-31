/**
 * FormDescription Component (FE-IMP-006)
 *
 * Help text for form fields (optional).
 *
 * Usage:
 * ```tsx
 * <FormItem>
 *   <FormLabel>Password</FormLabel>
 *   <FormControl>
 *     <Input {...field} type="password" />
 *   </FormControl>
 *   <FormDescription>
 *     Deve contenere maiuscole, minuscole e numeri (minimo 8 caratteri)
 *   </FormDescription>
 * </FormItem>
 * ```
 */

import * as React from 'react';

import { cn } from '@/lib/utils';

import { useFormField } from './FormField';

export const FormDescription = React.forwardRef<
  HTMLParagraphElement,
  React.HTMLAttributes<HTMLParagraphElement>
>(({ className, ...props }, ref) => {
  const { formDescriptionId } = useFormField();

  return (
    <p
      ref={ref}
      id={formDescriptionId}
      className={cn('text-xs text-slate-500 dark:text-slate-400', className)}
      {...props}
    />
  );
});
FormDescription.displayName = 'FormDescription';
