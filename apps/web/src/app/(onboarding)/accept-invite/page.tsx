'use client';

/**
 * Accept Invite Page
 * Issue #132 - Public page for invitation acceptance
 *
 * Validates the invitation token from URL params,
 * then renders the onboarding wizard on success.
 */

import { Suspense, useEffect, useState } from 'react';

import { useSearchParams } from 'next/navigation';

import { OnboardingWizard } from '@/components/onboarding/OnboardingWizard';
import { api } from '@/lib/api';
import type { TokenValidation } from '@/lib/api/schemas/invitation.schemas';

function AcceptInviteContent() {
  const searchParams = useSearchParams();
  const token = searchParams.get('token');
  const [validation, setValidation] = useState<TokenValidation | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      setError('No invitation token provided');
      setLoading(false);
      return;
    }
    api.invitations
      .validateInvitationToken(token)
      .then(result => {
        if (!result.valid) {
          setError('This invitation has expired or is invalid. Contact your administrator.');
        }
        setValidation(result);
      })
      .catch(() => setError('Failed to validate invitation'))
      .finally(() => setLoading(false));
  }, [token]);

  if (loading) {
    return (
      <div className="py-20 text-center" data-testid="loading-state">
        <div className="mx-auto mb-4 h-8 w-8 animate-spin rounded-full border-4 border-amber-200 border-t-amber-600" />
        <p className="text-slate-600">Validating invitation...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="py-20 text-center" data-testid="error-state">
        <h2 className="font-quicksand text-xl font-semibold text-red-900">{error}</h2>
        <p className="mt-2 text-slate-600">
          Please contact your administrator for a new invitation.
        </p>
      </div>
    );
  }

  return <OnboardingWizard token={token!} role={validation!.role ?? 'Player'} />;
}

export default function AcceptInvitePage() {
  return (
    <Suspense
      fallback={
        <div className="py-20 text-center">
          <p className="text-slate-600">Loading...</p>
        </div>
      }
    >
      <AcceptInviteContent />
    </Suspense>
  );
}
