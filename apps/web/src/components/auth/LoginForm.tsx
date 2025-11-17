/**
 * LoginForm Component (Migrated to RHF + Zod)
 *
 * Reusable login form with:
 * - React Hook Form for state management (NO useState)
 * - Zod for schema validation
 * - Server Actions pattern for auth
 * - Accessible form controls
 * - Loading states
 * - Localized Italian error messages
 *
 * Issue #1082: FE-IMP-006 — Form System (RHF + Zod)
 */

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormError,
} from '@/components/forms';
import { Input } from '@/components/ui/input';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { loginFormSchema, type LoginFormData } from '@/lib/schemas/forms';
import { loginAction, type AuthActionState } from '@/actions/auth';

// ============================================================================
// Component Props
// ============================================================================

export interface LoginFormProps {
  /**
   * Callback when login succeeds
   */
  onSuccess?: (user: NonNullable<AuthActionState['user']>) => void;

  /**
   * Initial email value (for pre-filling form)
   */
  initialEmail?: string;
}

// ============================================================================
// Component
// ============================================================================

export function LoginForm({
  onSuccess,
  initialEmail = '',
}: LoginFormProps) {
  // React Hook Form with Zod validation (NO useState)
  const form = useForm<LoginFormData>({
    resolver: zodResolver(loginFormSchema),
    defaultValues: {
      email: initialEmail,
      password: '',
    },
  });

  // Submit handler (Server Action)
  const onSubmit = async (data: LoginFormData) => {
    try {
      const formData = new FormData();
      formData.append('email', data.email);
      formData.append('password', data.password);

      const result = await loginAction({} as AuthActionState, formData);

      if (result.success && result.user && onSuccess) {
        onSuccess(result.user);
      } else if (result.error) {
        // Set form error from server
        form.setError('root', {
          message: result.error.message,
        });
      }
    } catch (error) {
      form.setError('root', {
        message:
          error instanceof Error
            ? error.message
            : 'Errore durante il login',
      });
    }
  };

  const isSubmitting = form.formState.isSubmitting;

  return (
    <Form form={form} onSubmit={onSubmit} className="space-y-4">
      {/* Email Field */}
      <FormField
        control={form.control}
        name="email"
        render={({ field }) => (
          <FormItem className="space-y-2">
            <FormLabel>Email</FormLabel>
            <FormControl>
              <Input
                {...field}
                type="email"
                placeholder="tuoemail@esempio.com"
                autoComplete="email"
                disabled={isSubmitting}
              />
            </FormControl>
            <FormError />
          </FormItem>
        )}
      />

      {/* Password Field */}
      <FormField
        control={form.control}
        name="password"
        render={({ field }) => (
          <FormItem className="space-y-2">
            <FormLabel>Password</FormLabel>
            <FormControl>
              <Input
                {...field}
                type="password"
                placeholder="••••••••"
                autoComplete="current-password"
                disabled={isSubmitting}
              />
            </FormControl>
            <FormError />
          </FormItem>
        )}
      />

      {/* Root Error Message */}
      {form.formState.errors.root && (
        <div
          className="rounded-md bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 p-3"
          role="alert"
          aria-live="polite"
        >
          <p className="text-sm text-red-800 dark:text-red-200">
            {form.formState.errors.root.message}
          </p>
        </div>
      )}

      {/* Submit Button */}
      <LoadingButton
        type="submit"
        className="w-full"
        isLoading={isSubmitting}
        loadingText="Accesso in corso..."
      >
        Accedi
      </LoadingButton>
    </Form>
  );
}
