/**
 * Setup Account Page — Invitation Activation Flow (Issue #124)
 *
 * Public page for invited users to set their password and activate their account.
 * Uses the GET /auth/validate-invitation and POST /auth/activate-account endpoints.
 *
 * Flow:
 * 1. Read `token` from URL search params
 * 2. Validate token via GET endpoint
 * 3. If valid: show password form with pre-filled email/displayName
 * 4. On submit: activate account and redirect to onboarding or dashboard
 */

import { Suspense } from 'react';

import { AuthLayout } from '@/components/layouts';

import { SetupAccountContent } from './_content';

export default function SetupAccountPage() {
  return (
    <Suspense
      fallback={
        <AuthLayout title="Caricamento...">
          <div className="text-center py-8">
            <div className="animate-pulse text-slate-500">Caricamento...</div>
          </div>
        </AuthLayout>
      }
    >
      <SetupAccountContent />
    </Suspense>
  );
}
