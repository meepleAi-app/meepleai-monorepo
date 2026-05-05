'use client';

import type { JSX } from 'react';
import { useEffect } from 'react';

import { Loader2 } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';

import { OnboardingTourClient } from './OnboardingTourClient';

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
  return <OnboardingTourClient userName={userName} />;
}
