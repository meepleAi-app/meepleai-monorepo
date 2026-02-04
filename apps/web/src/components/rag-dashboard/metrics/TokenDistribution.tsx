'use client';

import React, { useMemo } from 'react';

import { motion } from 'framer-motion';
import { Coins } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import type { TokenDistributionProps } from './types';

/**
 * Token category colors.
 */
const TOKEN_COLORS = {
  input: 'bg-blue-500',
  output: 'bg-green-500',
  context: 'bg-purple-500',
};

/**
 * TokenDistribution Component
 *
 * Displays token usage distribution as a segmented bar chart
 * with cost estimation.
 */
export function TokenDistribution({
  data,
  className,
}: TokenDistributionProps): React.JSX.Element {
  const percentages = useMemo(() => {
    const total = data.total || 1;
    return {
      input: (data.input / total) * 100,
      output: (data.output / total) * 100,
      context: (data.context / total) * 100,
    };
  }, [data]);

  const formatTokens = (value: number): string => {
    if (value >= 1000000) return `${(value / 1000000).toFixed(1)}M`;
    if (value >= 1000) return `${(value / 1000).toFixed(1)}K`;
    return value.toString();
  };

  return (
    <Card className={cn('h-full', className)}>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold flex items-center gap-2">
          <Coins className="h-4 w-4 text-amber-500" />
          Token Usage
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Stacked bar */}
        <div className="h-8 w-full rounded-lg overflow-hidden flex">
          <motion.div
            initial={{ width: 0 }}
            animate={{ width: `${percentages.input}%` }}
            transition={{ duration: 0.5, ease: 'easeOut' }}
            className={cn('h-full', TOKEN_COLORS.input)}
            title={`Input: ${formatTokens(data.input)}`}
          />
          <motion.div
            initial={{ width: 0 }}
            animate={{ width: `${percentages.output}%` }}
            transition={{ duration: 0.5, ease: 'easeOut', delay: 0.1 }}
            className={cn('h-full', TOKEN_COLORS.output)}
            title={`Output: ${formatTokens(data.output)}`}
          />
          <motion.div
            initial={{ width: 0 }}
            animate={{ width: `${percentages.context}%` }}
            transition={{ duration: 0.5, ease: 'easeOut', delay: 0.2 }}
            className={cn('h-full', TOKEN_COLORS.context)}
            title={`Context: ${formatTokens(data.context)}`}
          />
        </div>

        {/* Legend */}
        <div className="grid grid-cols-3 gap-2 text-xs">
          <div className="flex items-center gap-2">
            <div className={cn('w-3 h-3 rounded', TOKEN_COLORS.input)} />
            <div>
              <div className="text-muted-foreground">Input</div>
              <div className="font-medium">{formatTokens(data.input)}</div>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <div className={cn('w-3 h-3 rounded', TOKEN_COLORS.output)} />
            <div>
              <div className="text-muted-foreground">Output</div>
              <div className="font-medium">{formatTokens(data.output)}</div>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <div className={cn('w-3 h-3 rounded', TOKEN_COLORS.context)} />
            <div>
              <div className="text-muted-foreground">Context</div>
              <div className="font-medium">{formatTokens(data.context)}</div>
            </div>
          </div>
        </div>

        {/* Total and cost */}
        <div className="pt-2 border-t flex justify-between text-xs">
          <span className="text-muted-foreground">
            Total: <span className="font-medium text-foreground">{formatTokens(data.total)}</span>
          </span>
          <span className="text-muted-foreground">
            Est. Cost: <span className="font-medium text-green-500">${data.costEstimate.toFixed(3)}</span>
          </span>
        </div>
      </CardContent>
    </Card>
  );
}
