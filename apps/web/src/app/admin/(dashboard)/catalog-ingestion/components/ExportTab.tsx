'use client';

import { useState } from 'react';

import { AlertCircle, Download, Loader2 } from 'lucide-react';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Label } from '@/components/ui/primitives/label';

import { useExcelExport } from '../lib/catalog-ingestion-api';

const GAME_DATA_STATUSES = ['Skeleton', 'Enriched', 'Complete'] as const;

export function ExportTab() {
  const [selectedStatus, setSelectedStatus] = useState<string | undefined>(undefined);
  const [hasPdf, setHasPdf] = useState(false);
  const [hasPdfEnabled, setHasPdfEnabled] = useState(false);
  const exportMutation = useExcelExport();

  const handleExport = () => {
    exportMutation.mutate({
      status: selectedStatus,
      hasPdf: hasPdfEnabled ? hasPdf : undefined,
    });
  };

  return (
    <div className="space-y-6">
      <div className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl rounded-2xl border border-slate-200/60 dark:border-zinc-700/60 p-6 space-y-6">
        <div>
          <h3 className="font-quicksand text-lg font-semibold text-foreground">
            Export Catalog to Excel
          </h3>
          <p className="text-sm text-muted-foreground mt-1">
            Download the shared game catalog as an Excel file. Optionally filter by status or PDF
            availability.
          </p>
        </div>

        {/* Status filter */}
        <div className="space-y-3">
          <Label className="text-sm font-medium">Filter by Status</Label>
          <div className="flex flex-wrap gap-4">
            <div className="flex items-center gap-2">
              <Checkbox
                id="status-all"
                checked={selectedStatus === undefined}
                onCheckedChange={() => setSelectedStatus(undefined)}
              />
              <Label htmlFor="status-all" className="text-sm font-normal cursor-pointer">
                All
              </Label>
            </div>
            {GAME_DATA_STATUSES.map(status => (
              <div key={status} className="flex items-center gap-2">
                <Checkbox
                  id={`status-${status}`}
                  checked={selectedStatus === status}
                  onCheckedChange={checked => {
                    setSelectedStatus(checked ? status : undefined);
                  }}
                />
                <Label htmlFor={`status-${status}`} className="text-sm font-normal cursor-pointer">
                  {status}
                </Label>
              </div>
            ))}
          </div>
        </div>

        {/* HasPdf filter */}
        <div className="space-y-3">
          <div className="flex items-center gap-2">
            <Checkbox
              id="filter-pdf"
              checked={hasPdfEnabled}
              onCheckedChange={checked => setHasPdfEnabled(checked === true)}
            />
            <Label htmlFor="filter-pdf" className="text-sm font-normal cursor-pointer">
              Filter by PDF availability
            </Label>
          </div>
          {hasPdfEnabled && (
            <div className="ml-6 flex items-center gap-2">
              <Checkbox
                id="has-pdf"
                checked={hasPdf}
                onCheckedChange={checked => setHasPdf(checked === true)}
              />
              <Label htmlFor="has-pdf" className="text-sm font-normal cursor-pointer">
                Has PDF
              </Label>
            </div>
          )}
        </div>

        {/* Download button */}
        <Button onClick={handleExport} disabled={exportMutation.isPending} size="sm">
          {exportMutation.isPending ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Download className="h-4 w-4" />
          )}
          Download .xlsx
        </Button>
      </div>

      {/* Error */}
      {exportMutation.isError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {exportMutation.error instanceof Error
              ? exportMutation.error.message
              : 'Export failed. Please try again.'}
          </AlertDescription>
        </Alert>
      )}

      {/* Success */}
      {exportMutation.isSuccess && (
        <div className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl rounded-2xl border border-emerald-200/60 dark:border-emerald-700/40 p-4">
          <p className="text-sm text-emerald-700 dark:text-emerald-400">
            Export downloaded successfully.
          </p>
        </div>
      )}
    </div>
  );
}
