'use client';

import React from 'react';

import { motion } from 'framer-motion';
import { DollarSign, AlertTriangle } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import type { CostBreakdownProps } from './types';

/**
 * Get budget usage color.
 */
function getBudgetColor(percentage: number): string {
  if (percentage >= 90) return 'text-red-500';
  if (percentage >= 75) return 'text-amber-500';
  return 'text-green-500';
}

function getBudgetBarColor(percentage: number): string {
  if (percentage >= 90) return 'bg-red-500';
  if (percentage >= 75) return 'bg-amber-500';
  return 'bg-green-500';
}

/**
 * CostBreakdown Component
 *
 * Displays cost metrics including session cost, projected monthly,
 * and budget usage.
 */
export function CostBreakdown({
  data,
  className,
}: CostBreakdownProps): React.JSX.Element {
  const budgetPercentage = (data.budgetUsed / data.budgetLimit) * 100;

  return (
    <Card className={cn('h-full', className)}>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold flex items-center gap-2">
          <DollarSign className="h-4 w-4 text-green-500" />
          Cost Analysis
          {budgetPercentage >= 90 && (
            <Badge variant="destructive" className="text-xs">
              <AlertTriangle className="h-3 w-3 mr-1" />
              Alert
            </Badge>
          )}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Main metrics */}
        <div className="grid grid-cols-2 gap-4">
          <div>
            <div className="text-xs text-muted-foreground">Session Cost</div>
            <motion.div
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              className="text-xl font-bold text-foreground"
            >
              ${data.currentSession.toFixed(2)}
            </motion.div>
          </div>
          <div>
            <div className="text-xs text-muted-foreground">Monthly Projected</div>
            <motion.div
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.1 }}
              className="text-xl font-bold text-foreground"
            >
              ${data.projectedMonthly.toFixed(0)}
            </motion.div>
          </div>
        </div>

        {/* Avg per query */}
        <div className="p-3 rounded-lg bg-muted/50 text-center">
          <div className="text-xs text-muted-foreground">Average Cost Per Query</div>
          <div className="text-lg font-mono font-medium">
            ${data.avgPerQuery.toFixed(4)}
          </div>
        </div>

        {/* Budget usage */}
        <div className="space-y-2">
          <div className="flex items-center justify-between text-xs">
            <span className="text-muted-foreground">Budget Usage</span>
            <span className={cn('font-medium', getBudgetColor(budgetPercentage))}>
              ${data.budgetUsed.toFixed(0)} / ${data.budgetLimit}
            </span>
          </div>
          <div className="h-2 w-full bg-muted rounded-full overflow-hidden">
            <motion.div
              initial={{ width: 0 }}
              animate={{ width: `${Math.min(budgetPercentage, 100)}%` }}
              transition={{ duration: 0.5, ease: 'easeOut' }}
              className={cn('h-full rounded-full', getBudgetBarColor(budgetPercentage))}
            />
          </div>
          <div className="text-xs text-muted-foreground text-right">
            {budgetPercentage.toFixed(0)}% used
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
