'use client';

import { useState } from 'react';

import { ArrowRightLeft, Play, Eye, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

import { ConfirmationDialog } from './ConfirmationDialog';
import { SqlPreviewModal } from './SqlPreviewModal';
import { useSchemaCompare, usePreviewSql, useApplyMigrations } from '../hooks/useSchemaCompare';
import { useTunnelStatus } from '../hooks/useTunnelStatus';

import type { MigrationInfo } from '../types/db-sync';

function MigrationTable({
  title,
  migrations,
  variant,
}: {
  title: string;
  migrations: MigrationInfo[];
  variant: 'common' | 'local' | 'staging';
}) {
  const colorMap = {
    common: 'bg-slate-50/50 dark:bg-zinc-800/30',
    local: 'bg-blue-50/50 dark:bg-blue-900/10',
    staging: 'bg-amber-50/50 dark:bg-amber-900/10',
  };

  return (
    <div className="space-y-2">
      <h3 className="text-sm font-medium text-foreground">
        {title} <span className="text-muted-foreground">({migrations.length})</span>
      </h3>
      {migrations.length === 0 ? (
        <p className="text-sm text-muted-foreground">None</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-slate-200/60 dark:border-zinc-700/40">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200/60 dark:border-zinc-700/40 bg-slate-50/50 dark:bg-zinc-800/50">
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">
                  Migration ID
                </th>
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">Version</th>
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">
                  Applied On
                </th>
              </tr>
            </thead>
            <tbody>
              {migrations.map(m => (
                <tr
                  key={m.migrationId}
                  className={`border-b border-slate-200/40 dark:border-zinc-700/30 last:border-b-0 ${colorMap[variant]}`}
                >
                  <td className="px-4 py-2 font-mono text-xs text-foreground">{m.migrationId}</td>
                  <td className="px-4 py-2 text-muted-foreground">{m.productVersion}</td>
                  <td className="px-4 py-2 text-muted-foreground">
                    {m.appliedOn ? new Date(m.appliedOn).toLocaleString() : 'Pending'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export function SchemaComparisonTab() {
  const { data: tunnelStatus } = useTunnelStatus();
  const isConnected = tunnelStatus?.status === 'Open';

  const { data: schema, isLoading, error, refetch } = useSchemaCompare(isConnected);
  const previewSql = usePreviewSql();
  const applyMigrations = useApplyMigrations();

  const [showSqlPreview, setShowSqlPreview] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);

  const hasDifferences = schema && (schema.localOnly.length > 0 || schema.stagingOnly.length > 0);

  if (!isConnected) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <ArrowRightLeft className="mb-4 h-10 w-10 text-muted-foreground/50" aria-hidden="true" />
        <p className="text-sm text-muted-foreground">Connect the SSH tunnel to compare schemas.</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Schema Comparison
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Compare EF Core migrations between local and staging databases.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => refetch()} disabled={isLoading}>
            {isLoading && <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />}
            Refresh
          </Button>
          {hasDifferences && (
            <>
              <Button
                variant="outline"
                size="sm"
                onClick={() => previewSql.mutate()}
                disabled={previewSql.isPending}
              >
                <Eye className="mr-2 h-4 w-4" aria-hidden="true" />
                Preview SQL
              </Button>
              <Button
                size="sm"
                onClick={() => setShowConfirm(true)}
                disabled={applyMigrations.isPending}
              >
                <Play className="mr-2 h-4 w-4" aria-hidden="true" />
                Apply Migrations
              </Button>
            </>
          )}
        </div>
      </div>

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-800/40 dark:bg-red-900/20 dark:text-red-400">
          Failed to compare schemas: {(error as Error).message}
        </div>
      )}

      {isLoading && !schema && (
        <div className="space-y-4 animate-pulse">
          <div className="h-6 w-40 rounded bg-muted/50" />
          <div className="h-32 rounded-lg bg-muted/50" />
          <div className="h-6 w-40 rounded bg-muted/50" />
          <div className="h-32 rounded-lg bg-muted/50" />
        </div>
      )}

      {schema && (
        <div className="space-y-6">
          <MigrationTable title="Common Migrations" migrations={schema.common} variant="common" />
          <MigrationTable title="Local Only" migrations={schema.localOnly} variant="local" />
          <MigrationTable title="Staging Only" migrations={schema.stagingOnly} variant="staging" />

          {!hasDifferences && (
            <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700 dark:border-emerald-800/40 dark:bg-emerald-900/20 dark:text-emerald-400">
              Schemas are in sync. No differences found.
            </div>
          )}
        </div>
      )}

      {applyMigrations.isError && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-800/40 dark:bg-red-900/20 dark:text-red-400">
          Migration failed: {(applyMigrations.error as Error).message}
        </div>
      )}

      {applyMigrations.isSuccess && applyMigrations.data && (
        <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700 dark:border-emerald-800/40 dark:bg-emerald-900/20 dark:text-emerald-400">
          Migrations applied successfully. Inserted: {applyMigrations.data.inserted}, Updated:{' '}
          {applyMigrations.data.updated}
        </div>
      )}

      {previewSql.data !== null && previewSql.data !== undefined && (
        <SqlPreviewModal
          sql={String(previewSql.data)}
          open={showSqlPreview || (previewSql.data !== null && previewSql.data !== undefined)}
          onOpenChange={open => {
            setShowSqlPreview(open);
            if (!open) previewSql.reset();
          }}
        />
      )}

      <ConfirmationDialog
        title="Apply Migrations"
        description="This will apply pending migrations to the target database. This action may be destructive and cannot be easily undone."
        expectedText="APPLY MIGRATIONS"
        onConfirm={() => {
          applyMigrations.mutate(
            { direction: 'StagingToLocal', confirmation: 'APPLY MIGRATIONS' },
            { onSettled: () => setShowConfirm(false) }
          );
        }}
        isLoading={applyMigrations.isPending}
        open={showConfirm}
        onOpenChange={setShowConfirm}
      />
    </div>
  );
}
