'use client';

import React from 'react';

import { motion } from 'framer-motion';
import { ArrowRight } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { FlowStep } from './strategy-details-data';

/**
 * Props for StrategyFlowDiagram component.
 */
export interface StrategyFlowDiagramProps {
  steps: FlowStep[];
  className?: string;
}

/**
 * Color mapping for flow steps.
 */
const STEP_COLORS: Record<string, string> = {
  blue: 'bg-blue-500/10 border-blue-500/50 text-blue-500',
  purple: 'bg-purple-500/10 border-purple-500/50 text-purple-500',
  green: 'bg-green-500/10 border-green-500/50 text-green-500',
  orange: 'bg-orange-500/10 border-orange-500/50 text-orange-500',
  cyan: 'bg-cyan-500/10 border-cyan-500/50 text-cyan-500',
  red: 'bg-red-500/10 border-red-500/50 text-red-500',
};

const ARROW_COLORS: Record<string, string> = {
  blue: 'text-blue-500/50',
  purple: 'text-purple-500/50',
  green: 'text-green-500/50',
  orange: 'text-orange-500/50',
  cyan: 'text-cyan-500/50',
  red: 'text-red-500/50',
};

/**
 * StrategyFlowDiagram Component
 *
 * Displays a visual flow diagram of the retrieval strategy steps.
 * Each step is shown as a card with icon, label, and description.
 */
export function StrategyFlowDiagram({
  steps,
  className,
}: StrategyFlowDiagramProps): React.JSX.Element {
  return (
    <div
      className={cn(
        'flex flex-wrap items-center justify-center gap-2',
        className
      )}
      role="list"
      aria-label="Strategy flow steps"
    >
      {steps.map((step, index) => (
        <React.Fragment key={step.id}>
          <motion.div
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: index * 0.1 }}
            className={cn(
              'flex flex-col items-center p-3 rounded-lg border-2',
              'min-w-[100px] max-w-[140px]',
              STEP_COLORS[step.color] || STEP_COLORS.blue
            )}
            role="listitem"
          >
            <span className="text-2xl mb-1">{step.icon}</span>
            <span className="text-xs font-semibold text-center">{step.label}</span>
            <span className="text-[10px] text-muted-foreground text-center mt-1">
              {step.description}
            </span>
          </motion.div>

          {index < steps.length - 1 && (
            <motion.div
              initial={{ opacity: 0, scale: 0.5 }}
              animate={{ opacity: 1, scale: 1 }}
              transition={{ delay: index * 0.1 + 0.05 }}
              className={cn(
                'hidden sm:flex',
                ARROW_COLORS[step.color] || ARROW_COLORS.blue
              )}
              aria-hidden="true"
            >
              <ArrowRight className="h-5 w-5" />
            </motion.div>
          )}
        </React.Fragment>
      ))}
    </div>
  );
}
