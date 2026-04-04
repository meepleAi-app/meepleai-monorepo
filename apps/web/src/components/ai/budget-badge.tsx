'use client';

import { useEffect, useState } from 'react';

import { CreditCard, AlertCircle } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { getUserBudget, type UserBudgetDto } from '@/lib/api/clients/budgetClient';
import { logger } from '@/lib/logger';

interface BudgetBadgeProps {
  userId: string;
  className?: string;
}

/**
 * BudgetBadge Component
 * Displays user's credit balance in chat header
 * Color-coded: Green (>50%), Amber (20-50%), Red (<20%)
 */
export function BudgetBadge({ userId, className }: BudgetBadgeProps) {
  const [budget, setBudget] = useState<UserBudgetDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchBudget = async () => {
      try {
        setLoading(true);
        const data = await getUserBudget(userId);
        setBudget(data);
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load budget');
        logger.error('Budget fetch error:', err);
      } finally {
        setLoading(false);
      }
    };

    fetchBudget();

    // Refresh every 5 minutes (matches backend cache TTL)
    const interval = setInterval(fetchBudget, 5 * 60 * 1000);
    return () => clearInterval(interval);
  }, [userId]);

  if (loading) {
    return (
      <Badge variant="outline" className={className}>
        <CreditCard className="mr-1 h-3 w-3" />
        <span className="text-xs">Loading...</span>
      </Badge>
    );
  }

  if (error || !budget) {
    return (
      <Badge variant="destructive" className={className}>
        <AlertCircle className="mr-1 h-3 w-3" />
        <span className="text-xs">Error</span>
      </Badge>
    );
  }

  // Calculate usage percentage for color coding
  const usagePercent =
    budget.dailyLimit > 0
      ? ((budget.dailyLimit - budget.creditsRemaining) / budget.dailyLimit) * 100
      : 0;

  // Determine variant based on remaining credits
  const variant =
    usagePercent >= 80
      ? 'destructive' // Red: <20% remaining
      : usagePercent >= 50
        ? 'default' // Amber: 20-50% remaining
        : 'outline'; // Green: >50% remaining

  // Format credits with K suffix for large numbers
  const formatCredits = (credits: number): string => {
    if (credits >= 1000) {
      return `${(credits / 1000).toFixed(1)}K`;
    }
    return Math.floor(credits).toString();
  };

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <Badge
            variant={variant}
            className={`cursor-help ${className}`}
            data-testid="budget-badge"
          >
            <CreditCard className="mr-1 h-3 w-3" />
            <span className="text-xs font-mono">
              {formatCredits(budget.creditsRemaining)} credits
            </span>
          </Badge>
        </TooltipTrigger>
        <TooltipContent side="bottom" className="max-w-xs">
          <div className="space-y-2 text-xs">
            <div className="font-semibold">Credit Balance</div>
            <div className="grid grid-cols-2 gap-x-4 gap-y-1">
              <span className="text-muted-foreground">Daily:</span>
              <span className="font-mono">
                {Math.floor(budget.creditsRemaining)} / {budget.dailyLimit}
              </span>
              <span className="text-muted-foreground">Weekly:</span>
              <span className="font-mono">
                {Math.floor(budget.weeklyLimit - budget.weeklyCreditsUsed)} / {budget.weeklyLimit}
              </span>
            </div>
            <div className="border-t pt-2 text-muted-foreground">
              <div>Daily resets in: {budget.dailyResetIn}</div>
              <div>Weekly resets in: {budget.weeklyResetIn}</div>
            </div>
            {budget.isBlocked && (
              <div className="border-t pt-2 text-destructive font-semibold">
                ⚠️ Budget exhausted - using free models
              </div>
            )}
          </div>
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
