'use client';

import { OnboardingWizard } from '@/components/onboarding';

export default function OnboardingPage() {
  return (
    <div className="flex min-h-[80vh] items-center justify-center px-4">
      <div className="w-full max-w-lg">
        <OnboardingWizard token="" role="" startStep={2} />
      </div>
    </div>
  );
}
