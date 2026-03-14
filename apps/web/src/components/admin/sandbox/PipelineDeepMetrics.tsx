'use client';

import { useMemo } from 'react';

import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

/** Bucket for chunk size distribution histogram */
export interface ChunkSizeBucket {
  /** e.g. "0-200", "200-500" */
  rangeLabel: string;
  count: number;
}

/** Qdrant collection statistics */
export interface QdrantStats {
  vectorsCount: number;
  memoryUsageBytes: number;
  collectionStatus: 'green' | 'yellow' | 'red';
}

/** Quality indicator values */
export interface QualityIndicators {
  pageCoveragePercent: number;
  emptyChunksCount: number;
  duplicateDetectionCount: number;
}

export interface DeepMetricsData {
  chunkSizeDistribution?: ChunkSizeBucket[];
  qdrantStats?: QdrantStats;
  qualityIndicators?: QualityIndicators;
}

interface PipelineDeepMetricsProps {
  metrics?: DeepMetricsData;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

const COLLECTION_STATUS_CLASSES: Record<string, string> = {
  green: 'bg-emerald-100 text-emerald-800',
  yellow: 'bg-amber-100 text-amber-800',
  red: 'bg-red-100 text-red-800',
};

const COLLECTION_STATUS_LABEL: Record<string, string> = {
  green: 'Operativa',
  yellow: 'Degradata',
  red: 'Errore',
};

export function PipelineDeepMetrics({ metrics }: PipelineDeepMetricsProps) {
  if (!metrics) {
    return (
      <p className="font-nunito text-sm text-muted-foreground text-center py-4">
        Nessuna metrica disponibile
      </p>
    );
  }

  return (
    <div className="space-y-6">
      {/* Chunk size distribution */}
      {metrics.chunkSizeDistribution && metrics.chunkSizeDistribution.length > 0 && (
        <ChunkDistributionChart buckets={metrics.chunkSizeDistribution} />
      )}

      {/* Qdrant stats */}
      {metrics.qdrantStats && <QdrantStatsCard stats={metrics.qdrantStats} />}

      {/* Quality indicators */}
      {metrics.qualityIndicators && (
        <QualityIndicatorsCard indicators={metrics.qualityIndicators} />
      )}
    </div>
  );
}

function ChunkDistributionChart({ buckets }: { buckets: ChunkSizeBucket[] }) {
  const maxCount = useMemo(() => Math.max(...buckets.map(b => b.count), 1), [buckets]);

  return (
    <div className="space-y-2">
      <h4 className="font-quicksand text-sm font-semibold">Distribuzione dimensione chunk</h4>
      <div className="space-y-1.5" data-testid="chunk-distribution">
        {buckets.map(bucket => {
          const widthPercent = (bucket.count / maxCount) * 100;
          return (
            <div key={bucket.rangeLabel} className="flex items-center gap-2">
              <span className="w-20 shrink-0 font-mono text-xs text-muted-foreground text-right">
                {bucket.rangeLabel}
              </span>
              <div className="flex-1 h-5 bg-gray-100 rounded overflow-hidden">
                <div
                  data-testid={`bar-${bucket.rangeLabel}`}
                  className="h-full bg-amber-400 rounded transition-all"
                  style={{ width: `${widthPercent}%` }}
                />
              </div>
              <span className="w-10 shrink-0 font-mono text-xs text-right font-medium">
                {bucket.count}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function QdrantStatsCard({ stats }: { stats: QdrantStats }) {
  return (
    <div className="space-y-2">
      <h4 className="font-quicksand text-sm font-semibold">Statistiche Qdrant</h4>
      <div
        data-testid="qdrant-stats"
        className="grid grid-cols-3 gap-3 rounded-lg border bg-white/50 p-3"
      >
        <div className="text-center">
          <p className="font-mono text-lg font-bold" data-testid="vectors-count">
            {stats.vectorsCount.toLocaleString()}
          </p>
          <p className="font-nunito text-xs text-muted-foreground">Vettori</p>
        </div>
        <div className="text-center">
          <p className="font-mono text-lg font-bold" data-testid="memory-usage">
            {formatBytes(stats.memoryUsageBytes)}
          </p>
          <p className="font-nunito text-xs text-muted-foreground">Memoria</p>
        </div>
        <div className="flex flex-col items-center justify-center">
          <Badge
            data-testid="collection-status"
            className={cn('text-xs', COLLECTION_STATUS_CLASSES[stats.collectionStatus] ?? '')}
          >
            {COLLECTION_STATUS_LABEL[stats.collectionStatus] ?? stats.collectionStatus}
          </Badge>
          <p className="font-nunito text-xs text-muted-foreground mt-1">Stato</p>
        </div>
      </div>
    </div>
  );
}

function QualityIndicatorsCard({ indicators }: { indicators: QualityIndicators }) {
  return (
    <div className="space-y-2">
      <h4 className="font-quicksand text-sm font-semibold">Indicatori qualita</h4>
      <div data-testid="quality-indicators" className="flex flex-wrap gap-3">
        <Badge
          data-testid="page-coverage"
          className={cn(
            'text-xs',
            indicators.pageCoveragePercent >= 90
              ? 'bg-emerald-100 text-emerald-800'
              : indicators.pageCoveragePercent >= 70
                ? 'bg-amber-100 text-amber-800'
                : 'bg-red-100 text-red-800'
          )}
        >
          Copertura pagine: {indicators.pageCoveragePercent}%
        </Badge>
        <Badge
          data-testid="empty-chunks"
          className={cn(
            'text-xs',
            indicators.emptyChunksCount === 0
              ? 'bg-emerald-100 text-emerald-800'
              : 'bg-amber-100 text-amber-800'
          )}
        >
          Chunk vuoti: {indicators.emptyChunksCount}
        </Badge>
        <Badge
          data-testid="duplicates"
          className={cn(
            'text-xs',
            indicators.duplicateDetectionCount === 0
              ? 'bg-emerald-100 text-emerald-800'
              : 'bg-amber-100 text-amber-800'
          )}
        >
          Duplicati: {indicators.duplicateDetectionCount}
        </Badge>
      </div>
    </div>
  );
}
