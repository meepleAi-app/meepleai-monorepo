/**
 * PDF Controls Component (Issue #3155)
 *
 * Navigation and zoom controls for PDF viewer.
 */

'use client';

import {
  ChevronLeft,
  ChevronRight,
  ZoomIn,
  ZoomOut,
  Maximize2,
  RotateCcw,
  Download,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

export interface PdfControlsProps {
  currentPage: number;
  totalPages: number;
  zoom: number;
  onPageChange: (page: number) => void;
  onNextPage: () => void;
  onPreviousPage: () => void;
  onZoomIn: () => void;
  onZoomOut: () => void;
  onZoomFitWidth: () => void;
  onZoomReset: () => void;
  pdfUrl: string;
  disabled?: boolean;
}

export function PdfControls({
  currentPage,
  totalPages,
  zoom,
  onPageChange,
  onNextPage,
  onPreviousPage,
  onZoomIn,
  onZoomOut,
  onZoomFitWidth,
  onZoomReset,
  pdfUrl,
  disabled = false,
}: PdfControlsProps) {
  const handlePageInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const page = parseInt(e.target.value, 10);
    if (!isNaN(page) && page >= 1 && page <= totalPages) {
      onPageChange(page);
    }
  };

  const handleDownload = () => {
    window.open(pdfUrl, '_blank');
  };

  const zoomPercentage = Math.round(zoom * 100);

  return (
    <div className="pdf-controls flex items-center justify-between px-4 py-3 bg-white/95 backdrop-blur-md border-b border-gray-200">
      {/* Page Navigation */}
      <div className="flex items-center gap-2">
        <Button
          variant="ghost"
          size="sm"
          onClick={onPreviousPage}
          disabled={disabled || currentPage <= 1}
          title="Pagina precedente"
        >
          <ChevronLeft className="h-4 w-4" />
        </Button>

        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-gray-700">Pag.</span>
          <Input
            type="number"
            min={1}
            max={totalPages}
            value={currentPage}
            onChange={handlePageInputChange}
            disabled={disabled}
            className="w-16 h-8 text-center text-sm"
          />
          <span className="text-sm text-gray-600">/ {totalPages || '-'}</span>
        </div>

        <Button
          variant="ghost"
          size="sm"
          onClick={onNextPage}
          disabled={disabled || currentPage >= totalPages}
          title="Pagina successiva"
        >
          <ChevronRight className="h-4 w-4" />
        </Button>
      </div>

      {/* Zoom Controls */}
      <div className="flex items-center gap-2">
        <Button
          variant="ghost"
          size="sm"
          onClick={onZoomOut}
          disabled={disabled || zoom <= 0.5}
          title="Zoom indietro"
        >
          <ZoomOut className="h-4 w-4" />
        </Button>

        <span className="text-sm text-gray-600 font-medium min-w-[60px] text-center">
          {zoomPercentage}%
        </span>

        <Button
          variant="ghost"
          size="sm"
          onClick={onZoomIn}
          disabled={disabled || zoom >= 2.0}
          title="Zoom avanti"
        >
          <ZoomIn className="h-4 w-4" />
        </Button>

        <div className="w-px h-6 bg-gray-300 mx-2" />

        <Button
          variant="ghost"
          size="sm"
          onClick={onZoomFitWidth}
          disabled={disabled}
          title="Adatta larghezza"
        >
          <Maximize2 className="h-4 w-4" />
        </Button>

        <Button
          variant="ghost"
          size="sm"
          onClick={onZoomReset}
          disabled={disabled}
          title="Reset zoom"
        >
          <RotateCcw className="h-4 w-4" />
        </Button>

        <div className="w-px h-6 bg-gray-300 mx-2" />

        <Button
          variant="ghost"
          size="sm"
          onClick={handleDownload}
          disabled={disabled}
          title="Scarica PDF"
        >
          <Download className="h-4 w-4" />
        </Button>
      </div>
    </div>
  );
}
