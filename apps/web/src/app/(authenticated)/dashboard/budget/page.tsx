'use client';

import { useEffect, useState } from 'react';

import {
  CreditCard,
  TrendingUp,
  Calendar,
  AlertCircle,
  ExternalLink,
  DollarSign,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { useAuth } from '@/hooks/useAuth';
import { getUserBudget, type UserBudgetDto } from '@/lib/api/clients/budgetClient';
import { logger } from '@/lib/logger';

/**
 * Budget Dashboard Page
 * Route: /dashboard/budget
 * Shows detailed credit usage, trends, and tier upgrade options
 */
export default function BudgetDashboardPage() {
  const { user } = useAuth();
  const [budget, setBudget] = useState<UserBudgetDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!user) return;

    const fetchBudget = async () => {
      try {
        setLoading(true);
        const data = await getUserBudget(user.id);
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

    // Refresh every minute for real-time updates
    const interval = setInterval(fetchBudget, 60 * 1000);
    return () => clearInterval(interval);
  }, [user]);

  if (loading) {
    return (
      <div className="container mx-auto py-8 space-y-6">
        <h1 className="text-3xl font-bold">Budget Dashboard</h1>
        <Card>
          <CardContent className="pt-6">
            <div className="text-center text-muted-foreground">Loading budget data...</div>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (error || !budget) {
    return (
      <div className="container mx-auto py-8 space-y-6">
        <h1 className="text-3xl font-bold">Budget Dashboard</h1>
        <Card>
          <CardContent className="pt-6">
            <div className="flex items-center justify-center text-destructive">
              <AlertCircle className="mr-2 h-5 w-5" />
              <span>{error || 'Failed to load budget'}</span>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  const dailyUsagePercent =
    budget.dailyLimit > 0 ? (budget.dailyCreditsUsed / budget.dailyLimit) * 100 : 0;

  const weeklyUsagePercent =
    budget.weeklyLimit > 0 ? (budget.weeklyCreditsUsed / budget.weeklyLimit) * 100 : 0;

  return (
    <div className="container mx-auto py-8 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">Budget Dashboard</h1>
        <Button variant="outline" size="sm" asChild>
          <a href="/dashboard/settings/billing" className="flex items-center">
            <ExternalLink className="mr-2 h-4 w-4" />
            Upgrade Tier
          </a>
        </Button>
      </div>

      {/* Budget Status Alert */}
      {budget.isBlocked && (
        <Card className="border-destructive">
          <CardHeader>
            <CardTitle className="text-destructive flex items-center">
              <AlertCircle className="mr-2 h-5 w-5" />
              Budget Exhausted
            </CardTitle>
            <CardDescription>
              Your daily or weekly credit limit has been reached. Queries are now using free models.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button variant="default" size="sm" asChild>
              <a href="/dashboard/settings/billing">Upgrade to Pro for More Credits</a>
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Overview Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        {/* Daily Credits */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Daily Credits</CardTitle>
            <CreditCard className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{Math.floor(budget.creditsRemaining)}</div>
            <p className="text-xs text-muted-foreground">of {budget.dailyLimit} remaining</p>
            <Progress value={100 - dailyUsagePercent} className="mt-3 h-2" />
            <div className="mt-2 flex items-center text-xs text-muted-foreground">
              <Calendar className="mr-1 h-3 w-3" />
              Resets in {budget.dailyResetIn}
            </div>
          </CardContent>
        </Card>

        {/* Weekly Credits */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Weekly Credits</CardTitle>
            <TrendingUp className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {Math.floor(budget.weeklyLimit - budget.weeklyCreditsUsed)}
            </div>
            <p className="text-xs text-muted-foreground">of {budget.weeklyLimit} remaining</p>
            <Progress value={100 - weeklyUsagePercent} className="mt-3 h-2" />
            <div className="mt-2 flex items-center text-xs text-muted-foreground">
              <Calendar className="mr-1 h-3 w-3" />
              Resets in {budget.weeklyResetIn}
            </div>
          </CardContent>
        </Card>

        {/* Status */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Status</CardTitle>
            <DollarSign className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <Badge
                variant={
                  budget.isBlocked ? 'destructive' : dailyUsagePercent >= 80 ? 'default' : 'outline'
                }
                className="w-full justify-center"
              >
                {budget.isBlocked ? 'Blocked' : dailyUsagePercent >= 80 ? 'Low Credits' : 'Active'}
              </Badge>
              <div className="text-xs text-muted-foreground">1 credit = $0.00001 USD</div>
              <div className="text-xs text-muted-foreground">
                Daily: {Math.floor(budget.dailyCreditsUsed)} / {budget.dailyLimit} used
              </div>
              <div className="text-xs text-muted-foreground">
                Weekly: {Math.floor(budget.weeklyCreditsUsed)} / {budget.weeklyLimit} used
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Usage Details */}
      <Card>
        <CardHeader>
          <CardTitle>Credit Usage Breakdown</CardTitle>
          <CardDescription>Daily and weekly credit consumption</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          {/* Daily Usage */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium">Daily Usage</span>
              <span className="text-sm text-muted-foreground">
                {Math.floor(budget.dailyCreditsUsed)} / {budget.dailyLimit} credits (
                {dailyUsagePercent.toFixed(1)}%)
              </span>
            </div>
            <Progress
              value={dailyUsagePercent}
              className="h-3"
              indicatorClassName={
                dailyUsagePercent >= 95
                  ? 'bg-destructive'
                  : dailyUsagePercent >= 80
                    ? 'bg-yellow-500'
                    : 'bg-primary'
              }
            />
            <div className="flex justify-between text-xs text-muted-foreground">
              <span>Remaining: {Math.floor(budget.creditsRemaining)} credits</span>
              <span>${(budget.creditsRemaining * 0.00001).toFixed(6)} USD</span>
            </div>
          </div>

          {/* Weekly Usage */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium">Weekly Usage</span>
              <span className="text-sm text-muted-foreground">
                {Math.floor(budget.weeklyCreditsUsed)} / {budget.weeklyLimit} credits (
                {weeklyUsagePercent.toFixed(1)}%)
              </span>
            </div>
            <Progress
              value={weeklyUsagePercent}
              className="h-3"
              indicatorClassName={
                weeklyUsagePercent >= 95
                  ? 'bg-destructive'
                  : weeklyUsagePercent >= 80
                    ? 'bg-yellow-500'
                    : 'bg-primary'
              }
            />
            <div className="flex justify-between text-xs text-muted-foreground">
              <span>
                Remaining: {Math.floor(budget.weeklyLimit - budget.weeklyCreditsUsed)} credits
              </span>
              <span>
                ${((budget.weeklyLimit - budget.weeklyCreditsUsed) * 0.00001).toFixed(6)} USD
              </span>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Tier Upgrade CTA */}
      <Card>
        <CardHeader>
          <CardTitle>Need More Credits?</CardTitle>
          <CardDescription>Upgrade your tier for higher credit limits</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            <div className="grid gap-4 md:grid-cols-3">
              <div className="rounded-lg border p-4 space-y-2">
                <div className="font-semibold">Basic Tier</div>
                <div className="text-2xl font-bold">1,000</div>
                <div className="text-xs text-muted-foreground">credits/day</div>
                <div className="text-xs text-muted-foreground">($0.01/day)</div>
              </div>
              <div className="rounded-lg border p-4 space-y-2 border-primary">
                <div className="font-semibold flex items-center">
                  Pro Tier
                  <Badge variant="default" className="ml-2">
                    Popular
                  </Badge>
                </div>
                <div className="text-2xl font-bold">5,000</div>
                <div className="text-xs text-muted-foreground">credits/day</div>
                <div className="text-xs text-muted-foreground">($0.05/day)</div>
              </div>
              <div className="rounded-lg border p-4 space-y-2">
                <div className="font-semibold">Enterprise</div>
                <div className="text-2xl font-bold">∞</div>
                <div className="text-xs text-muted-foreground">Unlimited</div>
                <div className="text-xs text-muted-foreground">Contact sales</div>
              </div>
            </div>
            <Button className="w-full" asChild>
              <a href="/dashboard/settings/billing">View Upgrade Options</a>
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
