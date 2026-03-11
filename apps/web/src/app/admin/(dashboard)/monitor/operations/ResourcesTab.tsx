'use client';

/**
 * ResourcesTab — Database, Cache, and Vector store metrics
 * Issue #127 — Resources Dashboard Tab (stub for #126 page setup)
 */

import { useCallback, useEffect, useState } from 'react';

import { Database, HardDrive, Layers, Loader2, Trash2, Wrench } from 'lucide-react';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type {
  CacheMetrics,
  DatabaseMetrics,
  TableSize,
  VectorStoreMetrics,
} from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

interface MetricCardProps {
  title: string;
  icon: React.ReactNode;
  children: React.ReactNode;
  loading?: boolean;
}

function MetricCard({ title, icon, children, loading }: MetricCardProps) {
  return (
    <div
      className={cn(
        'rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md p-4',
        'shadow-sm'
      )}
    >
      <div className="flex items-center gap-2 mb-3">
        {icon}
        <h3 className="font-quicksand font-semibold text-sm">{title}</h3>
      </div>
      {loading ? (
        <div className="flex items-center justify-center py-6">
          <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
        </div>
      ) : (
        children
      )}
    </div>
  );
}

function StatRow({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="flex items-center justify-between py-1">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm font-medium">{value}</span>
    </div>
  );
}

export function ResourcesTab() {
  const [db, setDb] = useState<DatabaseMetrics | null>(null);
  const [cache, setCache] = useState<CacheMetrics | null>(null);
  const [vectors, setVectors] = useState<VectorStoreMetrics | null>(null);
  const [tables, setTables] = useState<TableSize[]>([]);
  const [loading, setLoading] = useState(true);

  const fetchAll = useCallback(async () => {
    try {
      const [dbRes, cacheRes, vecRes, tablesRes] = await Promise.all([
        api.admin.getResourceDatabaseMetrics(),
        api.admin.getResourceCacheMetrics(),
        api.admin.getResourceVectorMetrics(),
        api.admin.getResourceDatabaseTopTables(10),
      ]);
      setDb(dbRes);
      setCache(cacheRes);
      setVectors(vecRes);
      setTables(tablesRes);
    } catch {
      // silently fail — individual cards show empty state
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAll();
    const interval = setInterval(fetchAll, 60_000);
    return () => clearInterval(interval);
  }, [fetchAll]);

  const handleClearCache = async () => {
    await api.admin.clearCache();
    fetchAll();
  };

  const handleVacuum = async (full: boolean) => {
    await api.admin.vacuumDatabase(full);
    fetchAll();
  };

  return (
    <div className="space-y-6" data-testid="resources-tab">
      <div>
        <h2 className="font-quicksand text-lg font-semibold">Resources</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Database, cache, and vector store health metrics.
        </p>
      </div>

      {/* Metrics Cards */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <MetricCard
          title="Database"
          icon={<Database className="h-4 w-4 text-blue-600" />}
          loading={loading}
        >
          {db && (
            <div className="space-y-0.5">
              <StatRow label="Size" value={db.sizeFormatted} />
              <StatRow label="Connections" value={`${db.activeConnections}/${db.maxConnections}`} />
              <StatRow label="Txn Committed" value={db.transactionsCommitted.toLocaleString()} />
              <StatRow label="Txn Rolled Back" value={db.transactionsRolledBack.toLocaleString()} />
              <StatRow
                label="Growth (7d)"
                value={`${db.growthLast7Days > 0 ? '+' : ''}${db.growthLast7Days}%`}
              />
            </div>
          )}
        </MetricCard>

        <MetricCard
          title="Cache (Redis)"
          icon={<HardDrive className="h-4 w-4 text-red-600" />}
          loading={loading}
        >
          {cache && (
            <div className="space-y-0.5">
              <StatRow
                label="Memory"
                value={`${cache.usedMemoryFormatted} / ${cache.maxMemoryFormatted}`}
              />
              <StatRow label="Usage" value={`${cache.memoryUsagePercent.toFixed(1)}%`} />
              <StatRow label="Keys" value={cache.totalKeys.toLocaleString()} />
              <StatRow label="Hit Rate" value={`${(cache.hitRate * 100).toFixed(1)}%`} />
              <StatRow label="Evicted" value={cache.evictedKeys.toLocaleString()} />
            </div>
          )}
        </MetricCard>

        <MetricCard
          title="Vector Store (Qdrant)"
          icon={<Layers className="h-4 w-4 text-purple-600" />}
          loading={loading}
        >
          {vectors && (
            <div className="space-y-0.5">
              <StatRow label="Collections" value={vectors.totalCollections} />
              <StatRow label="Vectors" value={vectors.totalVectors.toLocaleString()} />
              <StatRow label="Indexed" value={vectors.indexedVectors.toLocaleString()} />
              <StatRow label="Memory" value={vectors.memoryFormatted} />
            </div>
          )}
        </MetricCard>
      </div>

      {/* Top Tables */}
      {tables.length > 0 && (
        <div className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md p-4">
          <h3 className="font-quicksand font-semibold text-sm mb-3">Top Tables by Size</h3>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-xs text-muted-foreground">
                  <th className="text-left py-2 pr-4">Table</th>
                  <th className="text-right py-2 px-4">Rows</th>
                  <th className="text-right py-2 px-4">Size</th>
                  <th className="text-right py-2 pl-4">Index</th>
                </tr>
              </thead>
              <tbody>
                {tables.map(t => (
                  <tr key={t.tableName} className="border-b last:border-0">
                    <td className="py-2 pr-4 font-mono text-xs">{t.tableName}</td>
                    <td className="py-2 px-4 text-right">{t.rowCount.toLocaleString()}</td>
                    <td className="py-2 px-4 text-right">{t.sizeFormatted}</td>
                    <td className="py-2 pl-4 text-right">{t.indexSizeFormatted}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="flex flex-wrap gap-3">
        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button variant="outline" size="sm" data-testid="clear-cache-button">
              <Trash2 className="h-4 w-4 mr-2" />
              Clear Cache
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Clear Cache</AlertDialogTitle>
              <AlertDialogDescription>
                This will clear all cached data from Redis. Active sessions may experience slower
                responses until the cache is rebuilt.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction onClick={handleClearCache}>Clear Cache</AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button variant="outline" size="sm" data-testid="vacuum-db-button">
              <Wrench className="h-4 w-4 mr-2" />
              Vacuum Database
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Vacuum Database</AlertDialogTitle>
              <AlertDialogDescription>
                VACUUM will reclaim storage and update statistics. A full vacuum locks the database
                briefly but reclaims more space.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction onClick={() => handleVacuum(false)}>
                Standard Vacuum
              </AlertDialogAction>
              <AlertDialogAction
                onClick={() => handleVacuum(true)}
                className="bg-amber-600 hover:bg-amber-700"
              >
                Full Vacuum
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </div>
  );
}
