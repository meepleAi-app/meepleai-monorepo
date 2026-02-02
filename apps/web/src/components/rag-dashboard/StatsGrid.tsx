'use client';

/**
 * StatsGrid Component
 *
 * Displays key RAG system metrics in a grid of animated stat cards.
 * Part of the "Neural Gaming Interface" aesthetic.
 */

import React from 'react';

import { motion } from 'framer-motion';

import { cn } from '@/lib/utils';

import type { DashboardStats, ViewMode } from './types';

interface StatCardProps {
  value: string | number;
  label: string;
  sublabel?: string;
  variant?: 'default' | 'success' | 'warning';
  delay?: number;
}

const StatCard = ({ value, label, sublabel, variant = 'default', delay = 0 }: StatCardProps) => (
  <motion.div
    className="rag-stat-card"
    data-variant={variant}
    initial={{ opacity: 0, y: 20 }}
    animate={{ opacity: 1, y: 0 }}
    transition={{ delay, duration: 0.4, ease: [0.4, 0, 0.2, 1] }}
    whileHover={{ scale: 1.02 }}
  >
    <div className="rag-stat-value">{value}</div>
    <div className="rag-stat-label">{label}</div>
    {sublabel && (
      <div className="mt-1 text-xs text-muted-foreground opacity-70">{sublabel}</div>
    )}
  </motion.div>
);

interface StatsGridProps {
  stats: DashboardStats;
  viewMode: ViewMode;
  className?: string;
}

export function StatsGrid({ stats, viewMode, className }: StatsGridProps) {
  const technicalStats = [
    { value: stats.ragVariants, label: 'RAG Variants', sublabel: 'Configurable strategies', variant: 'default' as const },
    { value: stats.avgTokensPerQuery.toLocaleString(), label: 'Avg Tokens/Query', sublabel: 'Optimized consumption', variant: 'default' as const },
    { value: `${stats.tokenReduction}%`, label: 'Token Reduction', sublabel: 'vs naive RAG', variant: 'success' as const },
    { value: `${stats.targetAccuracy}%`, label: 'Target Accuracy', sublabel: 'Rule lookup precision', variant: 'success' as const },
    { value: `$${stats.monthlyCost}`, label: 'Monthly Cost', sublabel: '100K queries/month', variant: 'warning' as const },
    { value: `${stats.cacheHitRate}%`, label: 'Cache Hit Rate', sublabel: 'Semantic + memory', variant: 'success' as const },
  ];

  const businessStats = [
    { value: `${stats.tokenReduction}%`, label: 'Cost Reduction', sublabel: 'Token optimization savings', variant: 'success' as const },
    { value: `${stats.targetAccuracy}%`, label: 'Answer Accuracy', sublabel: 'User satisfaction driver', variant: 'success' as const },
    { value: `$${stats.monthlyCost}`, label: 'Monthly Cost', sublabel: '100K queries budget', variant: 'warning' as const },
    { value: `$${(stats.monthlyCost / 100000 * 1000).toFixed(2)}`, label: 'Cost per 1K Queries', sublabel: 'Unit economics', variant: 'default' as const },
    { value: stats.ragVariants, label: 'Strategy Options', sublabel: 'Flexibility for scaling', variant: 'default' as const },
    { value: `${stats.cacheHitRate}%`, label: 'Cache Efficiency', sublabel: 'Instant response rate', variant: 'success' as const },
  ];

  const displayStats = viewMode === 'technical' ? technicalStats : businessStats;

  return (
    <div className={cn('grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4', className)}>
      {displayStats.map((stat, index) => (
        <StatCard
          key={`${stat.label}-${index}`}
          value={stat.value}
          label={stat.label}
          sublabel={stat.sublabel}
          variant={stat.variant}
          delay={index * 0.05}
        />
      ))}
    </div>
  );
}
