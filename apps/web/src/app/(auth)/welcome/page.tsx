/**
 * Welcome Page - Post-Registration Landing
 *
 * Displays a welcome message after successful registration,
 * then automatically redirects to dashboard after 2 seconds.
 */

import { Suspense } from 'react';

import { WelcomeFallback, WelcomeContent } from './_content';

export default function WelcomePage() {
  return (
    <Suspense fallback={<WelcomeFallback />}>
      <WelcomeContent />
    </Suspense>
  );
}
