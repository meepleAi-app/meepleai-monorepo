'use client';

import React from 'react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

export interface VersionItem {
  id: string;
  versionNumber: number;
  content: string;
  label?: string;
  createdAt: string;
  createdBy?: string;
  isCurrent?: boolean;
}

export interface VersionHistoryProps {
  versions: VersionItem[];
  onCompare?: (versionId: string) => void;
  onView?: (versionId: string) => void;
  onRestore?: (versionId: string) => void;
  isLoading?: boolean;
  title?: string;
}

export function VersionHistory({
  versions,
  onCompare,
  onView,
  onRestore,
  isLoading = false,
  title = 'Version History',
}: VersionHistoryProps) {
  if (isLoading) {
    return (
      <Card className="bg-white/70 backdrop-blur-md">
        <CardHeader>
          <CardTitle className="font-quicksand">{title}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-3" data-testid="version-history-loading">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-20 bg-muted/50 rounded-lg animate-pulse" />
            ))}
          </div>
        </CardContent>
      </Card>
    );
  }

  if (versions.length === 0) {
    return (
      <Card className="bg-white/70 backdrop-blur-md">
        <CardHeader>
          <CardTitle className="font-quicksand">{title}</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground" data-testid="version-history-empty">
            No versions available.
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="bg-white/70 backdrop-blur-md">
      <CardHeader>
        <CardTitle className="font-quicksand">{title}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-3" data-testid="version-history-list">
          {versions.map((version) => (
            <div
              key={version.id}
              className="flex items-start justify-between rounded-lg border border-border/60 p-4 transition-colors hover:bg-muted/30"
              data-testid={`version-item-${version.versionNumber}`}
            >
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <span className="font-quicksand font-semibold text-sm">
                    v{version.versionNumber}
                  </span>
                  {version.isCurrent && (
                    <Badge
                      variant="default"
                      className="bg-green-100 text-green-800 hover:bg-green-100/80 border-transparent text-xs"
                      data-testid="current-badge"
                    >
                      Current
                    </Badge>
                  )}
                  <span className="text-xs text-muted-foreground">
                    {new Date(version.createdAt).toLocaleDateString()}
                  </span>
                </div>
                {version.label && (
                  <p className="mt-1 text-sm text-muted-foreground font-nunito truncate">
                    &ldquo;{version.label}&rdquo;
                  </p>
                )}
                {version.createdBy && (
                  <p className="mt-0.5 text-xs text-muted-foreground/70">
                    by {version.createdBy}
                  </p>
                )}
              </div>
              <div className="flex items-center gap-1.5 ml-3 shrink-0">
                {onCompare && (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => onCompare(version.id)}
                    data-testid={`compare-btn-${version.versionNumber}`}
                  >
                    Compare
                  </Button>
                )}
                {onView && (
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => onView(version.id)}
                    data-testid={`view-btn-${version.versionNumber}`}
                  >
                    View
                  </Button>
                )}
                {onRestore && !version.isCurrent && (
                  <Button
                    size="sm"
                    variant="secondary"
                    onClick={() => onRestore(version.id)}
                    data-testid={`restore-btn-${version.versionNumber}`}
                  >
                    Restore
                  </Button>
                )}
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
