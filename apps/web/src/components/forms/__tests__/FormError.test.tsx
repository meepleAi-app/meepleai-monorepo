/**
 * Tests for FormError component
 * Requires FormProvider context from react-hook-form
 */

import { render, screen } from '@testing-library/react';
import { useForm, FormProvider } from 'react-hook-form';
import { FormError } from '../FormError';
import { FormField } from '../FormField';
import { FormItem } from '@/components/ui/form';

// Test wrapper with FormProvider
function TestForm({
  children,
  defaultValues = {},
}: {
  children: React.ReactNode;
  defaultValues?: any;
}) {
  const methods = useForm({ defaultValues });
  return <FormProvider {...methods}>{children}</FormProvider>;
}

describe('FormError', () => {
  describe('Rendering', () => {
    it('should not render when there is no error', () => {
      render(
        <TestForm>
          <FormField
            name="test"
            render={({ field }) => (
              <FormItem>
                <FormError />
              </FormItem>
            )}
          />
        </TestForm>
      );

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('should render error message when error exists', () => {
      const methods = useForm({
        defaultValues: { test: '' },
      });

      // Manually set error
      methods.setError('test', { type: 'required', message: 'This field is required' });

      render(
        <FormProvider {...methods}>
          <FormField
            name="test"
            render={({ field }) => (
              <FormItem>
                <FormError />
              </FormItem>
            )}
          />
        </FormProvider>
      );

      expect(screen.getByRole('alert')).toBeInTheDocument();
      expect(screen.getByText('This field is required')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have alert role', () => {
      const methods = useForm({ defaultValues: { test: '' } });
      methods.setError('test', { type: 'required', message: 'Error message' });

      render(
        <FormProvider {...methods}>
          <FormField
            name="test"
            render={({ field }) => (
              <FormItem>
                <FormError />
              </FormItem>
            )}
          />
        </FormProvider>
      );

      const alert = screen.getByRole('alert');
      expect(alert).toHaveAttribute('aria-live', 'polite');
    });
  });

  describe('Custom Error Messages', () => {
    it('should render custom children when provided', () => {
      render(
        <TestForm>
          <FormField
            name="test"
            render={({ field }) => (
              <FormItem>
                <FormError>Custom error message</FormError>
              </FormItem>
            )}
          />
        </TestForm>
      );

      expect(screen.getByRole('alert')).toBeInTheDocument();
      expect(screen.getByText('Custom error message')).toBeInTheDocument();
    });
  });
});
