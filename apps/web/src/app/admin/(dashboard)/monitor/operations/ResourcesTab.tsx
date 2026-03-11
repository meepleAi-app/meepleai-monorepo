'use client';

/**
 * ResourcesTab — Database, Cache, and Vector store metrics
 * Issue #127 — Enhanced Resources Dashboard Tab
 *
 * Enhancements over stub (#126):
 * - Sortable DataTable for top tables
 * - Level 2 confirmation for destructive ops (vacuum, rebuild)
 * - Toast feedback on actions
 * - Trend indicators on KPI cards
 * - Loading reset on refetch
 */

import { useCallback, useEffect, useState } from 'react';

import { type ColumnDef } from '@tanstack/react-table';
import { Database, HardDrive, Layers, Loader2, TrendingDown, TrendingUp } from 'lucide-react';

import {
  AdminConfirmationDialog,
  AdminConfirmationLevel,
} from '@/components/ui/admin/admin-confirmation-dialog';
import { DataTable, SortableHeader } from '@/components/ui/data-display/data-table';
import { Button } from '@/components/ui/primitives/button';
import { useToast } from '@/hooks/use-toast';
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

function StatRow({
  label,
  value,
  trend,
}: {
  label: string;
  value: string | number;
  trend?: 'up' | 'down' | null;
}) {
  return (
    <div className="flex items-center justify-between py-1">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="flex items-center gap-1 text-sm font-medium">
        {value}
        {trend === 'up' && <TrendingUp className="h-3 w-3 text-green-600" />}
        {trend === 'down' && <TrendingDown className="h-3 w-3 text-red-600" />}
      </span>
    </div>
  );
}

const tableColumns: ColumnDef<TableSize>[] = [
  {
    accessorKey: 'tableName',
    header: ({ column }) => <SortableHeader column={column}>Table</SortableHeader>,
    cell: ({ row }) => <span className="font-mono text-xs">{row.getValue('tableName')}</span>,
  },
  {
    accessorKey: 'rowCount',
    header: ({ column }) => <SortableHeader column={column}>Rows</SortableHeader>,
    cell: ({ row }) => (
      <span className="text-right block">
        {(row.getValue('rowCount') as number).toLocaleString()}
      </span>
    ),
  },
  {
    accessorKey: 'totalSizeBytes',
    header: ({ column }) => <SortableHeader column={column}>Size</SortableHeader>,
    cell: ({ row }) => <span className="text-right block">{row.original.sizeFormatted}</span>,
  },
  {
    accessorKey: 'indexSizeBytes',
    header: ({ column }) => <SortableHeader column={column}>Index</SortableHeader>,
    cell: ({ row }) => <span className="text-right block">{row.original.indexSizeFormatted}</span>,
  },
];

export function ResourcesTab() {
  const [db, setDb] = useState<DatabaseMetrics | null>(null);
  const [cache, setCache] = useState<CacheMetrics | null>(null);
  const [vectors, setVectors] = useState<VectorStoreMetrics | null>(null);
  const [tables, setTables] = useState<TableSize[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);

  // Confirmation dialogs
  const [vacuumOpen, setVacuumOpen] = useState(false);
  const [vacuumFull, setVacuumFull] = useState(false);
  const [rebuildOpen, setRebuildOpen] = useState(false);
  const [clearCacheOpen, setClearCacheOpen] = useState(false);

  const { toast } = useToast();

  const fetchAll = useCallback(async () => {
    setLoading(true);
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
      toast({ title: 'Failed to load resource metrics', variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    fetchAll();
    const interval = setInterval(fetchAll, 60_000);
    return () => clearInterval(interval);
  }, [fetchAll]);

  const handleClearCache = async () => {
    setActionLoading(true);
    try {
      await api.admin.clearCache();
      toast({ title: 'Cache cleared successfully' });
      fetchAll();
    } catch {
      toast({ title: 'Failed to clear cache', variant: 'destructive' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleVacuum = async () => {
    setActionLoading(true);
    try {
      await api.admin.vacuumDatabase(vacuumFull);
      toast({ title: `${vacuumFull ? 'Full' : 'Standard'} vacuum completed` });
      fetchAll();
    } catch {
      toast({ title: 'Vacuum failed', variant: 'destructive' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleRebuildVectors = async () => {
    setActionLoading(true);
    try {
      await api.admin.rebuildVectors();
      toast({ title: 'Vector rebuild started' });
      fetchAll();
    } catch {
      toast({ title: 'Vector rebuild failed', variant: 'destructive' });
    } finally {
      setActionLoading(false);
    }
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
                trend={db.growthLast7Days > 0 ? 'up' : db.growthLast7Days < 0 ? 'down' : null}
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
              <StatRow
                label="Usage"
                value={`${cache.memoryUsagePercent.toFixed(1)}%`}
                trend={cache.memoryUsagePercent > 80 ? 'up' : null}
              />
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

      {/* Top Tables (Sortable DataTable) */}
      {tables.length > 0 && (
        <div className="rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md p-4">
          <h3 className="font-quicksand font-semibold text-sm mb-3">Top Tables by Size</h3>
          <DataTable
            columns={tableColumns}
            data={tables}
            isLoading={loading}
            emptyMessage="No table data available."
          />
        </div>
      )}

      {/* Actions with Level 2 Confirmation */}
      <div className="flex flex-wrap gap-3">
        <Button
          variant="outline"
          size="sm"
          onClick={() => setClearCacheOpen(true)}
          disabled={actionLoading}
          data-testid="clear-cache-button"
        >
          Clear Cache
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            setVacuumFull(false);
            setVacuumOpen(true);
          }}
          disabled={actionLoading}
          data-testid="vacuum-db-button"
        >
          Standard Vacuum
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={() => {
            setVacuumFull(true);
            setVacuumOpen(true);
          }}
          disabled={actionLoading}
          data-testid="full-vacuum-db-button"
        >
          Full Vacuum
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={() => setRebuildOpen(true)}
          disabled={actionLoading}
          data-testid="rebuild-vectors-button"
        >
          Rebuild Vectors
        </Button>
      </div>

      {/* Clear Cache — Level 1 */}
      <AdminConfirmationDialog
        isOpen={clearCacheOpen}
        onClose={() => setClearCacheOpen(false)}
        onConfirm={handleClearCache}
        level={AdminConfirmationLevel.Level1}
        title="Clear Cache"
        message="This will clear all cached data from Redis. Active sessions may experience slower responses until the cache is rebuilt."
        isLoading={actionLoading}
      />

      {/* Vacuum — Level 2 */}
      <AdminConfirmationDialog
        isOpen={vacuumOpen}
        onClose={() => setVacuumOpen(false)}
        onConfirm={handleVacuum}
        level={AdminConfirmationLevel.Level2}
        title={vacuumFull ? 'Full Vacuum Database' : 'Standard Vacuum Database'}
        message={
          vacuumFull
            ? 'A full VACUUM locks the database and reclaims maximum space. This may cause brief downtime.'
            : 'Standard VACUUM reclaims storage and updates statistics without locking.'
        }
        warningMessage={vacuumFull ? 'This will briefly lock the database.' : undefined}
        isLoading={actionLoading}
      />

      {/* Rebuild Vectors — Level 2 */}
      <AdminConfirmationDialog
        isOpen={rebuildOpen}
        onClose={() => setRebuildOpen(false)}
        onConfirm={handleRebuildVectors}
        level={AdminConfirmationLevel.Level2}
        title="Rebuild Vector Indices"
        message="This will rebuild all vector store indices. Search may be degraded during the rebuild process."
        warningMessage="Existing indices will be temporarily unavailable."
        isLoading={actionLoading}
      />
    </div>
  );
}
