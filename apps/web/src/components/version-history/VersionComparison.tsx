'use client';

import React, { useMemo } from 'react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { processDiff } from '@/lib/diffProcessor';

import type { VersionItem } from './VersionHistory';

export interface VersionComparisonProps {
  leftVersion: VersionItem;
  rightVersion: VersionItem;
  onClose?: () => void;
}

export function VersionComparison({
  leftVersion,
  rightVersion,
  onClose,
}: VersionComparisonProps) {
  const diff = useMemo(
    () => processDiff(leftVersion.content, rightVersion.content),
    [leftVersion.content, rightVersion.content]
  );

  return (
    <Card className="bg-white/70 backdrop-blur-md">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="font-quicksand">
            Comparing v{leftVersion.versionNumber} vs v{rightVersion.versionNumber}
          </CardTitle>
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2 text-xs">
              <Badge variant="default" className="bg-green-100 text-green-800 border-transparent">
                +{diff.statistics.added}
              </Badge>
              <Badge variant="default" className="bg-red-100 text-red-800 border-transparent">
                -{diff.statistics.deleted}
              </Badge>
              <Badge variant="secondary">
                ~{diff.statistics.modified}
              </Badge>
            </div>
            {onClose && (
              <Button size="sm" variant="ghost" onClick={onClose} data-testid="close-comparison">
                Close
              </Button>
            )}
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-2 gap-0 border border-border/60 rounded-lg overflow-hidden" data-testid="diff-panel">
          {/* Left panel header */}
          <div className="bg-red-50/50 px-3 py-2 border-b border-r border-border/60">
            <span className="text-xs font-semibold text-red-700">
              v{leftVersion.versionNumber}
            </span>
            {leftVersion.label && (
              <span className="ml-2 text-xs text-muted-foreground">{leftVersion.label}</span>
            )}
          </div>
          {/* Right panel header */}
          <div className="bg-green-50/50 px-3 py-2 border-b border-border/60">
            <span className="text-xs font-semibold text-green-700">
              v{rightVersion.versionNumber}
            </span>
            {rightVersion.label && (
              <span className="ml-2 text-xs text-muted-foreground">{rightVersion.label}</span>
            )}
          </div>

          {/* Left panel content */}
          <div className="p-3 border-r border-border/60 font-mono text-xs leading-relaxed overflow-auto max-h-[500px]">
            {diff.oldLines.length > 0 ? (
              diff.oldLines.map((line, idx) => (
                <div
                  key={idx}
                  className={`px-1.5 py-0.5 rounded-sm ${
                    line.type === 'deleted'
                      ? 'bg-red-100/70 text-red-900'
                      : line.type === 'modified'
                        ? 'bg-amber-100/50 text-amber-900'
                        : 'text-foreground/80'
                  }`}
                >
                  <span className="inline-block w-8 text-right text-muted-foreground/50 mr-2 select-none">
                    {line.lineNumber ?? ''}
                  </span>
                  {line.type === 'deleted' && <span className="text-red-500 mr-1">-</span>}
                  {line.content}
                </div>
              ))
            ) : (
              <span className="text-muted-foreground italic">Empty</span>
            )}
          </div>

          {/* Right panel content */}
          <div className="p-3 font-mono text-xs leading-relaxed overflow-auto max-h-[500px]">
            {diff.newLines.length > 0 ? (
              diff.newLines.map((line, idx) => (
                <div
                  key={idx}
                  className={`px-1.5 py-0.5 rounded-sm ${
                    line.type === 'added'
                      ? 'bg-green-100/70 text-green-900'
                      : line.type === 'modified'
                        ? 'bg-amber-100/50 text-amber-900'
                        : 'text-foreground/80'
                  }`}
                >
                  <span className="inline-block w-8 text-right text-muted-foreground/50 mr-2 select-none">
                    {line.lineNumber ?? ''}
                  </span>
                  {line.type === 'added' && <span className="text-green-500 mr-1">+</span>}
                  {line.content}
                </div>
              ))
            ) : (
              <span className="text-muted-foreground italic">Empty</span>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
