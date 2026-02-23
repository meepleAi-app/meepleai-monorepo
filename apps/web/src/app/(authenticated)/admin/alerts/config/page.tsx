import { Suspense } from 'react';

import { AlertConfigPageClient } from './client';

/**
 * Alert Configuration Page (Issue #915)
 * Server component wrapper for alert configuration UI
 */
export const metadata = {
  title: 'Alert Configuration | MeepleAI Admin',
  description: 'Configure email, Slack, and PagerDuty settings for the alerting system',
};

export default function AlertConfigPage() {
  return (
    <Suspense>
      <AlertConfigPageClient />
    </Suspense>
  );
}
