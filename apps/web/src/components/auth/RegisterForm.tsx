/**
 * RegisterForm Component (Migrated to RHF + Zod)
 *
 * Reusable registration form with:
 * - React Hook Form for state management (NO useState)
 * - Zod for schema validation (including password confirmation)
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
  FormDescription,
} from '@/components/forms';
import { Input } from '@/components/ui/input';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { registerFormSchema, type RegisterFormData } from '@/lib/schemas/forms';
import { registerAction, type AuthActionState } from '@/actions/auth';

// ============================================================================
// Component Props
// ============================================================================

export interface RegisterFormProps {
  /**
   * Callback when registration succeeds
   */
  onSuccess?: (user: NonNullable<AuthActionState['user']>) => void;
}

// ============================================================================
// Component
// ============================================================================

export function RegisterForm({
  onSuccess
}: RegisterFormProps) {
  // React Hook Form with Zod validation (NO useState)
  const form = useForm<RegisterFormData>({
    resolver: zodResolver(registerFormSchema),
    defaultValues: {
      email: '',
      displayName: '',
      password: '',
      confirmPassword: '',
    },
  });

  // Submit handler (Server Action)
  const onSubmit = async (data: RegisterFormData) => {
    try {
      const formData = new FormData();
      formData.append('email', data.email);
      if (data.displayName) {
        formData.append('displayName', data.displayName);
      }
      formData.append('password', data.password);

      const result = await registerAction({} as AuthActionState, formData);

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
            : 'Errore durante la registrazione',
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

      {/* Display Name Field (Optional) */}
      <FormField
        control={form.control}
        name="displayName"
        render={({ field }) => (
          <FormItem className="space-y-2">
            <FormLabel>Nome visualizzato (Opzionale)</FormLabel>
            <FormControl>
              <Input
                {...field}
                type="text"
                placeholder="Mario Rossi"
                autoComplete="name"
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
                autoComplete="new-password"
                disabled={isSubmitting}
              />
            </FormControl>
            <FormDescription>
              Deve contenere maiuscole, minuscole e numeri (minimo 8 caratteri)
            </FormDescription>
            <FormError />
          </FormItem>
        )}
      />

      {/* Confirm Password Field */}
      <FormField
        control={form.control}
        name="confirmPassword"
        render={({ field }) => (
          <FormItem className="space-y-2">
            <FormLabel>Conferma Password</FormLabel>
            <FormControl>
              <Input
                {...field}
                type="password"
                placeholder="••••••••"
                autoComplete="new-password"
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
        loadingText="Creazione account..."
      >
        Crea Account
      </LoadingButton>
    </Form>
  );
}
