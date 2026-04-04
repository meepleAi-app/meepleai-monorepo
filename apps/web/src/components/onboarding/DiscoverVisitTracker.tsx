'use client';

import { useEffect } from 'react';

/**
 * Tracks that the user has visited the Discover page.
 * Sets a localStorage flag used by the onboarding checklist.
 * Renders nothing.
 */
export function DiscoverVisitTracker() {
  useEffect(() => {
    localStorage.setItem('hasVisitedDiscover', 'true');
  }, []);

  return null;
}
