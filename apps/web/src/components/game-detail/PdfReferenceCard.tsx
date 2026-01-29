/**
 * PDF Reference Card Component (Issue #3156)
 *
 * Clickable card displaying PDF reference in chat messages.
 * Triggers PDF viewer to jump to referenced page.
 */

'use client';

import { FileText, ChevronRight } from 'lucide-react';

export interface PdfReference {
  pdfId: string;
  pdfName: string;
  pageNumber: number;
  excerpt: string;
  confidence?: number;
}

export interface PdfReferenceCardProps {
  reference: PdfReference;
  /** Callback to trigger PDF page navigation */
  onJumpToPage: (pageNumber: number, pdfId: string) => void;
}

export function PdfReferenceCard({ reference, onJumpToPage }: PdfReferenceCardProps) {
  const handleClick = () => {
    onJumpToPage(reference.pageNumber, reference.pdfId);
  };

  return (
    <button
      onClick={handleClick}
      className="w-full mt-3 p-3 bg-white rounded-lg border border-blue-200 hover:border-blue-400 hover:shadow-sm transition-all group text-left"
    >
      <div className="flex items-start gap-2">
        <FileText className="w-4 h-4 text-blue-600 flex-shrink-0 mt-0.5" />
        <div className="flex-1 min-w-0">
          <p className="text-xs font-medium text-blue-900 mb-1">
            📄 {reference.pdfName} - Pag. {reference.pageNumber}
          </p>
          <p className="text-xs text-blue-700 line-clamp-2">"{reference.excerpt}"</p>
        </div>
        <ChevronRight className="w-4 h-4 text-blue-600 flex-shrink-0 group-hover:translate-x-1 transition-transform" />
      </div>
    </button>
  );
}
