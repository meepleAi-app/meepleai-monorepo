'use client';

import React, { useState, useEffect } from 'react';

import { motion } from 'framer-motion';
import { RefreshCw, Loader2, AlertCircle, Download } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

import { LatencyChart } from './LatencyChart';
import { TokenDistribution } from './TokenDistribution';
import { CacheHitGauge } from './CacheHitGauge';
import { AccuracyScore } from './AccuracyScore';
import { CostBreakdown } from './CostBreakdown';
import { MOCK_RAG_METRICS, generateMockMetrics } from './mock-data';

import type { PerformanceMetricsProps } from './types';

/**
 * PerformanceMetrics Container Component
 *
 * Displays all 5 performance metric widgets in a responsive grid.
 * Features:
 * - Real-time updates via SSE (with mock fallback)
 * - Refresh button for manual updates
 * - Loading and error states
 * - Export data option
 * - Responsive layout (1-2-3 columns)
 */
export function PerformanceMetrics({
  metrics: externalMetrics,
  isLoading: externalLoading,
  error: externalError,
  onRefresh,
  className,
}: PerformanceMetricsProps): React.JSX.Element {
  // Use external data if provided, otherwise use mock data
  const [internalMetrics, setInternalMetrics] = useState(MOCK_RAG_METRICS);
  const [isRefreshing, setIsRefreshing] = useState(false);

  const metrics = externalMetrics ?? internalMetrics;
  const isLoading = externalLoading ?? false;
  const error = externalError ?? null;

  // Simulate real-time updates when using mock data
  useEffect(() => {
    if (externalMetrics) return; // Skip if external data provided

    const interval = setInterval(() => {
      setInternalMetrics(generateMockMetrics());
    }, 30000); // Update every 30 seconds

    return () => clearInterval(interval);
  }, [externalMetrics]);

  const handleRefresh = async () => {
    setIsRefreshing(true);
    if (onRefresh) {
      await onRefresh();
    } else {
      // Simulate refresh with mock data
      await new Promise((resolve) => setTimeout(resolve, 500));
      setInternalMetrics(generateMockMetrics());
    }
    setIsRefreshing(false);
  };

  const handleExport = () => {
    const dataStr = JSON.stringify(metrics, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `rag-metrics-${new Date().toISOString().split('T')[0]}.json`;
    link.click();
    URL.revokeObjectURL(url);
  };

  if (error) {
    return (
      <div className={cn('p-6 border rounded-lg bg-destructive/10', className)}>
        <div className="flex items-center gap-3 text-destructive">
          <AlertCircle className="h-5 w-5" />
          <div>
            <p className="font-medium">Failed to load metrics</p>
            <p className="text-sm text-muted-foreground">{error}</p>
          </div>
        </div>
        <Button variant="outline" size="sm" className="mt-4" onClick={handleRefresh}>
          Try Again
        </Button>
      </div>
    );
  }

  return (
    <div className={cn('space-y-4', className)}>
      {/* Header with controls */}
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold">Performance Metrics</h3>
          <p className="text-sm text-muted-foreground">
            Real-time analytics for RAG system performance
          </p>
        </div>
        <div className="flex items-center gap-2">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleExport}
                  disabled={isLoading}
                >
                  <Download className="h-4 w-4" />
                </Button>
              </TooltipTrigger>
              <TooltipContent>Export as JSON</TooltipContent>
            </Tooltip>
          </TooltipProvider>
          <Button
            variant="outline"
            size="sm"
            onClick={handleRefresh}
            disabled={isLoading || isRefreshing}
          >
            {isRefreshing ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <RefreshCw className="h-4 w-4" />
            )}
            <span className="ml-2 hidden sm:inline">Refresh</span>
          </Button>
        </div>
      </div>

      {/* Last updated timestamp */}
      <p className="text-xs text-muted-foreground">
        Last updated: {new Date(metrics.timestamp).toLocaleTimeString()}
      </p>

      {/* Metrics grid */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {[...Array(5)].map((_, i) => (
            <div
              key={i}
              className="h-48 rounded-lg bg-muted/50 animate-pulse"
            />
          ))}
        </div>
      ) : (
        <motion.div
          className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ duration: 0.3 }}
        >
          <LatencyChart data={metrics.latency} targetThreshold={500} />
          <TokenDistribution data={metrics.tokenUsage} />
          <CacheHitGauge data={metrics.cache} />
          <AccuracyScore data={metrics.accuracy} />
          <CostBreakdown data={metrics.cost} />
        </motion.div>
      )}
    </div>
  );
}
