'use client';

import React from 'react';

import { motion } from 'framer-motion';
import { Database } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import type { CacheHitGaugeProps } from './types';

/**
 * Get color based on hit rate percentage.
 */
function getHitRateColor(hitRate: number): string {
  if (hitRate >= 80) return 'text-green-500';
  if (hitRate >= 60) return 'text-blue-500';
  if (hitRate >= 40) return 'text-amber-500';
  return 'text-red-500';
}

function _getHitRateGradient(hitRate: number): string {
  if (hitRate >= 80) return 'from-green-500 to-green-400';
  if (hitRate >= 60) return 'from-blue-500 to-blue-400';
  if (hitRate >= 40) return 'from-amber-500 to-amber-400';
  return 'from-red-500 to-red-400';
}

/**
 * CacheHitGauge Component
 *
 * Displays cache hit rate as a circular gauge with supporting metrics.
 */
export function CacheHitGauge({
  data,
  className,
}: CacheHitGaugeProps): React.JSX.Element {
  const circumference = 2 * Math.PI * 45; // radius = 45
  const dashOffset = circumference - (data.hitRate / 100) * circumference;

  return (
    <Card className={cn('h-full', className)}>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-semibold flex items-center gap-2">
          <Database className="h-4 w-4 text-purple-500" />
          Cache Performance
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col items-center">
        {/* Circular gauge */}
        <div className="relative w-32 h-32">
          <svg className="w-full h-full transform -rotate-90" viewBox="0 0 100 100">
            {/* Background circle */}
            <circle
              cx="50"
              cy="50"
              r="45"
              fill="none"
              stroke="currentColor"
              strokeWidth="8"
              className="text-muted"
            />
            {/* Progress circle */}
            <motion.circle
              cx="50"
              cy="50"
              r="45"
              fill="none"
              stroke="url(#cacheGradient)"
              strokeWidth="8"
              strokeLinecap="round"
              strokeDasharray={circumference}
              initial={{ strokeDashoffset: circumference }}
              animate={{ strokeDashoffset: dashOffset }}
              transition={{ duration: 1, ease: 'easeOut' }}
            />
            <defs>
              <linearGradient id="cacheGradient" x1="0%" y1="0%" x2="100%" y2="0%">
                <stop offset="0%" className={cn('stop-current', getHitRateColor(data.hitRate))} />
                <stop offset="100%" className={cn('stop-current', getHitRateColor(data.hitRate))} style={{ opacity: 0.6 }} />
              </linearGradient>
            </defs>
          </svg>
          {/* Center text */}
          <div className="absolute inset-0 flex flex-col items-center justify-center">
            <motion.span
              initial={{ opacity: 0, scale: 0.5 }}
              animate={{ opacity: 1, scale: 1 }}
              className={cn('text-2xl font-bold', getHitRateColor(data.hitRate))}
            >
              {data.hitRate}%
            </motion.span>
            <span className="text-xs text-muted-foreground">Hit Rate</span>
          </div>
        </div>

        {/* Stats */}
        <div className="grid grid-cols-2 gap-4 mt-4 w-full text-xs">
          <div className="text-center">
            <div className="text-muted-foreground">Hits</div>
            <div className="font-medium text-green-500">{data.totalHits.toLocaleString()}</div>
          </div>
          <div className="text-center">
            <div className="text-muted-foreground">Misses</div>
            <div className="font-medium text-red-400">{data.totalMisses.toLocaleString()}</div>
          </div>
        </div>

        {/* Cache size */}
        <div className="pt-2 mt-2 border-t w-full text-center text-xs text-muted-foreground">
          Cache: {data.cacheSize}MB • TTL: {Math.round(data.ttlSeconds / 60)}min
        </div>
      </CardContent>
    </Card>
  );
}
