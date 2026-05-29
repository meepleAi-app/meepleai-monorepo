'use client';

import type { IngestionStep } from '@/lib/api/schemas/ingestion-log.schemas';

import { IngestionTimelineStep } from './IngestionTimelineStep';

const PIPELINE_ORDER: ReadonlyArray<IngestionStep['stepName']> = [
  'Upload',
  'Extract',
  'Chunk',
  'Embed',
  'Index',
];

interface IngestionTimelineProps {
  readonly steps: ReadonlyArray<IngestionStep>;
}

function pendingPlaceholder(name: IngestionStep['stepName']): IngestionStep {
  return {
    id: `pending-${name}`,
    stepName: name,
    status: 'pending',
    startedAt: null,
    completedAt: null,
    durationMs: null,
    metadataJson: null,
    logEntries: [],
  };
}

/**
 * Always renders the 5 pipeline steps in fixed order (Upload → Extract → Chunk →
 * Embed → Index). Missing steps from backend are rendered as `pending`
 * placeholders. Issue #1650.
 */
export function IngestionTimeline({ steps }: IngestionTimelineProps) {
  const byName = new Map(steps.map(s => [s.stepName, s] as const));
  const ordered = PIPELINE_ORDER.map(n => byName.get(n) ?? pendingPlaceholder(n));

  return (
    <ol className="flex flex-col gap-0 py-1.5 list-none m-0 p-0">
      {ordered.map((step, idx) => (
        <IngestionTimelineStep
          key={step.id}
          step={step}
          index={idx}
          isLast={idx === ordered.length - 1}
        />
      ))}
    </ol>
  );
}
