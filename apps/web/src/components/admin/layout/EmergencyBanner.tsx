'use client';

/**
 * EmergencyBanner — Global admin banner when emergency overrides are active
 * Issue #129 — Shows a persistent red banner in AdminShell when any LLM override is active
 */

import { useCallback, useEffect, useState } from 'react';

import { ShieldAlert, X } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

export function EmergencyBanner() {
  const [count, setCount] = useState(0);
  const [dismissed, setDismissed] = useState(false);
  const [lastDismissedCount, setLastDismissedCount] = useState(0);

  const fetchCount = useCallback(async () => {
    try {
      const overrides = await api.admin.getActiveEmergencyOverrides();
      const newCount = overrides.length;
      setCount(newCount);
      // Only re-show if the count increased (new override added)
      if (newCount > lastDismissedCount) setDismissed(false);
    } catch {
      // silent — banner is non-critical
    }
  }, [lastDismissedCount]);

  useEffect(() => {
    fetchCount();
    const interval = setInterval(fetchCount, 60_000);
    return () => clearInterval(interval);
  }, [fetchCount]);

  if (count === 0 || dismissed) return null;

  return (
    <div
      className="flex items-center gap-3 px-4 py-2 bg-red-600 text-white text-sm"
      data-testid="emergency-banner"
    >
      <ShieldAlert className="h-4 w-4 flex-shrink-0" />
      <span className="flex-1">
        {count} active emergency override{count > 1 ? 's' : ''}.{' '}
        <Link
          href="/admin/monitor/operations?tab=emergency"
          className="underline font-medium hover:text-red-100"
        >
          View details
        </Link>
      </span>
      <Button
        variant="ghost"
        size="sm"
        onClick={() => {
          setDismissed(true);
          setLastDismissedCount(count);
        }}
        className="h-6 w-6 p-0 text-white hover:text-red-200 hover:bg-red-700"
        aria-label="Dismiss banner"
      >
        <X className="h-4 w-4" />
      </Button>
    </div>
  );
}
