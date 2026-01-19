/**
 * ExportUsageButton Component (Issue #2521)
 *
 * Export button for AI usage reports with:
 * - Dropdown menu (CSV/JSON)
 * - Date range selection (optional)
 * - Model filter (optional)
 * - Blob download trigger
 */

'use client';

import { useState } from 'react';

import { Button } from '@/components/ui/primitives/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Loader2, Download, FileText, Code } from 'lucide-react';
import { toast } from '@/components/layout/Toast';

import { api, type ExportUsageReportParams } from '@/lib/api';

interface ExportUsageButtonProps {
  modelId?: string;
  startDate?: string;
  endDate?: string;
}

export function ExportUsageButton({ modelId, startDate, endDate }: ExportUsageButtonProps) {
  const [isExporting, setIsExporting] = useState(false);

  const handleExport = async (format: 'csv' | 'json') => {
    setIsExporting(true);

    const params: ExportUsageReportParams = {
      format,
      modelId,
      startDate,
      endDate,
    };

    try {
      const blob = await api.admin.exportUsageReport(params);

      // Trigger browser download
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `ai-usage-report-${new Date().toISOString().split('T')[0]}.${format}`;
      document.body.appendChild(a);
      a.click();

      // Revoke after delay to ensure download starts
      setTimeout(() => {
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      }, 100);

      toast.success(`Usage report exported successfully as ${format.toUpperCase()}`);
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to export usage report');
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" disabled={isExporting}>
          {isExporting ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Exporting...
            </>
          ) : (
            <>
              <Download className="mr-2 h-4 w-4" />
              Export Report
            </>
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onClick={() => handleExport('csv')} disabled={isExporting}>
          <FileText className="mr-2 h-4 w-4" />
          Export as CSV
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => handleExport('json')} disabled={isExporting}>
          <Code className="mr-2 h-4 w-4" />
          Export as JSON
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
