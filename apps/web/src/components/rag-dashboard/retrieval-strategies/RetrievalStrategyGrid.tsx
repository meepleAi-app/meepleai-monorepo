'use client';

import React, { useCallback, useMemo, useState } from 'react';

import { motion } from 'framer-motion';

import { cn } from '@/lib/utils';

import { RetrievalStrategyCard } from './RetrievalStrategyCard';
import { getAllRetrievalStrategies, RETRIEVAL_STRATEGY_ORDER } from './strategy-data';

import type { RetrievalStrategyGridProps, RetrievalStrategy, RetrievalStrategyType } from './types';

/**
 * Container animation variants for staggered children.
 */
const containerVariants = {
  hidden: { opacity: 0 },
  visible: {
    opacity: 1,
    transition: {
      staggerChildren: 0.1,
      delayChildren: 0.1,
    },
  },
};

/**
 * RetrievalStrategyGrid Component
 *
 * Displays all 6 RAG retrieval strategies in a responsive grid layout.
 *
 * Features:
 * - Responsive grid: 2 cols mobile, 3 cols tablet, 6 cols desktop
 * - Staggered animation on mount
 * - Strategy selection with visual feedback
 * - Keyboard navigation support
 */
export function RetrievalStrategyGrid({
  strategies,
  selectedStrategy,
  onStrategySelect,
  className,
}: RetrievalStrategyGridProps): React.JSX.Element {
  const [localSelectedStrategy, setLocalSelectedStrategy] = useState<RetrievalStrategyType | null>(
    selectedStrategy ?? null
  );

  // Use provided strategies or default to all strategies in order
  const displayStrategies = useMemo(() => {
    if (strategies) {
      return strategies;
    }
    const allStrategies = getAllRetrievalStrategies();
    return RETRIEVAL_STRATEGY_ORDER.map(
      (id) => allStrategies.find((s) => s.id === id)!
    ).filter(Boolean);
  }, [strategies]);

  const handleStrategySelect = useCallback(
    (strategy: RetrievalStrategy) => {
      const newSelected = strategy.id === localSelectedStrategy ? null : strategy.id;
      setLocalSelectedStrategy(newSelected);
      onStrategySelect?.(strategy);
    },
    [localSelectedStrategy, onStrategySelect]
  );

  // Sync external selection changes
  React.useEffect(() => {
    if (selectedStrategy !== undefined) {
      setLocalSelectedStrategy(selectedStrategy);
    }
  }, [selectedStrategy]);

  return (
    <motion.div
      className={cn(
        'grid gap-4',
        'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6',
        className
      )}
      variants={containerVariants}
      initial="hidden"
      animate="visible"
      role="listbox"
      aria-label="RAG Retrieval Strategies"
    >
      {displayStrategies.map((strategy) => (
        <RetrievalStrategyCard
          key={strategy.id}
          strategy={strategy}
          isSelected={localSelectedStrategy === strategy.id}
          onClick={handleStrategySelect}
        />
      ))}
    </motion.div>
  );
}
