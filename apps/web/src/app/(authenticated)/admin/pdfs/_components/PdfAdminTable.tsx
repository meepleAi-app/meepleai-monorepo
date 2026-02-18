'use client';

import { ArrowUpDown, ArrowUp, ArrowDown } from 'lucide-react';

import { PdfProgressBar } from '@/components/pdf/PdfProgressBar';
import { PdfStatusBadge } from '@/components/pdf/PdfStatusBadge';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import type { AdminPdfListItem } from '@/lib/api/clients/pdfClient';
import type { PdfState } from '@/types/pdf';
import { isPdfStateTerminal } from '@/types/pdf';

import { PdfRowActions } from './PdfRowActions';

interface PdfAdminTableProps {
  items: AdminPdfListItem[];
  selectedIds: Set<string>;
  onSelectionChange: (ids: Set<string>) => void;
  sortBy: string;
  sortOrder: 'asc' | 'desc';
  onSortChange: (field: string) => void;
  onDelete: (pdfId: string, fileName: string) => void;
  onReindex: (pdfId: string, fileName: string) => void;
  onRetry: (pdfId: string, fileName: string) => void;
}

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / Math.pow(k, i)).toFixed(1)} ${sizes[i]}`;
}

function mapToPdfState(state: string): PdfState {
  const lower = state.toLowerCase();
  const validStates: PdfState[] = ['pending', 'uploading', 'extracting', 'chunking', 'embedding', 'indexing', 'ready', 'failed'];
  return validStates.find(s => s === lower) || 'pending';
}

function SortHeader({ label, field, currentSort, currentOrder, onSort }: {
  label: string;
  field: string;
  currentSort: string;
  currentOrder: 'asc' | 'desc';
  onSort: (field: string) => void;
}) {
  const isActive = currentSort === field;
  return (
    <button
      onClick={() => onSort(field)}
      className="flex items-center gap-1 hover:text-foreground transition-colors"
    >
      {label}
      {isActive ? (
        currentOrder === 'asc' ? <ArrowUp className="h-3 w-3" /> : <ArrowDown className="h-3 w-3" />
      ) : (
        <ArrowUpDown className="h-3 w-3 opacity-50" />
      )}
    </button>
  );
}

export function PdfAdminTable({
  items,
  selectedIds,
  onSelectionChange,
  sortBy,
  sortOrder,
  onSortChange,
  onDelete,
  onReindex,
  onRetry,
}: PdfAdminTableProps) {
  const allSelected = items.length > 0 && items.every(item => selectedIds.has(item.id));
  const someSelected = items.some(item => selectedIds.has(item.id)) && !allSelected;

  const handleSelectAll = () => {
    if (allSelected) {
      onSelectionChange(new Set());
    } else {
      onSelectionChange(new Set(items.map(item => item.id)));
    }
  };

  const handleSelectOne = (id: string) => {
    const next = new Set(selectedIds);
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    onSelectionChange(next);
  };

  return (
    <div className="bg-card rounded-lg border overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead className="bg-muted/50">
            <tr className="text-left text-sm text-muted-foreground">
              <th className="p-3 w-10">
                <Checkbox
                  checked={allSelected ? true : someSelected ? 'indeterminate' : false}
                  onCheckedChange={handleSelectAll}
                  aria-label="Select all"
                />
              </th>
              <th className="p-3 font-medium">
                <SortHeader label="Filename" field="filename" currentSort={sortBy} currentOrder={sortOrder} onSort={onSortChange} />
              </th>
              <th className="p-3 font-medium">Game</th>
              <th className="p-3 font-medium">Status</th>
              <th className="p-3 font-medium w-36">Progress</th>
              <th className="p-3 font-medium">
                <SortHeader label="Size" field="filesize" currentSort={sortBy} currentOrder={sortOrder} onSort={onSortChange} />
              </th>
              <th className="p-3 font-medium">Chunks</th>
              <th className="p-3 font-medium">Pages</th>
              <th className="p-3 font-medium">
                <SortHeader label="Uploaded" field="uploadedat" currentSort={sortBy} currentOrder={sortOrder} onSort={onSortChange} />
              </th>
              <th className="p-3 font-medium w-10" />
            </tr>
          </thead>
          <tbody className="divide-y">
            {items.map((pdf) => {
              const pdfState = mapToPdfState(pdf.processingState);
              const isTerminal = isPdfStateTerminal(pdfState);

              return (
                <tr
                  key={pdf.id}
                  className={`hover:bg-muted/30 ${selectedIds.has(pdf.id) ? 'bg-muted/20' : ''}`}
                >
                  <td className="p-3">
                    <Checkbox
                      checked={selectedIds.has(pdf.id)}
                      onCheckedChange={() => handleSelectOne(pdf.id)}
                      aria-label={`Select ${pdf.fileName}`}
                    />
                  </td>
                  <td className="p-3 text-sm font-mono max-w-[200px] truncate" title={pdf.fileName}>
                    {pdf.fileName}
                  </td>
                  <td className="p-3 text-sm max-w-[150px] truncate" title={pdf.gameTitle || undefined}>
                    {pdf.gameTitle || '—'}
                  </td>
                  <td className="p-3">
                    <PdfStatusBadge state={pdfState} variant="compact" />
                  </td>
                  <td className="p-3">
                    {!isTerminal && (
                      <PdfProgressBar
                        state={pdfState}
                        progress={pdf.progressPercentage}
                        showLabel={false}
                        className="w-28"
                      />
                    )}
                    {isTerminal && (
                      <span className="text-xs text-muted-foreground">
                        {pdfState === 'ready' ? '100%' : '—'}
                      </span>
                    )}
                  </td>
                  <td className="p-3 text-sm text-muted-foreground">
                    {formatFileSize(pdf.fileSizeBytes)}
                  </td>
                  <td className="p-3 text-sm">{pdf.chunkCount}</td>
                  <td className="p-3 text-sm">{pdf.pageCount || '—'}</td>
                  <td className="p-3 text-sm text-muted-foreground whitespace-nowrap">
                    {new Date(pdf.uploadedAt).toLocaleDateString('en-US', {
                      month: 'short',
                      day: 'numeric',
                      year: 'numeric',
                    })}
                  </td>
                  <td className="p-3">
                    <PdfRowActions
                      pdf={pdf}
                      onDelete={onDelete}
                      onReindex={onReindex}
                      onRetry={onRetry}
                    />
                  </td>
                </tr>
              );
            })}
            {items.length === 0 && (
              <tr>
                <td colSpan={10} className="p-8 text-center text-muted-foreground">
                  No PDF documents found.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
