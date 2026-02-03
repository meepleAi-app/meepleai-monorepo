/**
 * Token Quota Display - Progress bar with usage tracking
 * Issue #3240 (FRONT-004)
 */

'use client';

import { AlertTriangle } from 'lucide-react';

export function TokenQuotaDisplay() {
  // Mock data - replace with useTokenQuota hook
  const current = 450;
  const limit = 500;
  const percentage = (current / limit) * 100;
  const resetTime = '2d 14h';

  const getColor = () => {
    if (percentage > 90) return 'bg-red-500';
    if (percentage > 70) return 'bg-yellow-500';
    return 'bg-cyan-500';
  };

  const showWarning = percentage > 90;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <label className="text-sm font-medium text-slate-200">Token Quota</label>
        <span className="text-xs text-slate-400">Resets in {resetTime}</span>
      </div>

      {/* Progress Bar */}
      <div className="relative h-3 rounded-full bg-slate-800 overflow-hidden">
        <div
          className={`h-full transition-all duration-500 ${getColor()} ${showWarning ? 'agent-pulse-cyan' : ''}`}
          style={{ width: `${percentage}%` }}
        />
      </div>

      {/* Usage Text */}
      <div className="flex items-center justify-between">
        <span className="text-sm text-slate-300">
          {current.toLocaleString()} / {limit.toLocaleString()} tokens
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
