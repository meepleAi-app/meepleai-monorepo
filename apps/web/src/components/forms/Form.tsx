/**
 * Form Component (FE-IMP-006)
 *
 * Context provider and wrapper for React Hook Form integration.
 * Based on shadcn/ui patterns with accessibility features.
 *
 * Usage:
 * ```tsx
 * const form = useForm({ resolver: zodResolver(schema) });
 *
 * <Form {...form}>
 *   <form onSubmit={form.handleSubmit(onSubmit)}>
 *     <FormField name="email" control={form.control} render={...} />
 *   </form>
 * </Form>
 * ```
 */

import * as React from 'react';
import { FormProvider, UseFormReturn, FieldValues } from 'react-hook-form';

export interface FormProps<TFieldValues extends FieldValues> {
  /**
   * React Hook Form instance (from useForm)
   */
  form: UseFormReturn<TFieldValues>;

  /**
   * Form children (FormField components)
   */
  children: React.ReactNode;

  /**
   * Submit handler
   */
  onSubmit: (data: TFieldValues) => void | Promise<void>;

  /**
   * Additional CSS classes
   */
  className?: string;
}

/**
 * Form component wrapper with React Hook Form context
 */
export function Form<TFieldValues extends FieldValues>({
  form,
  children,
  onSubmit,
  className,
}: FormProps<TFieldValues>) {
  return (
    <FormProvider {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} noValidate className={className}>
        {children}
      </form>
    </FormProvider>
  );
}
