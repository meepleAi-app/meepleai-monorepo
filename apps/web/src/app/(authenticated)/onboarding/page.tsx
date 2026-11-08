'use client';

import type { JSX } from 'react';
import { useEffect } from 'react';

import { Loader2 } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';

import { OnboardingGenericWizard } from './OnboardingGenericWizard';

/**
 * /onboarding entry point — generic (not invited) users.
 *
 * Asse D follow-up P3 (umbrella #1895 sub-issue #1899 follow-up). The legacy
 * `OnboardingTourClient` 5-step page-flow has been replaced by the 3-step
 * `OnboardingGenericWizard` built on the asse-B `WizardModal` primitive.
 *
 * Invited users land here too: `/accept-invite` accepts the token and redirects
 * to this page. (A separate 5-step `OnboardingWizard` existed, mounted by no
 * route; Issue #4138 deleted it.)
 */
export default function OnboardingPage(): JSX.Element | null {
  const router = useRouter();
  const { user, loading, refreshUser } = useAuth();

  useEffect(() => {
    void refreshUser();
  }, [refreshUser]);

  useEffect(() => {
    if (!loading && user?.onboardingCompleted) {
      router.replace('/library');
    }
  }, [loading, user, router]);

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-amber-500" aria-label="Caricamento" />
      </div>
    );
  }

  if (!user || user.onboardingCompleted) {
    return null;
  }

  const userName = user.displayName?.trim() || null;
  return <OnboardingGenericWizard userName={userName} />;
}
