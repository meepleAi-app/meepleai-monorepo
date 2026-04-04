/**
 * Token Quota Display - Progress bar with usage tracking
 * Issue #3240 (FRONT-004)
 *
 * Displays the current token quota for the authenticated user.
 * Quota data comes from the session quota API.
 */

'use client';

import { AlertTriangle } from 'lucide-react';

import { useSessionQuota } from '@/hooks/queries/useSessionQuota';

export function TokenQuotaDisplay() {
  const { data, isLoading } = useSessionQuota();

  if (isLoading) {
    return (
      <div className="space-y-3 animate-pulse">
        <div className="flex items-center justify-between">
          <div className="h-4 w-24 bg-slate-800 rounded" />
          <div className="h-3 w-16 bg-slate-800 rounded" />
        </div>
        <div className="h-3 rounded-full bg-slate-800" />
        <div className="h-4 w-32 bg-slate-800 rounded" />
      </div>
    );
  }

  if (!data) {
    return (
      <div className="space-y-3">
        <label className="text-sm font-medium text-slate-200">Session Quota</label>
        <p className="text-xs text-slate-500">Quota information not available.</p>
      </div>
    );
  }

  const current = data.currentSessions;
  const limit = data.maxSessions;
  const percentage = limit > 0 ? (current / limit) * 100 : 0;

  const getColor = () => {
    if (percentage > 90) return 'bg-red-500';
    if (percentage > 70) return 'bg-yellow-500';
    return 'bg-cyan-500';
  };

  const showWarning = percentage > 90;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <label className="text-sm font-medium text-slate-200">Session Quota</label>
        <span className="text-xs text-slate-400">
          {data.canCreateNew ? `${data.remainingSlots} remaining` : 'Quota full'}
        </span>
      </div>

      {/* Progress Bar */}
      <div className="relative h-3 rounded-full bg-slate-800 overflow-hidden">
        <div
          className={`h-full transition-all duration-500 ${getColor()} ${showWarning ? 'agent-pulse-cyan' : ''}`}
          style={{ width: `${Math.min(percentage, 100)}%` }}
        />
      </div>

      {/* Usage Text */}
      <div className="flex items-center justify-between">
        <span className="text-sm text-slate-300">
          {current} / {data.isUnlimited ? '∞' : limit} sessions
        </span>
        {showWarning && (
          <div className="flex items-center gap-1 text-red-400">
            <AlertTriangle className="h-4 w-4" />
            <span className="text-xs">Quota almost full</span>
          </div>
        )}
      </div>
    </div>
  );
}
