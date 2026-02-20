/**
 * VectorCollectionCard Component
 * Issue #4861: MeepleCard design system for /admin/knowledge-base/vectors
 *
 * Displays a Qdrant vector collection using MeepleCard entity="document" variant="grid"
 * with collection metrics, health status badge, and admin quick actions.
 */

'use client';

import { Database, HardDrive, Layers, RefreshCw, Settings, Trash2 } from 'lucide-react';

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';

export interface VectorCollectionStatus {
  health: 'healthy' | 'degraded' | 'error';
  optimizerStatus?: string;
}

export interface VectorCollectionDto {
  name: string;
  vectorCount: number;
  dimensions: number;
  storage: string;
  status: VectorCollectionStatus;
}

interface VectorCollectionCardProps {
  collection: VectorCollectionDto;
  onOptimize?: (name: string) => void;
  onReindex?: (name: string) => void;
  onDelete?: (name: string) => void;
}

const healthLabels: Record<string, string> = {
  healthy: 'Healthy',
  degraded: 'Degraded',
  error: 'Error',
};

export function VectorCollectionCard({
  collection,
  onOptimize,
  onReindex,
  onDelete,
}: VectorCollectionCardProps) {
  const metadata: MeepleCardMetadata[] = [
    { icon: Database, label: `${collection.vectorCount.toLocaleString()} vectors` },
    { icon: Layers, label: `${collection.dimensions}D` },
    { icon: HardDrive, label: collection.storage },
  ];

  const quickActions = [
    ...(onOptimize
      ? [{ icon: Settings, label: 'Optimize', onClick: () => onOptimize(collection.name) }]
      : []),
    ...(onReindex
      ? [{ icon: RefreshCw, label: 'Reindex', onClick: () => onReindex(collection.name) }]
      : []),
    ...(onDelete
      ? [
          {
            icon: Trash2,
            label: 'Delete',
            onClick: () => onDelete(collection.name),
            destructive: true,
          },
        ]
      : []),
  ];

  return (
    <MeepleCard
      entity="document"
      variant="grid"
      title={collection.name}
      subtitle={`${collection.vectorCount.toLocaleString()} vettori \u00b7 ${collection.dimensions}D`}
      badge={healthLabels[collection.status.health] || collection.status.health}
      metadata={metadata}
      quickActions={quickActions.length > 0 ? quickActions : undefined}
      data-testid={`vector-collection-card-${collection.name}`}
    />
  );
}
