'use client';

import { MoreHorizontal, Trash2, RotateCcw, RefreshCw } from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import type { AdminPdfListItem } from '@/lib/api/clients/pdfClient';

interface PdfRowActionsProps {
  pdf: AdminPdfListItem;
  onDelete: (pdfId: string, fileName: string) => void;
  onReindex: (pdfId: string, fileName: string) => void;
  onRetry: (pdfId: string, fileName: string) => void;
}

export function PdfRowActions({ pdf, onDelete, onReindex, onRetry }: PdfRowActionsProps) {
  const state = pdf.processingState.toLowerCase();
  const canReindex = state === 'ready' || state === 'failed';
  const canRetry = state === 'failed' && pdf.retryCount < 3;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
          <MoreHorizontal className="h-4 w-4" />
          <span className="sr-only">Actions</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {canRetry && (
          <DropdownMenuItem onClick={() => onRetry(pdf.id, pdf.fileName)}>
            <RotateCcw className="h-4 w-4 mr-2" />
            Retry Processing
          </DropdownMenuItem>
        )}
        {canReindex && (
          <DropdownMenuItem onClick={() => onReindex(pdf.id, pdf.fileName)}>
            <RefreshCw className="h-4 w-4 mr-2" />
            Reindex
          </DropdownMenuItem>
        )}
        <DropdownMenuItem
          onClick={() => onDelete(pdf.id, pdf.fileName)}
          className="text-red-600 focus:text-red-600"
        >
          <Trash2 className="h-4 w-4 mr-2" />
          Delete
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
