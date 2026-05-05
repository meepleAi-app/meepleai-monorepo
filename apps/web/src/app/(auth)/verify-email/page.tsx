/**
 * Verify Email Page (Issue #3076)
 *
 * Handles email verification token validation.
 * User lands here after clicking the verification link in their email.
 */

import { Suspense } from 'react';

import { Metadata } from 'next';

import { AuthCard } from '@/components/ui/v2/auth-card';

import { VerifyEmailContent } from './_content';

export const metadata: Metadata = {
  title: 'Verifica email | MeepleAI',
  description: 'Verifica il tuo indirizzo email per attivare il tuo account MeepleAI.',
  robots: { index: false, follow: false },
};

export default function VerifyEmailPage() {
  return (
    <Suspense
      fallback={
        <AuthCard title="Verifica email">
          <div className="text-center py-8">
            <div className="animate-pulse text-muted-foreground text-sm">
              Verifica email in corso...
            </div>
          </div>
        </AuthCard>
      }
    >
      <VerifyEmailContent />
    </Suspense>
  );
}
