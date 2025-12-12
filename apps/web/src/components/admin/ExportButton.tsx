/**
 * ExportButton Component
 *
 * Dropdown button for exporting dashboard data to CSV or PDF formats.
 * Uses Shadcn/UI components for consistent design.
 *
 * Issue #2139: Testing Dashboard Export Functionality
 */

'use client';

import { useState } from 'react';
import { Download, FileText, FileSpreadsheet } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { toast } from 'sonner';

export interface ExportButtonProps {
  /**
   * Function to call when CSV export is requested
   */
  onExportCSV: () => void;

  /**
   * Function to call when PDF export is requested
   */
  onExportPDF: () => Promise<void>;

  /**
   * Whether export is currently in progress
   */
  isExporting?: boolean;

  /**
   * Optional className for button styling
   */
  className?: string;
}

/**
 * ExportButton component for dashboard data export
 */
export function ExportButton({
  onExportCSV,
  onExportPDF,
  isExporting = false,
  className,
}: ExportButtonProps) {
  const [isPdfExporting, setIsPdfExporting] = useState(false);

  const handleCSVExport = () => {
    try {
      onExportCSV();
      toast.success('CSV exported successfully');
    } catch (error) {
      console.error('CSV export failed:', error);
      toast.error('Failed to export CSV. Please try again.');
    }
  };

  const handlePDFExport = async () => {
    setIsPdfExporting(true);
    try {
      await onExportPDF();
      toast.success('PDF exported successfully');
    } catch (error) {
      console.error('PDF export failed:', error);
      toast.error('Failed to export PDF. Please try again.');
    } finally {
      setIsPdfExporting(false);
    }
  };

  const isDisabled = isExporting || isPdfExporting;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          disabled={isDisabled}
          className={className}
          aria-label="Export dashboard data"
        >
          <Download className="w-4 h-4 mr-2" aria-hidden="true" />
          {isPdfExporting ? 'Exporting...' : 'Export'}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onClick={handleCSVExport} disabled={isDisabled}>
          <FileSpreadsheet className="w-4 h-4 mr-2" aria-hidden="true" />
          Export as CSV
        </DropdownMenuItem>
        <DropdownMenuItem onClick={handlePDFExport} disabled={isDisabled}>
          <FileText className="w-4 h-4 mr-2" aria-hidden="true" />
          Export as PDF
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
