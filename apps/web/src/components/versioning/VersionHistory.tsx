'use client';

/**
 * VersionHistory Component (Issue #3355)
 * Displays a list of versions with actions to view, compare, and restore.
 */

import { useState, useEffect, useCallback } from 'react';

import { History, GitCompare, Eye, RotateCcw, Calendar, User, Hash } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { VersionNode, VersionHistoryResponse } from './types';

interface VersionHistoryProps {
  gameId: string;
  onViewVersion?: (version: string) => void;
  onCompareVersions?: (fromVersion: string, toVersion: string) => void;
  onRestoreVersion?: (version: string) => Promise<void>;
  className?: string;
}

export function VersionHistory({
  gameId,
  onViewVersion,
  onCompareVersions,
  onRestoreVersion,
  className,
}: VersionHistoryProps): React.JSX.Element {
  const [versions, setVersions] = useState<VersionNode[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedForCompare, setSelectedForCompare] = useState<string | null>(null);
  const [restoreDialogOpen, setRestoreDialogOpen] = useState(false);
  const [versionToRestore, setVersionToRestore] = useState<string | null>(null);
  const [restoring, setRestoring] = useState(false);

  const fetchVersionHistory = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await fetch(
        `${process.env.NEXT_PUBLIC_API_BASE}/api/v1/games/${gameId}/rulespec/history`,
        { credentials: 'include' }
      );

      if (!response.ok) {
        throw new Error('Failed to fetch version history');
      }

      const data: VersionHistoryResponse = await response.json();
      setVersions(data.versions || []);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  }, [gameId]);

  useEffect(() => {
    if (gameId) {
      fetchVersionHistory();
    }
  }, [gameId, fetchVersionHistory]);

  const handleCompareClick = (version: string) => {
    if (selectedForCompare === null) {
      setSelectedForCompare(version);
    } else if (selectedForCompare === version) {
      setSelectedForCompare(null);
    } else {
      // Compare the two versions (older first)
      const versions1 = [selectedForCompare, version];
      versions1.sort();
      onCompareVersions?.(versions1[0], versions1[1]);
      setSelectedForCompare(null);
    }
  };

  const handleRestoreClick = (version: string) => {
    setVersionToRestore(version);
    setRestoreDialogOpen(true);
  };

  const confirmRestore = async () => {
    if (!versionToRestore || !onRestoreVersion) return;

    try {
      setRestoring(true);
      await onRestoreVersion(versionToRestore);
      await fetchVersionHistory(); // Refresh after restore
    } finally {
      setRestoring(false);
      setRestoreDialogOpen(false);
      setVersionToRestore(null);
    }
  };

  const formatDate = (dateString: string): string => {
    return new Date(dateString).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  if (loading) {
    return (
      <Card className={className}>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <History className="h-5 w-5" />
            Version History
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-center p-8" role="status" aria-live="polite">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary" />
            <span className="sr-only">Loading version history...</span>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (error) {
    return (
      <Card className={className}>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <History className="h-5 w-5" />
            Version History
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="bg-destructive/10 border border-destructive/20 text-destructive px-4 py-3 rounded-md">
            <strong className="font-semibold">Error:</strong> {error}
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <>
      <Card className={className}>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <History className="h-5 w-5" />
            Version History
            {selectedForCompare && (
              <Badge variant="secondary" className="ml-2">
                Select another version to compare
              </Badge>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {versions.length === 0 ? (
            <div className="text-center p-8 text-muted-foreground">
              No version history available.
            </div>
          ) : (
            <div className="space-y-3" data-testid="version-history-list">
              {versions.map((version, index) => (
                <div
                  key={version.id}
                  className={cn(
                    'border rounded-lg p-4 transition-colors',
                    version.isCurrentVersion && 'border-primary bg-primary/5',
                    selectedForCompare === version.version && 'ring-2 ring-primary',
                    !version.isCurrentVersion && !selectedForCompare && 'hover:bg-muted/50'
                  )}
                  data-testid={`version-item-${version.version}`}
                >
                  <div className="flex items-start justify-between">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-lg">{version.version}</span>
                        {version.isCurrentVersion && (
                          <Badge variant="default">Current</Badge>
                        )}
                        {index === versions.length - 1 && (
                          <Badge variant="outline">Initial</Badge>
                        )}
                      </div>
                      {version.description && (
                        <p className="text-sm text-muted-foreground">
                          &ldquo;{version.description}&rdquo;
                        </p>
                      )}
                      <div className="flex items-center gap-4 text-xs text-muted-foreground">
                        <span className="flex items-center gap-1">
                          <Calendar className="h-3 w-3" />
                          {formatDate(version.createdAt)}
                        </span>
                        <span className="flex items-center gap-1">
                          <User className="h-3 w-3" />
                          {version.author}
                        </span>
                        <span className="flex items-center gap-1">
                          <Hash className="h-3 w-3" />
                          {version.changeCount} changes
                        </span>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <Button
                        variant={selectedForCompare === version.version ? 'default' : 'outline'}
                        size="sm"
                        onClick={() => handleCompareClick(version.version)}
                        title={
                          selectedForCompare === version.version
                            ? 'Cancel comparison'
                            : selectedForCompare
                              ? 'Compare with selected'
                              : 'Select for comparison'
                        }
                      >
                        <GitCompare className="h-4 w-4" />
                        <span className="ml-1 hidden sm:inline">Compare</span>
                      </Button>
                      {onViewVersion && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => onViewVersion(version.version)}
                          title="View this version"
                        >
                          <Eye className="h-4 w-4" />
                          <span className="ml-1 hidden sm:inline">View</span>
                        </Button>
                      )}
                      {!version.isCurrentVersion && onRestoreVersion && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => handleRestoreClick(version.version)}
                          title="Restore this version"
                        >
                          <RotateCcw className="h-4 w-4" />
                          <span className="ml-1 hidden sm:inline">Restore</span>
                        </Button>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={restoreDialogOpen} onOpenChange={setRestoreDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Restore Version</DialogTitle>
            <DialogDescription>
              Are you sure you want to restore version <strong>{versionToRestore}</strong>?
              This will create a new version based on the selected one.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setRestoreDialogOpen(false)}
              disabled={restoring}
            >
              Cancel
            </Button>
            <Button onClick={confirmRestore} disabled={restoring}>
              {restoring ? 'Restoring...' : 'Restore'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

export default VersionHistory;
