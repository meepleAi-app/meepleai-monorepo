'use client';

import { useRef, useState } from 'react';

import { AlertCircle, CheckCircle2, Loader2, Upload } from 'lucide-react';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';

import { useExcelImport, type ExcelImportResult } from '../lib/catalog-ingestion-api';

export function ExcelImportTab() {
  const fileRef = useRef<HTMLInputElement>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const importMutation = useExcelImport();

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0] ?? null;
    setSelectedFile(file);
    importMutation.reset();
  };

  const handleUpload = () => {
    if (!selectedFile) return;
    importMutation.mutate(selectedFile);
  };

  const result = importMutation.data as ExcelImportResult | undefined;

  return (
    <div className="space-y-6">
      {/* Upload section */}
      <div className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl rounded-2xl border border-slate-200/60 dark:border-zinc-700/60 p-6 space-y-4">
        <h3 className="font-quicksand text-lg font-semibold text-foreground">Upload Excel File</h3>
        <p className="text-sm text-muted-foreground">
          Import skeleton games from an Excel spreadsheet (.xlsx). The file should contain columns
          for game name, year, and optionally player count, play time, and BGG ID.
        </p>

        <div className="flex items-center gap-4">
          <input
            ref={fileRef}
            type="file"
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            onChange={handleFileChange}
            className="text-sm text-muted-foreground file:mr-4 file:py-2 file:px-4 file:rounded-lg file:border-0 file:text-sm file:font-semibold file:bg-primary/10 file:text-primary hover:file:bg-primary/20 file:cursor-pointer"
          />
          <Button
            onClick={handleUpload}
            disabled={!selectedFile || importMutation.isPending}
            size="sm"
          >
            {importMutation.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Upload className="h-4 w-4" />
            )}
            {importMutation.isPending ? 'Importing...' : 'Import'}
          </Button>
        </div>
      </div>

      {/* Error alert */}
      {importMutation.isError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {importMutation.error instanceof Error
              ? importMutation.error.message
              : 'Import failed. Please try again.'}
          </AlertDescription>
        </Alert>
      )}

      {/* Results */}
      {result && (
        <div className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl rounded-2xl border border-slate-200/60 dark:border-zinc-700/60 p-6 space-y-4">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-5 w-5 text-emerald-500" />
            <h3 className="font-quicksand text-lg font-semibold text-foreground">Import Results</h3>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <ResultStat label="Total Rows" value={result.total} />
            <ResultStat
              label="Created"
              value={result.created}
              color="text-emerald-600 dark:text-emerald-400"
            />
            <ResultStat
              label="Duplicates"
              value={result.duplicates}
              color="text-amber-600 dark:text-amber-400"
            />
            <ResultStat
              label="Errors"
              value={result.errors}
              color="text-red-600 dark:text-red-400"
            />
          </div>

          {/* Row errors table */}
          {result.rowErrors.length > 0 && (
            <div className="space-y-2">
              <h4 className="text-sm font-semibold text-foreground">Row Errors</h4>
              <div className="overflow-auto max-h-64 rounded-lg border border-slate-200/60 dark:border-zinc-700/40">
                <table className="w-full text-sm">
                  <thead className="bg-slate-50 dark:bg-zinc-800 sticky top-0">
                    <tr>
                      <th className="text-left px-3 py-2 font-medium text-muted-foreground">Row</th>
                      <th className="text-left px-3 py-2 font-medium text-muted-foreground">
                        Column
                      </th>
                      <th className="text-left px-3 py-2 font-medium text-muted-foreground">
                        Error
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-zinc-700/40">
                    {result.rowErrors.map((err, i) => (
                      <tr key={i}>
                        <td className="px-3 py-2 tabular-nums">{err.rowNumber}</td>
                        <td className="px-3 py-2">{err.columnName ?? '-'}</td>
                        <td className="px-3 py-2 text-red-600 dark:text-red-400">
                          {err.errorMessage}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function ResultStat({ label, value, color }: { label: string; value: number; color?: string }) {
  return (
    <div className="text-center p-3 rounded-lg bg-slate-50/80 dark:bg-zinc-700/40">
      <div className={`text-2xl font-bold tabular-nums ${color ?? 'text-foreground'}`}>{value}</div>
      <div className="text-xs text-muted-foreground mt-1">{label}</div>
    </div>
  );
}
