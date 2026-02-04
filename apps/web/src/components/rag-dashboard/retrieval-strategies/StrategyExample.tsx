'use client';

import React from 'react';

import { motion } from 'framer-motion';
import { MessageSquare, Lightbulb, FileText } from 'lucide-react';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import type { StrategyExample as StrategyExampleType } from './strategy-details-data';

/**
 * Props for StrategyExample component.
 */
export interface StrategyExampleProps {
  example: StrategyExampleType;
  strategyColor?: string;
  className?: string;
}

/**
 * Color mapping for example cards.
 */
const COLOR_CLASSES: Record<string, string> = {
  blue: 'border-blue-500/30 bg-blue-500/5',
  purple: 'border-purple-500/30 bg-purple-500/5',
  green: 'border-green-500/30 bg-green-500/5',
  orange: 'border-orange-500/30 bg-orange-500/5',
  cyan: 'border-cyan-500/30 bg-cyan-500/5',
  red: 'border-red-500/30 bg-red-500/5',
};

const HIGHLIGHT_CLASSES: Record<string, string> = {
  blue: 'bg-blue-500/10 border-blue-500/30',
  purple: 'bg-purple-500/10 border-purple-500/30',
  green: 'bg-green-500/10 border-green-500/30',
  orange: 'bg-orange-500/10 border-orange-500/30',
  cyan: 'bg-cyan-500/10 border-cyan-500/30',
  red: 'bg-red-500/10 border-red-500/30',
};

/**
 * StrategyExample Component
 *
 * Displays an example query and response for a retrieval strategy.
 * Shows the query, context, response, and a highlight explaining
 * how the strategy processed the query.
 */
export function StrategyExample({
  example,
  strategyColor = 'blue',
  className,
}: StrategyExampleProps): React.JSX.Element {
  return (
    <div className={cn('space-y-4', className)}>
      {/* Query */}
      <motion.div
        initial={{ opacity: 0, x: -20 }}
        animate={{ opacity: 1, x: 0 }}
        transition={{ delay: 0.1 }}
      >
        <Card className={cn('border', COLOR_CLASSES[strategyColor] || COLOR_CLASSES.blue)}>
          <CardContent className="p-4">
            <div className="flex items-start gap-3">
              <div className="p-2 rounded-lg bg-muted/50">
                <MessageSquare className="h-4 w-4 text-muted-foreground" />
              </div>
              <div className="flex-1">
                <p className="text-xs font-semibold text-muted-foreground mb-1">
                  User Query
                </p>
                <p className="text-sm font-medium">{example.query}</p>
                <p className="text-xs text-muted-foreground mt-1 italic">
                  Context: {example.context}
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>

      {/* Response */}
      <motion.div
        initial={{ opacity: 0, x: 20 }}
        animate={{ opacity: 1, x: 0 }}
        transition={{ delay: 0.2 }}
      >
        <Card className="border border-border/50">
          <CardContent className="p-4">
            <div className="flex items-start gap-3">
              <div className="p-2 rounded-lg bg-muted/50">
                <FileText className="h-4 w-4 text-muted-foreground" />
              </div>
              <div className="flex-1">
                <p className="text-xs font-semibold text-muted-foreground mb-1">
                  RAG Response
                </p>
                <p className="text-sm">{example.response}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </motion.div>

      {/* Highlight */}
      {example.highlight && (
        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.3 }}
        >
          <div
            className={cn(
              'flex items-start gap-3 p-4 rounded-lg border',
              HIGHLIGHT_CLASSES[strategyColor] || HIGHLIGHT_CLASSES.blue
            )}
          >
            <Lightbulb className="h-4 w-4 flex-shrink-0 mt-0.5 text-amber-500" />
            <div>
              <p className="text-xs font-semibold text-amber-600 dark:text-amber-400 mb-1">
                Strategy Insight
              </p>
              <p className="text-xs text-muted-foreground">{example.highlight}</p>
            </div>
          </div>
        </motion.div>
      )}
    </div>
  );
}
