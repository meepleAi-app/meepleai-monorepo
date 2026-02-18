'use client';

import { useEffect, useState } from 'react';
import { getAdminBudgetOverview, type AdminBudgetOverviewDto } from '@/lib/api/clients/budgetClient';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, DollarSign, Euro } from 'lucide-react';

interface BudgetDebugPanelProps {
  className?: string;
}

/**
 * BudgetDebugPanel Component
 * Admin-only debug panel showing OpenRouter balance and app budgets
 * Displayed in agent playground for budget monitoring
 */
export function BudgetDebugPanel({ className }: BudgetDebugPanelProps) {
  const [overview, setOverview] = useState<AdminBudgetOverviewDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchOverview = async () => {
      try {
        setLoading(true);
        const data = await getAdminBudgetOverview();
        setOverview(data);
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load budget overview');
        console.error('Budget overview fetch error:', err);
      } finally {
        setLoading(false);
      }
    };

    fetchOverview();

    // Refresh every 5 minutes
    const interval = setInterval(fetchOverview, 5 * 60 * 1000);
    return () => clearInterval(interval);
  }, []);

  if (loading) {
    return (
      <Card className={className}>
        <CardHeader>
          <CardTitle className="text-sm font-medium">Budget Overview</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-sm text-muted-foreground">Loading budget data...</div>
        </CardContent>
      </Card>
    );
  }

  if (error || !overview) {
    return (
      <Card className={className}>
        <CardHeader>
          <CardTitle className="text-sm font-medium text-destructive">
            <AlertCircle className="mr-2 inline h-4 w-4" />
            Budget Error
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="text-sm text-muted-foreground">{error || 'No data'}</div>
        </CardContent>
      </Card>
    );
  }

  const dailyStatus = overview.dailyUsagePercent >= 95 ? 'critical' :
                      overview.dailyUsagePercent >= 80 ? 'warning' : 'ok';

  const weeklyStatus = overview.weeklyUsagePercent >= 95 ? 'critical' :
                       overview.weeklyUsagePercent >= 80 ? 'warning' : 'ok';

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle className="text-sm font-medium">Budget Overview (Admin)</CardTitle>
        <CardDescription>OpenRouter balance and app budget status</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* OpenRouter Balance */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium flex items-center">
              <Euro className="mr-2 h-4 w-4" />
              OpenRouter Balance
            </span>
            <Badge variant={overview.openRouterBalanceEuros > 10 ? 'default' : 'destructive'}>
              €{overview.openRouterBalanceEuros.toFixed(2)}
            </Badge>
          </div>
        </div>

        {/* Daily Budget */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium flex items-center">
              <DollarSign className="mr-2 h-4 w-4" />
              Daily Budget
            </span>
            <Badge variant={dailyStatus === 'ok' ? 'outline' : dailyStatus === 'warning' ? 'default' : 'destructive'}>
              {overview.dailyUsagePercent}%
            </Badge>
          </div>
          <div className="space-y-1">
            <div className="flex justify-between text-xs text-muted-foreground">
              <span>${overview.dailySpendUsd.toFixed(4)} used</span>
              <span>${overview.dailyBudgetUsd.toFixed(2)} limit</span>
            </div>
            <Progress
              value={overview.dailyUsagePercent}
              className="h-2"
              indicatorClassName={
                dailyStatus === 'critical' ? 'bg-destructive' :
                dailyStatus === 'warning' ? 'bg-yellow-500' :
                'bg-primary'
              }
            />
          </div>
        </div>

        {/* Weekly Budget */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium flex items-center">
              <DollarSign className="mr-2 h-4 w-4" />
              Weekly Budget
            </span>
            <Badge variant={weeklyStatus === 'ok' ? 'outline' : weeklyStatus === 'warning' ? 'default' : 'destructive'}>
              {overview.weeklyUsagePercent}%
            </Badge>
          </div>
          <div className="space-y-1">
            <div className="flex justify-between text-xs text-muted-foreground">
              <span>${overview.weeklySpendUsd.toFixed(4)} used</span>
              <span>${overview.weeklyBudgetUsd.toFixed(2)} limit</span>
            </div>
            <Progress
              value={overview.weeklyUsagePercent}
              className="h-2"
              indicatorClassName={
                weeklyStatus === 'critical' ? 'bg-destructive' :
                weeklyStatus === 'warning' ? 'bg-yellow-500' :
                'bg-primary'
              }
            />
          </div>
        </div>

        {/* Alert if budget critical */}
        {(dailyStatus === 'critical' || weeklyStatus === 'critical') && (
          <div className="rounded-lg border border-destructive bg-destructive/10 p-3">
            <div className="flex items-start">
              <AlertCircle className="mr-2 mt-0.5 h-4 w-4 text-destructive" />
              <div className="text-xs">
                <div className="font-semibold text-destructive">Budget Alert</div>
                <div className="text-muted-foreground">
                  System may auto-switch to free models
                </div>
              </div>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
