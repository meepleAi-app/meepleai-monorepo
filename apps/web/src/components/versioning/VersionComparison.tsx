'use client';

/**
 * VersionComparison Component (Issue #3355)
 * Side-by-side diff view comparing two versions with highlighted changes.
 */

import React, { useState, useEffect, useCallback } from 'react';

import { GitCompare, Plus, Minus, RefreshCw, Equal, X } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

import { ChangeItem } from './ChangeItem';

import type { VersionDiff, RuleAtomChange, DiffSummary } from './types';

interface VersionComparisonProps {
  gameId: string;
  fromVersion: string;
  toVersion: string;
  onClose?: () => void;
  className?: string;
}

function DiffSummaryBadges({ summary }: { summary: DiffSummary }): React.JSX.Element {
  return (
    <div className="flex items-center gap-2 flex-wrap">
      {summary.added > 0 && (
        <Badge variant="default" className="bg-green-600 hover:bg-green-700">
          <Plus className="h-3 w-3 mr-1" />
          {summary.added} Added
        </Badge>
      )}
      {summary.modified > 0 && (
        <Badge variant="default" className="bg-amber-500 hover:bg-amber-600">
          <RefreshCw className="h-3 w-3 mr-1" />
          {summary.modified} Modified
        </Badge>
      )}
      {summary.deleted > 0 && (
        <Badge variant="default" className="bg-red-600 hover:bg-red-700">
          <Minus className="h-3 w-3 mr-1" />
          {summary.deleted} Deleted
        </Badge>
      )}
      {summary.unchanged > 0 && (
        <Badge variant="secondary">
          <Equal className="h-3 w-3 mr-1" />
          {summary.unchanged} Unchanged
        </Badge>
      )}
    </div>
  );
}

type FilterType = 'all' | 'Added' | 'Modified' | 'Deleted';

export function VersionComparison({
  gameId,
  fromVersion,
  toVersion,
  onClose,
  className,
}: VersionComparisonProps): React.JSX.Element {
  const [diff, setDiff] = useState<VersionDiff | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<FilterType>('all');
  const [showUnchanged, setShowUnchanged] = useState(false);

  const fetchDiff = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await fetch(
        `${process.env.NEXT_PUBLIC_API_BASE}/api/v1/games/${gameId}/rulespec/diff?fromVersion=${fromVersion}&toVersion=${toVersion}`,
        { credentials: 'include' }
      );

      if (!response.ok) {
        throw new Error('Failed to fetch version diff');
      }

      const data: VersionDiff = await response.json();
      setDiff(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  }, [gameId, fromVersion, toVersion]);

  useEffect(() => {
    if (gameId && fromVersion && toVersion) {
      fetchDiff();
    }
  }, [gameId, fromVersion, toVersion, fetchDiff]);

  const formatDate = (dateString: string): string => {
    return new Date(dateString).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  const getFilteredChanges = (): RuleAtomChange[] => {
    if (!diff) return [];

    return diff.changes.filter(change => {
      if (change.type === 'Unchanged' && !showUnchanged) return false;
      if (filter === 'all') return true;
      return change.type === filter;
    });
  };

  if (loading) {
    return (
      <Card className={className}>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <GitCompare className="h-5 w-5" />
            Comparing Versions
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-center p-8" role="status" aria-live="polite">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary" />
            <span className="sr-only">Loading comparison...</span>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (error) {
    return (
      <Card className={className}>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="flex items-center gap-2">
            <GitCompare className="h-5 w-5" />
            Comparing Versions
          </CardTitle>
          {onClose && (
            <Button variant="ghost" size="icon" onClick={onClose}>
              <X className="h-4 w-4" />
            </Button>
          )}
        </CardHeader>
        <CardContent>
          <div className="bg-destructive/10 border border-destructive/20 text-destructive px-4 py-3 rounded-md">
            <strong className="font-semibold">Error:</strong> {error}
          </div>
        </CardContent>
      </Card>
    );
  }

  if (!diff) return <></>;

  const filteredChanges = getFilteredChanges();

  return (
    <Card className={className}>
      <CardHeader>
        <div className="flex items-start justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <GitCompare className="h-5 w-5" />
              Version Comparison
            </CardTitle>
            <div className="mt-2 flex items-center gap-4 text-sm text-muted-foreground">
              <div className="flex items-center gap-2">
                <Badge variant="outline">{diff.fromVersion}</Badge>
                <span>{formatDate(diff.fromCreatedAt)}</span>
              </div>
              <span className="text-lg">&rarr;</span>
              <div className="flex items-center gap-2">
                <Badge variant="default">{diff.toVersion}</Badge>
                <span>{formatDate(diff.toCreatedAt)}</span>
              </div>
            </div>
          </div>
          {onClose && (
            <Button variant="ghost" size="icon" onClick={onClose}>
              <X className="h-4 w-4" />
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Summary */}
        <div className="flex items-center justify-between flex-wrap gap-4 p-4 bg-muted/50 rounded-lg">
          <div>
            <span className="text-sm font-medium">
              {diff.summary.totalChanges} total changes
            </span>
          </div>
          <DiffSummaryBadges summary={diff.summary} />
        </div>

        {/* Filters */}
        <div className="flex items-center justify-between flex-wrap gap-4">
          <div className="flex items-center gap-2">
            <span className="text-sm text-muted-foreground">Filter:</span>
            {(['all', 'Added', 'Modified', 'Deleted'] as const).map(f => (
              <Button
                key={f}
                variant={filter === f ? 'default' : 'outline'}
                size="sm"
                onClick={() => setFilter(f)}
              >
                {f === 'all' ? 'All' : f}
              </Button>
            ))}
          </div>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setShowUnchanged(!showUnchanged)}
          >
            {showUnchanged ? 'Hide' : 'Show'} Unchanged
          </Button>
        </div>

        {/* Changes List */}
        <div className="space-y-3" data-testid="version-comparison-changes">
          {filteredChanges.length === 0 ? (
            <div className="text-center p-8 text-muted-foreground">
              No changes match the current filter.
            </div>
          ) : (
            filteredChanges.map((change, index) => (
              <ChangeItem key={`${change.oldAtom || change.newAtom}-${index}`} change={change} />
            ))
          )}
        </div>
      </CardContent>
    </Card>
  );
}

export default VersionComparison;
