/**
 * BudgetAlertBanner Component (Issue #2521)
 *
 * Alert banner for budget thresholds with:
 * - Warning at 80% budget
 * - Critical at 95% budget
 * - Exceeded at 100% budget
 * - Dismissible with localStorage persistence
 */

'use client';

import { useState, useEffect } from 'react';

import { AlertTriangle, AlertCircle, X } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { BUDGET_ALERT_THRESHOLDS, type CostTrackingDto } from '@/lib/api';

interface BudgetAlertBannerProps {
  costData: CostTrackingDto;
}

export function BudgetAlertBanner({ costData }: BudgetAlertBannerProps) {
  const [isDismissed, setIsDismissed] = useState(false);

  // Check localStorage for dismissed state
  useEffect(() => {
    try {
      const dismissed = localStorage.getItem('budget-alert-dismissed');
      if (dismissed) {
        const dismissedDate = new Date(dismissed);
        const now = new Date();
        // Reset dismissal after 24 hours
        if (now.getTime() - dismissedDate.getTime() < 24 * 60 * 60 * 1000) {
          setIsDismissed(true);
        } else {
          localStorage.removeItem('budget-alert-dismissed');
        }
      }
    } catch (error) {
      console.warn('localStorage unavailable for budget alert:', error);
      // Degrade gracefully - alert remains dismissible for session only
    }
  }, []);

  const handleDismiss = () => {
    setIsDismissed(true);
    try {
      localStorage.setItem('budget-alert-dismissed', new Date().toISOString());
    } catch (error) {
      console.warn('Failed to persist budget alert dismissal:', error);
      // Still dismiss for current session
    }
  };

  // Don't show if dismissed or on track
  if (isDismissed || costData.budgetStatus === 'on_track') {
    return null;
  }

  const todayPercentage = costData.today.percentageUsed;
  const monthPercentage = costData.thisMonth.percentageUsed;

  // Determine alert level (use higher of today or month)
  const maxPercentage = Math.max(todayPercentage, monthPercentage);
  const isExceeded = maxPercentage >= BUDGET_ALERT_THRESHOLDS.exceeded;
  const isCritical = maxPercentage >= BUDGET_ALERT_THRESHOLDS.critical;
  const isWarning = maxPercentage >= BUDGET_ALERT_THRESHOLDS.warning;

  if (!isWarning) return null;

  const variant = isExceeded || isCritical ? 'destructive' : 'default';
  const Icon = isExceeded || isCritical ? AlertCircle : AlertTriangle;

  const title = isExceeded
    ? '❌ Budget Exceeded'
    : isCritical
      ? '🚨 Critical: Budget Nearly Exceeded'
      : '⚠️ Warning: Approaching Budget Limit';

  const description = isExceeded
    ? `You have exceeded your budget limit. Today: ${todayPercentage.toFixed(1)}%, This Month: ${monthPercentage.toFixed(1)}%.`
    : isCritical
      ? `You are at ${maxPercentage.toFixed(1)}% of your budget limit. Consider reducing AI model usage.`
      : `You have used ${maxPercentage.toFixed(1)}% of your budget. Monitor your usage to avoid overspending.`;

  return (
    <Alert variant={variant} className="relative">
      <Icon className="h-4 w-4" />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>{description}</AlertDescription>
      <Button
        variant="ghost"
        size="sm"
        className="absolute top-2 right-2 h-6 w-6 p-0"
        onClick={handleDismiss}
      >
        <X className="h-4 w-4" />
      </Button>
    </Alert>
  );
}
