/**
 * VectorCollectionCard Component
 * Issue #4861: MeepleCard design system for /admin/knowledge-base/vectors
 * Issue #4877: Qdrant Advanced Operations + Delete
 *
 * Displays a Qdrant vector collection using MeepleCard entity="document" variant="grid"
 * with collection metrics, health status badge, and admin quick actions.
 */

'use client';

import { Database, Layers, RefreshCw, Trash2 } from 'lucide-react';

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';

export interface VectorCollectionCardProps {
  name: string;
  vectorCount: number;
  dimensions: number;
  storage: string;
  health: number;
  onReindex?: (name: string) => void;
  onDelete?: (name: string) => void;
}

function healthLabel(health: number): string {
  if (health >= 90) return 'Healthy';
  if (health >= 70) return 'Degraded';
  return 'Error';
}

export function VectorCollectionCard({
  name,
  vectorCount,
  dimensions,
  storage,
  health,
  onReindex,
  onDelete,
}: VectorCollectionCardProps) {
  const metadata: MeepleCardMetadata[] = [
    { icon: Database, label: `${vectorCount.toLocaleString()} vectors` },
    { icon: Layers, label: `${dimensions}D` },
  ];

  const quickActions = [
    ...(onReindex
      ? [{ icon: RefreshCw, label: 'Rebuild Index', onClick: () => onReindex(name) }]
      : []),
    ...(onDelete
      ? [
          {
            icon: Trash2,
            label: 'Delete',
            onClick: () => onDelete(name),
            destructive: true,
          },
        ]
      : []),
  ];

  return (
    <MeepleCard
      entity="document"
      variant="grid"
      title={name}
      subtitle={`${vectorCount.toLocaleString()} vectors · ${dimensions}D · ${storage}`}
      badge={healthLabel(health)}
      metadata={metadata}
      quickActions={quickActions.length > 0 ? quickActions : undefined}
      data-testid={`vector-collection-card-${name}`}
    />
  );
}
