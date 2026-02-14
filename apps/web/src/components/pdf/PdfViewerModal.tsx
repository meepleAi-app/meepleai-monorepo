/**
 * PdfViewerModal - Modal for PDF viewing (BGAI-074, #4133)
 * Migration: react-pdf → @react-pdf-viewer/core
 */

'use client';

import { useEffect } from 'react';

import { Viewer, Worker } from '@react-pdf-viewer/core';
import { defaultLayoutPlugin } from '@react-pdf-viewer/default-layout';
import { pageNavigationPlugin } from '@react-pdf-viewer/page-navigation';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';

import '@react-pdf-viewer/core/lib/styles/index.css';
import '@react-pdf-viewer/default-layout/lib/styles/index.css';

export interface PdfViewerModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  pdfUrl: string;
  initialPage?: number;
  documentName?: string;
}

export function PdfViewerModal({
  open,
  onOpenChange,
  pdfUrl,
  initialPage = 1,
  documentName = 'PDF Document',
}: PdfViewerModalProps) {
  const pageNavigationPluginInstance = pageNavigationPlugin();
  const { jumpToPage } = pageNavigationPluginInstance;
  const defaultLayoutPluginInstance = defaultLayoutPlugin();

  // Jump to initial page when modal opens
  useEffect(() => {
    if (open && initialPage > 1) {
      const timer = setTimeout(() => {
        jumpToPage(initialPage - 1); // 0-indexed
      }, 200);
      return () => clearTimeout(timer);
    }
  }, [open, initialPage, jumpToPage]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-6xl h-[90vh]">
        <DialogHeader>
          <DialogTitle>{documentName}</DialogTitle>
        </DialogHeader>
        <div className="flex-1 overflow-hidden">
          <Worker workerUrl="https://unpkg.com/pdfjs-dist@3.0.279/build/pdf.worker.min.js">
            <Viewer
              fileUrl={pdfUrl}
              plugins={[defaultLayoutPluginInstance, pageNavigationPluginInstance]}
            />
          </Worker>
        </div>
      </DialogContent>
    </Dialog>
  );
}

export default PdfViewerModal;
