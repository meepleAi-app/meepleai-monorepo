'use client';

import { useState, useMemo } from 'react';

import { ArrowRightLeft, Database, Loader2, ChevronRight } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';

import { ConfirmationDialog } from './ConfirmationDialog';
import { useTableList, useTableCompare, useSyncTable } from '../hooks/useTableCompare';
import { useTunnelStatus } from '../hooks/useTunnelStatus';

import type { TableInfo } from '../types/db-sync';

function groupByContext(tables: TableInfo[]): Record<string, TableInfo[]> {
  const groups: Record<string, TableInfo[]> = {};
  for (const t of tables) {
    const ctx = t.boundedContext ?? 'Other';
    if (!groups[ctx]) groups[ctx] = [];
    groups[ctx].push(t);
  }
  return Object.fromEntries(Object.entries(groups).sort(([a], [b]) => a.localeCompare(b)));
}

export function DataComparisonTab() {
  const { data: tunnelStatus } = useTunnelStatus();
  const isConnected = tunnelStatus?.status === 'Open';

  const { data: tables, isLoading: tablesLoading } = useTableList(isConnected);
  const [selectedTable, setSelectedTable] = useState<string | null>(null);
  const { data: diff, isLoading: diffLoading, error: diffError } = useTableCompare(selectedTable);
  const syncTable = useSyncTable();
  const [showConfirm, setShowConfirm] = useState(false);

  const grouped = useMemo(() => (tables ? groupByContext(tables) : {}), [tables]);

  if (!isConnected) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <Database className="mb-4 h-10 w-10 text-muted-foreground/50" aria-hidden="true" />
        <p className="text-sm text-muted-foreground">
          Connect the SSH tunnel to compare table data.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Data Comparison
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Compare row data between local and staging databases.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[280px_1fr]">
        {/* Sidebar: table list */}
        <div className="rounded-lg border border-slate-200/60 dark:border-zinc-700/40">
          <div className="border-b border-slate-200/60 bg-slate-50/50 px-4 py-2.5 dark:border-zinc-700/40 dark:bg-zinc-800/50">
            <h3 className="text-sm font-medium text-muted-foreground">
              Tables
              {tables && <span className="ml-1.5 text-xs">({tables.length})</span>}
            </h3>
          </div>
          <ScrollArea className="h-[500px]">
            {tablesLoading && (
              <div className="flex items-center justify-center py-8">
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              </div>
            )}
            {tables && tables.length === 0 && (
              <p className="px-4 py-8 text-center text-sm text-muted-foreground">
                No tables found.
              </p>
            )}
            {Object.entries(grouped).map(([ctx, ctxTables]) => (
              <div key={ctx}>
                <div className="px-4 py-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground/70">
                  {ctx}
                </div>
                {ctxTables.map(t => {
                  const isSelected = selectedTable === t.tableName;
                  const hasDiff = t.localRowCount !== t.stagingRowCount;
                  return (
                    <button
                      key={t.tableName}
                      onClick={() => setSelectedTable(t.tableName)}
                      className={`flex w-full items-center justify-between px-4 py-2 text-left text-sm transition-colors hover:bg-muted/50 ${
                        isSelected
                          ? 'bg-muted/70 font-medium text-foreground'
                          : 'text-foreground/80'
                      }`}
                    >
                      <span className="truncate">{t.tableName}</span>
                      <span className="flex items-center gap-1.5">
                        {hasDiff && (
                          <span className="inline-block h-1.5 w-1.5 rounded-full bg-amber-500" />
                        )}
                        <span className="text-xs text-muted-foreground">
                          {t.localRowCount}/{t.stagingRowCount}
                        </span>
                        <ChevronRight className="h-3 w-3 text-muted-foreground" />
                      </span>
                    </button>
                  );
                })}
              </div>
            ))}
          </ScrollArea>
        </div>

        {/* Right panel: diff details */}
        <div className="rounded-lg border border-slate-200/60 dark:border-zinc-700/40">
          {!selectedTable && (
            <div className="flex flex-col items-center justify-center py-16 text-center">
              <ArrowRightLeft
                className="mb-4 h-8 w-8 text-muted-foreground/40"
                aria-hidden="true"
              />
              <p className="text-sm text-muted-foreground">Select a table to view differences.</p>
            </div>
          )}

          {selectedTable && diffLoading && (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          )}

          {diffError && (
            <div className="m-4 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-800/40 dark:bg-red-900/20 dark:text-red-400">
              Failed to compare: {(diffError as Error).message}
            </div>
          )}

          {diff && (
            <div className="space-y-4 p-4">
              <div className="flex items-center justify-between">
                <h3 className="font-quicksand text-base font-semibold text-foreground">
                  {diff.tableName}
                </h3>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => setShowConfirm(true)}
                  disabled={
                    syncTable.isPending ||
                    (diff.modified.length === 0 &&
                      diff.localOnly.length === 0 &&
                      diff.stagingOnly.length === 0)
                  }
                >
                  Sync Table
                </Button>
              </div>

              {/* Summary stats */}
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                <div className="rounded-lg bg-muted/30 p-3 text-center">
                  <div className="text-lg font-semibold text-foreground">{diff.localRowCount}</div>
                  <div className="text-xs text-muted-foreground">Local Rows</div>
                </div>
                <div className="rounded-lg bg-muted/30 p-3 text-center">
                  <div className="text-lg font-semibold text-foreground">
                    {diff.stagingRowCount}
                  </div>
                  <div className="text-xs text-muted-foreground">Staging Rows</div>
                </div>
                <div className="rounded-lg bg-muted/30 p-3 text-center">
                  <div className="text-lg font-semibold text-foreground">{diff.identicalCount}</div>
                  <div className="text-xs text-muted-foreground">Identical</div>
                </div>
                <div className="rounded-lg bg-muted/30 p-3 text-center">
                  <div className="text-lg font-semibold text-foreground">
                    {diff.modified.length}
                  </div>
                  <div className="text-xs text-muted-foreground">Modified</div>
                </div>
              </div>

              {/* Modified rows */}
              {diff.modified.length > 0 && (
                <div className="space-y-2">
                  <h4 className="text-sm font-medium text-foreground">
                    Modified Rows ({diff.modified.length})
                  </h4>
                  <div className="overflow-x-auto rounded-lg border border-slate-200/60 dark:border-zinc-700/40">
                    <table className="w-full text-sm">
                      <thead>
                        <tr className="border-b border-slate-200/60 dark:border-zinc-700/40 bg-slate-50/50 dark:bg-zinc-800/50">
                          <th className="px-4 py-2 text-left font-medium text-muted-foreground">
                            Primary Key
                          </th>
                          <th className="px-4 py-2 text-left font-medium text-muted-foreground">
                            Column
                          </th>
                          <th className="px-4 py-2 text-left font-medium text-muted-foreground">
                            Local
                          </th>
                          <th className="px-4 py-2 text-left font-medium text-muted-foreground">
                            Staging
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {diff.modified.slice(0, 100).map((row, ri) =>
                          row.differences.map((d, di) => (
                            <tr
                              key={`${ri}-${di}`}
                              className="border-b border-slate-200/40 dark:border-zinc-700/30 last:border-b-0"
                            >
                              {di === 0 && (
                                <td
                                  className="px-4 py-2 font-mono text-xs text-foreground"
                                  rowSpan={row.differences.length}
                                >
                                  {Object.entries(row.primaryKey)
                                    .map(([k, v]) => `${k}=${v}`)
                                    .join(', ')}
                                </td>
                              )}
                              <td className="px-4 py-2 text-foreground">{d.column}</td>
                              <td className="px-4 py-2 font-mono text-xs text-blue-600 dark:text-blue-400">
                                {d.localValue ?? 'NULL'}
                              </td>
                              <td className="px-4 py-2 font-mono text-xs text-amber-600 dark:text-amber-400">
                                {d.stagingValue ?? 'NULL'}
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>
                  {diff.modified.length > 100 && (
                    <p className="text-xs text-muted-foreground">
                      Showing first 100 of {diff.modified.length} modified rows.
                    </p>
                  )}
                </div>
              )}

              {/* Local only */}
              {diff.localOnly.length > 0 && (
                <div className="space-y-2">
                  <h4 className="text-sm font-medium text-foreground">
                    Local Only ({diff.localOnly.length})
                  </h4>
                  <p className="text-xs text-muted-foreground">
                    Rows present in local database but missing from staging.
                  </p>
                </div>
              )}

              {/* Staging only */}
              {diff.stagingOnly.length > 0 && (
                <div className="space-y-2">
                  <h4 className="text-sm font-medium text-foreground">
                    Staging Only ({diff.stagingOnly.length})
                  </h4>
                  <p className="text-xs text-muted-foreground">
                    Rows present in staging database but missing from local.
                  </p>
                </div>
              )}

              {diff.modified.length === 0 &&
                diff.localOnly.length === 0 &&
                diff.stagingOnly.length === 0 && (
                  <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700 dark:border-emerald-800/40 dark:bg-emerald-900/20 dark:text-emerald-400">
                    Table data is identical across both databases.
                  </div>
                )}

              {syncTable.isSuccess && syncTable.data && (
                <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700 dark:border-emerald-800/40 dark:bg-emerald-900/20 dark:text-emerald-400">
                  Sync completed. Inserted: {syncTable.data.inserted}, Updated:{' '}
                  {syncTable.data.updated}
                </div>
              )}

              {syncTable.isError && (
                <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-800/40 dark:bg-red-900/20 dark:text-red-400">
                  Sync failed: {(syncTable.error as Error).message}
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      <ConfirmationDialog
        title={`Sync Table: ${selectedTable}`}
        description={`This will synchronize the "${selectedTable}" table data. This action may overwrite existing rows.`}
        expectedText="SYNC DATA"
        onConfirm={() => {
          if (!selectedTable) return;
          syncTable.mutate(
            {
              tableName: selectedTable,
              direction: 'StagingToLocal',
              confirmation: 'SYNC DATA',
            },
            { onSettled: () => setShowConfirm(false) }
          );
        }}
        isLoading={syncTable.isPending}
        open={showConfirm}
        onOpenChange={setShowConfirm}
      />
    </div>
  );
}
