/**
 * LoginForm Component
 *
 * Reusable login form with:
 * - React 19 useActionState for form state management
 * - Server Actions pattern for auth
 * - Accessible form controls
 * - Loading states
 * - Localized Italian error messages
 *
 * Issue #1078: FE-IMP-002 — Server Actions per Auth & Export
 */

import { useActionState, useEffect } from 'react';
import { AccessibleFormInput } from '@/components/accessible';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { loginAction, type AuthActionState } from '@/actions/auth';
import { validationMessages } from '@/lib/i18n/errors';

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
  // Use React 19 useActionState for form handling
  const [state, formAction, isPending] = useActionState<AuthActionState, FormData>(
    loginAction,
    { success: false }
  );

  // Call onSuccess callback when login succeeds (useEffect to avoid render-time side effects)
  useEffect(() => {
    if (state.success && state.user && onSuccess) {
      onSuccess(state.user);
    }
  }, [state.success, state.user, onSuccess]);

  return (
    <form action={formAction} className="space-y-4" noValidate>
      {/* Email Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label="Email"
          id="login-email"
          name="email"
          type="email"
          placeholder="tuoemail@esempio.com"
          autoComplete="email"
          defaultValue={initialEmail}
          required
          disabled={isPending}
        />
      </div>

      {/* Password Field */}
      <div className="space-y-2">
        <AccessibleFormInput
          label="Password"
          id="login-password"
          name="password"
          type="password"
          placeholder="••••••••"
          autoComplete="current-password"
          required
          disabled={isPending}
        />
      </div>

      {/* Error Message */}
      {state.error && (
        <div
          className="rounded-md bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 p-3"
          role="alert"
          aria-live="polite"
        >
          <p className="text-sm text-red-800 dark:text-red-200">{state.error.message}</p>
        </div>
      )}

      {/* Submit Button */}
      <LoadingButton
        type="submit"
        className="w-full"
        isLoading={isPending}
        loadingText="Accesso in corso..."
      >
        Accedi
      </LoadingButton>
    </form>
  );
}
