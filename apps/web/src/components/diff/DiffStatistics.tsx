import React from 'react';
import { DiffStatistics as DiffStats } from '../../lib/diffProcessor';

export interface DiffStatisticsProps {
  statistics: DiffStats;
  compact?: boolean;
}

/**
 * Enhanced statistics header showing diff summary
 * Displays added, deleted, modified, and unchanged line counts
 */
export function DiffStatistics({ statistics, compact = false }: DiffStatisticsProps) {
  if (compact) {
    return (
      <div className="diff-statistics diff-statistics--compact">
        <span className="diff-stat diff-stat--added" title="Added lines">
          +{statistics.added}
        </span>
        <span className="diff-stat diff-stat--deleted" title="Deleted lines">
          -{statistics.deleted}
        </span>
        {statistics.modified > 0 && (
          <span className="diff-stat diff-stat--modified" title="Modified lines">
            ~{statistics.modified}
          </span>
        )}
      </div>
    );
  }

  return (
    <div className="diff-statistics" data-testid="diff-statistics">
      <div className="diff-stat-item diff-stat-item--added">
        <span className="diff-stat-value">+{statistics.added}</span>
        <span className="diff-stat-label">Added</span>
      </div>
      <div className="diff-stat-item diff-stat-item--deleted">
        <span className="diff-stat-value">-{statistics.deleted}</span>
        <span className="diff-stat-label">Deleted</span>
      </div>
      {statistics.modified > 0 && (
        <div className="diff-stat-item diff-stat-item--modified">
          <span className="diff-stat-value">~{statistics.modified}</span>
          <span className="diff-stat-label">Modified</span>
        </div>
      )}
      <div className="diff-stat-item diff-stat-item--unchanged">
        <span className="diff-stat-value">{statistics.unchanged}</span>
        <span className="diff-stat-label">Unchanged</span>
      </div>
      <div className="diff-stat-item diff-stat-item--total">
        <span className="diff-stat-value">{statistics.totalLines}</span>
        <span className="diff-stat-label">Total Lines</span>
      </div>
    </div>
  );
}
