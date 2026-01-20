/**
 * Tests for FormDescription component
 * Requires FormProvider context from react-hook-form
 */

import { render, screen } from '@testing-library/react';
import { useForm, FormProvider } from 'react-hook-form';
import { FormDescription } from '../FormDescription';
import { FormField } from '../FormField';
import { FormItem } from '@/components/ui/forms/form';

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

describe('FormDescription', () => {
  describe('Rendering', () => {
    it('should render description text', () => {
      render(
        <TestForm>
          <FormField
            name="test"
            render={({ field }) => (
              <FormItem>
                <FormDescription>This is a help text</FormDescription>
              </FormItem>
            )}
          />
        </TestForm>
      );

      expect(screen.getByText('This is a help text')).toBeInTheDocument();
    });

    it('should render with custom className', () => {
      render(
        <TestForm>
          <FormField
            name="test"
            render={({ field }) => (
              <FormItem>
                <FormDescription className="custom-class">Help text</FormDescription>
              </FormItem>
            )}
          />
        </TestForm>
      );

      const description = screen.getByText('Help text');
      expect(description).toHaveClass('custom-class');
    });
  });

  describe('Accessibility', () => {
    it('should have proper ID for aria-describedby', () => {
      render(
        <TestForm>
          <FormField
            name="test"
            render={({ field }) => (
              <FormItem>
                <FormDescription>Help text</FormDescription>
              </FormItem>
            )}
          />
        </TestForm>
      );

      const description = screen.getByText('Help text');
      expect(description).toHaveAttribute('id');
      expect(description.id).toMatch(/-form-item-description$/);
    });
  });

  describe('Styling', () => {
    it('should apply default styles', () => {
      render(
        <TestForm>
          <FormField
            name="test"
            render={({ field }) => (
              <FormItem>
                <FormDescription>Help text</FormDescription>
              </FormItem>
            )}
          />
        </TestForm>
      );

      const description = screen.getByText('Help text');
      expect(description).toHaveClass('text-xs');
    });
  });
});
