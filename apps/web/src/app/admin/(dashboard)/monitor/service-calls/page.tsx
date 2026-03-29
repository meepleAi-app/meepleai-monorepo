'use client';

import { ServiceCallHistory } from './ServiceCallHistory';
import { ServiceSummaryCards } from './ServiceSummaryCards';

export default function ServiceCallsPage() {
  return (
    <div data-testid="service-calls-page" className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Service Call History</h1>
        <p className="text-muted-foreground">
          External service interactions, latency, and error tracking
        </p>
      </div>
      <ServiceSummaryCards />
      <ServiceCallHistory />
    </div>
  );
}
