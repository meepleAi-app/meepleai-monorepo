/**
 * ExportButton Component (Issue #2611)
 *
 * Button with dropdown menu for exporting user library to CSV/JSON.
 * Features:
 * - Quick CSV export
 * - Advanced dropdown with format/scope options
 * - Loading state during export
 * - Toast notifications
 */

'use client';

import React, { useState } from 'react';

import { Download, ChevronDown, FileText, FileJson, Loader2 } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Button } from '@/components/ui/primitives/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import {
  exportLibrary,
  type ExportFormat,
  type ExportScope,
} from '@/lib/export/libraryExport';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

export interface ExportButtonProps {
  /** Library data to export */
  data: UserLibraryEntry[];
  /** Number of filtered items (for display) */
  filteredCount?: number;
  /** Total items in library (for display) */
  totalCount?: number;
  /** Whether to show dropdown or just quick export */
  showAdvanced?: boolean;
  /** Custom className */
  className?: string;
  /** Disabled state */
  disabled?: boolean;
}

export function ExportButton({
  data,
  filteredCount,
  totalCount,
  showAdvanced = true,
  className,
  disabled,
}: ExportButtonProps) {
  const [isExporting, setIsExporting] = useState(false);

  const handleExport = async (format: ExportFormat, scope: ExportScope) => {
    if (isExporting || disabled) return;

    setIsExporting(true);
    try {
      exportLibrary(data, { format, scope });
      const formatLabel = format === 'csv' ? 'CSV' : 'JSON';
      const scopeLabel = scope === 'minimal' ? 'base' : 'completo';
      toast.success(`Libreria esportata con successo (${formatLabel} ${scopeLabel})`);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : "Impossibile esportare la libreria."
      );
    } finally {
      setIsExporting(false);
    }
  };

  const handleQuickExport = () => {
    handleExport('csv', 'minimal');
  };

  if (!showAdvanced) {
    return (
      <Button
        variant="outline"
        size="sm"
        onClick={handleQuickExport}
        disabled={disabled || isExporting || data.length === 0}
        className={className}
      >
        {isExporting ? (
          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        ) : (
          <Download className="mr-2 h-4 w-4" />
        )}
        Esporta CSV
      </Button>
    );
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          disabled={disabled || isExporting || data.length === 0}
          className={className}
        >
          {isExporting ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <Download className="mr-2 h-4 w-4" />
          )}
          Esporta
          <ChevronDown className="ml-1 h-4 w-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-48">
        <DropdownMenuItem onClick={() => handleExport('csv', 'minimal')}>
          <FileText className="mr-2 h-4 w-4" />
          CSV - Base
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => handleExport('csv', 'full')}>
          <FileText className="mr-2 h-4 w-4" />
          CSV - Completo
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={() => handleExport('json', 'minimal')}>
          <FileJson className="mr-2 h-4 w-4" />
          JSON - Base
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => handleExport('json', 'full')}>
          <FileJson className="mr-2 h-4 w-4" />
          JSON - Completo
        </DropdownMenuItem>
        {(filteredCount !== undefined || totalCount !== undefined) && (
          <>
            <DropdownMenuSeparator />
            <div className="px-2 py-1.5 text-xs text-muted-foreground">
              {filteredCount !== undefined && totalCount !== undefined && filteredCount < totalCount
                ? `${filteredCount} di ${totalCount} giochi`
                : `${totalCount ?? data.length} giochi`}
            </div>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
