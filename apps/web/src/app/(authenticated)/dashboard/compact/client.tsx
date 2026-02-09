/**
 * Compact Dashboard Client Component
 * Issue #3306 - User Dashboard Hub Core - MVP
 * Issue #3910 - Redirects to main dashboard (legacy removed)
 */

'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';

export function CompactDashboardClient() {
  const router = useRouter();

  useEffect(() => {
    router.replace('/dashboard');
  }, [router]);

  return <div>Redirecting to dashboard...</div>;
}
