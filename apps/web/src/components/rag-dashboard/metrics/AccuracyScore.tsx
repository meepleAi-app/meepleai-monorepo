'use client';

import React from 'react';

import { motion } from 'framer-motion';
import { Target, Star } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import { RETRIEVAL_STRATEGY_ORDER } from '../retrieval-strategies';

import type { AccuracyScoreProps } from './types';

/**
 * Get color based on accuracy score.
 */
function getScoreColor(score: number): string {
  if (score >= 90) return 'text-green-500';
  if (score >= 75) return 'text-blue-500';
  if (score >= 60) return 'text-amber-500';
  return 'text-red-500';
}

function getScoreBarColor(score: number): string {
  if (score >= 90) return 'bg-green-500';
  if (score >= 75) return 'bg-blue-500';
  if (score >= 60) return 'bg-amber-500';
  return 'bg-red-500';
}

/**
 * AccuracyScore Component
 *
 * Displays accuracy metrics with overall score and per-strategy breakdown.
 */
export function AccuracyScore({
  data,
  className,
}: AccuracyScoreProps): React.JSX.Element {
  return (
    <Card className={cn('h-full', className)}>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold flex items-center gap-2">
          <Target className="h-4 w-4 text-green-500" />
          Accuracy
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Overall score */}
        <div className="flex items-center justify-between">
          <div>
            <div className="text-xs text-muted-foreground">Overall Score</div>
            <motion.div
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              className={cn('text-3xl font-bold', getScoreColor(data.overallScore))}
            >
              {data.overallScore}%
            </motion.div>
          </div>
          <div className="text-right">
            <div className="text-xs text-muted-foreground flex items-center gap-1 justify-end">
              <Star className="h-3 w-3 text-amber-400" />
              User Feedback
            </div>
            <div className="font-medium">{data.userFeedbackScore.toFixed(1)}/5</div>
          </div>
        </div>

        {/* Per-strategy bars */}
        <div className="space-y-2">
          <div className="text-xs text-muted-foreground">By Strategy</div>
          {RETRIEVAL_STRATEGY_ORDER.slice(0, 4).map((strategyId, index) => (
            <div key={strategyId} className="space-y-1">
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground truncate">{strategyId}</span>
                <span className="font-mono">{data.byStrategy[strategyId]}%</span>
              </div>
              <div className="h-1.5 w-full bg-muted rounded-full overflow-hidden">
                <motion.div
                  initial={{ width: 0 }}
                  animate={{ width: `${data.byStrategy[strategyId]}%` }}
                  transition={{ duration: 0.5, ease: 'easeOut', delay: index * 0.05 }}
                  className={cn('h-full rounded-full', getScoreBarColor(data.byStrategy[strategyId]))}
                />
              </div>
            </div>
          ))}
        </div>

        {/* Citation accuracy */}
        <div className="pt-2 border-t text-xs flex justify-between">
          <span className="text-muted-foreground">Citation Accuracy</span>
          <span className={cn('font-medium', getScoreColor(data.citationAccuracy))}>
            {data.citationAccuracy}%
          </span>
        </div>
      </CardContent>
    </Card>
  );
}
