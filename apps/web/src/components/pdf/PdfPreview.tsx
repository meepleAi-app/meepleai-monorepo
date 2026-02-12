/**
 * PdfPreview Component (Issue #4133)
 *
 * Simplified PDF preview for file upload validation
 * Migration: react-pdf → @react-pdf-viewer/core
 */

'use client';

import { useState, useEffect } from 'react';

import { Viewer, Worker } from '@react-pdf-viewer/core';
import { defaultLayoutPlugin } from '@react-pdf-viewer/default-layout';

import '@react-pdf-viewer/core/lib/styles/index.css';
import '@react-pdf-viewer/default-layout/lib/styles/index.css';

export interface PdfPreviewProps {
  file: File;
  onClose?: () => void;
}

export function PdfPreview({ file, onClose }: PdfPreviewProps) {
  const [fileUrl, setFileUrl] = useState<string | null>(null);

  const defaultLayoutPluginInstance = defaultLayoutPlugin();

  // Convert File to URL for viewer
  useEffect(() => {
    const url = URL.createObjectURL(file);
    setFileUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);

  if (!fileUrl) {
    return <div className="flex items-center justify-center h-full">Loading...</div>;
  }

  return (
    <div className="pdf-preview-container h-full">
      <Worker workerUrl="https://unpkg.com/pdfjs-dist@3.0.279/build/pdf.worker.min.js">
        <Viewer fileUrl={fileUrl} plugins={[defaultLayoutPluginInstance]} />
      </Worker>
    </div>
  );
}
