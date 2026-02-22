'use client';

/**
 * BusinessKpiCards — Animated KPI summary cards for Business view
 *
 * Shows the 4 key business metrics of TOMAC-RAG with gradient values
 * and subtle entrance animations.
 */

import { motion } from 'framer-motion';
import { DollarSign, Target, TrendingDown, Zap } from 'lucide-react';

import { DEFAULT_STATS } from './types';

// =============================================================================
// KPI Card Data
// =============================================================================

const KPI_CARDS = [
  {
    icon: TrendingDown,
    label: 'Token Reduction',
    value: `${Math.abs(DEFAULT_STATS.tokenReduction)}%`,
    sub: 'vs. unoptimised pipeline',
    gradient: 'linear-gradient(135deg, hsl(142,76%,36%), hsl(142,76%,50%))',
    iconColor: 'hsl(142,76%,40%)',
    bg: 'hsla(142,76%,36%,0.08)',
    border: 'hsla(142,76%,36%,0.25)',
  },
  {
    icon: Target,
    label: 'Target Accuracy',
    value: `${DEFAULT_STATS.targetAccuracy}%`,
    sub: 'CRAG-validated responses',
    gradient: 'linear-gradient(135deg, hsl(221,83%,53%), hsl(262,83%,62%))',
    iconColor: 'hsl(221,83%,55%)',
    bg: 'hsla(221,83%,53%,0.08)',
    border: 'hsla(221,83%,53%,0.25)',
  },
  {
    icon: Zap,
    label: 'Cache Hit Rate',
    value: `${DEFAULT_STATS.cacheHitRate}%`,
    sub: 'semantic similarity matches',
    gradient: 'linear-gradient(135deg, hsl(25,95%,53%), hsl(45,93%,47%))',
    iconColor: 'hsl(25,95%,53%)',
    bg: 'hsla(25,95%,53%,0.08)',
    border: 'hsla(25,95%,53%,0.25)',
  },
  {
    icon: DollarSign,
    label: 'Monthly Cost',
    value: `${DEFAULT_STATS.monthlyCost.toLocaleString()}`,
    sub: 'at 100K queries / month',
    gradient: 'linear-gradient(135deg, hsl(262,83%,62%), hsl(0,72%,51%))',
    iconColor: 'hsl(262,83%,62%)',
    bg: 'hsla(262,83%,62%,0.08)',
    border: 'hsla(262,83%,62%,0.25)',
  },
] as const;

// =============================================================================
// Component
// =============================================================================

export function BusinessKpiCards() {
  return (
    <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
      {KPI_CARDS.map((card, i) => {
        const Icon = card.icon;
        return (
          <motion.div
            key={card.label}
            className="rag-biz-kpi-card"
            style={{
              backgroundColor: card.bg,
              borderColor: card.border,
            }}
            initial={{ opacity: 0, y: 16 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.08, duration: 0.35 }}
          >
            <Icon className="h-5 w-5 mb-3" style={{ color: card.iconColor }} />
            <div
              className="rag-biz-kpi-value"
              style={{ backgroundImage: card.gradient }}
            >
              {card.value}
            </div>
            <div className="rag-biz-kpi-label">{card.label}</div>
            <div className="rag-biz-kpi-sub">{card.sub}</div>
          </motion.div>
        );
      })}
    </div>
  );
}
