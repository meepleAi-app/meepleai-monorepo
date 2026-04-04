'use client';

/**
 * RequestAccessForm Component
 *
 * Email submission form for invite-only registration mode.
 * States: form → submitting → success.
 *
 * The API always returns 202 (email enumeration-safe), so we always show
 * the success message on any non-network-error response.
 */

import { useState } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import * as z from 'zod';

import { AccessibleFormInput } from '@/components/accessible';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { useTranslation } from '@/hooks/useTranslation';
import { useApiClient } from '@/lib/api/context';

// ============================================================================
// Schema
// ============================================================================

const requestAccessSchema = z.object({
  email: z.string().min(1, 'Email is required').email('Please enter a valid email address'),
});

type RequestAccessFormData = z.infer<typeof requestAccessSchema>;

// ============================================================================
// Component
// ============================================================================

export function RequestAccessForm() {
  const { t } = useTranslation();
  const { accessRequests } = useApiClient();
  const [submitted, setSubmitted] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RequestAccessFormData>({
    resolver: zodResolver(requestAccessSchema),
    defaultValues: { email: '' },
  });

  const onFormSubmit = async (data: RequestAccessFormData) => {
    try {
      await accessRequests.requestAccess(data.email);
    } catch {
      // Network-level errors are swallowed intentionally.
      // The API always returns 202, so we show success on any non-network error too.
      // Only genuine network failures reach here; still show success to prevent enumeration.
    }
    setSubmitted(true);
  };

  if (submitted) {
    return (
      <div
        className="rounded-md bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 p-6 text-center"
        role="status"
        aria-live="polite"
        data-testid="request-access-success"
      >
        <p className="text-sm font-medium text-green-800 dark:text-green-200">
          {t('auth.requestAccess.successTitle', 'Request submitted!')}
        </p>
        <p className="mt-1 text-sm text-green-700 dark:text-green-300">
          {t(
            'auth.requestAccess.successMessage',
            "If your email is eligible, you'll receive an invitation shortly."
          )}
        </p>
      </div>
    );
  }

  return (
    <form
      onSubmit={handleSubmit(onFormSubmit)}
      className="space-y-4"
      noValidate
      data-testid="request-access-form"
    >
      <div className="space-y-2">
        <AccessibleFormInput
          label={t('auth.requestAccess.email', 'Email address')}
          id="request-access-email"
          type="email"
          placeholder={t('auth.requestAccess.emailPlaceholder', 'you@example.com')}
          autoComplete="email"
          error={errors.email?.message}
          required
          disabled={isSubmitting}
          data-testid="request-access-email"
          {...register('email')}
        />
      </div>

      <LoadingButton
        type="submit"
        className="w-full"
        isLoading={isSubmitting}
        loadingText={t('auth.requestAccess.submitting', 'Submitting...')}
        data-testid="request-access-submit"
      >
        {t('auth.requestAccess.submit', 'Request Access')}
      </LoadingButton>
    </form>
  );
}
