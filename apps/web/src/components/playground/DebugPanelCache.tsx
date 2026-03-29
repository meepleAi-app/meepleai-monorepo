'use client';

import { Database } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui';
import type { CacheInfo } from '@/lib/agent/playground-sse-parser';
import { cn } from '@/lib/utils';

interface DebugPanelCacheProps {
  cacheInfo: CacheInfo | null;
  sessionCacheHits: number;
  sessionCacheRequests: number;
}

export function DebugPanelCache({
  cacheInfo,
  sessionCacheHits,
  sessionCacheRequests,
}: DebugPanelCacheProps) {
  if (!cacheInfo || cacheInfo.status === 'skip') return null;

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm flex items-center gap-2">
          <Database className="h-4 w-4" />
          Cache
          <span
            className={cn(
              'text-[10px] px-1.5 py-0.5 rounded leading-none font-semibold',
              cacheInfo.status === 'hit' && 'bg-green-100 text-green-800',
              cacheInfo.status === 'miss' && 'bg-red-100 text-red-800'
            )}
          >
            {cacheInfo.status.toUpperCase()}
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-2 text-sm">
          {cacheInfo.tier && (
            <div className="flex justify-between">
              <span className="text-muted-foreground">Tier</span>
              <span className="font-mono">{cacheInfo.tier}</span>
            </div>
          )}
          {cacheInfo.cacheKey && (
            <div className="flex justify-between">
              <span className="text-muted-foreground">Key</span>
              <span className="font-mono text-xs truncate max-w-[140px]" title={cacheInfo.cacheKey}>
                {cacheInfo.cacheKey}
              </span>
            </div>
          )}
          {cacheInfo.ttlSeconds > 0 && (
            <div className="flex justify-between">
              <span className="text-muted-foreground">TTL</span>
              <span className="font-mono">{cacheInfo.ttlSeconds}s</span>
            </div>
          )}
          {cacheInfo.latencyMs > 0 && (
            <div className="flex justify-between">
              <span className="text-muted-foreground">Lookup</span>
              <span className="font-mono">{cacheInfo.latencyMs.toFixed(2)}ms</span>
            </div>
          )}
          {sessionCacheRequests > 0 && (
            <div className="border-t pt-2 flex justify-between text-xs text-muted-foreground">
              <span>Session hit rate</span>
              <span className="font-mono">
                {sessionCacheHits}/{sessionCacheRequests} (
                {((sessionCacheHits / sessionCacheRequests) * 100).toFixed(0)}%)
              </span>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
