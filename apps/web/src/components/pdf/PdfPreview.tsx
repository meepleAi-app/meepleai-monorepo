/**
 * PdfPreview Component (Issue #4133, #4252)
 *
 * Simplified PDF preview for file upload validation.
 * Uses react-pdf with pdfjs-dist v5 for security (GHSA-wgrm-67xf-hhpq).
 */

'use client';

import { useState, useEffect, useCallback } from 'react';

import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';

pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url,
).toString();

export interface PdfPreviewProps {
  file: File;
  onClose?: () => void;
}

export function PdfPreview({ file }: PdfPreviewProps) {
  const [fileUrl, setFileUrl] = useState<string | null>(null);
  const [numPages, setNumPages] = useState<number>(0);

  // Convert File to URL for viewer
  useEffect(() => {
    const url = URL.createObjectURL(file);
    setFileUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);

  const onDocumentLoadSuccess = useCallback(({ numPages }: { numPages: number }) => {
    setNumPages(numPages);
  }, []);

  if (!fileUrl) {
    return <div className="flex items-center justify-center h-full">Loading...</div>;
  }

  return (
    <div className="pdf-preview-container h-full overflow-auto">
      <Document file={fileUrl} onLoadSuccess={onDocumentLoadSuccess}>
        {Array.from({ length: numPages }, (_, index) => (
          <Page
            key={`page_${index + 1}`}
            pageNumber={index + 1}
            width={600}
            className="mb-2"
          />
        ))}
      </Document>
    </div>
  );
}
