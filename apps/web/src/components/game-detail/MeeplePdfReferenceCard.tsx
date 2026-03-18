/**
 * MeeplePdfReferenceCard Component
 *
 * Clickable card displaying PDF reference in chat messages,
 * built on MeepleCard with entity="kb" and variant="list".
 *
 * Migrated from PdfReferenceCard to use the unified MeepleCard system.
 * Uses "list" variant (64x64 thumbnail) instead of "compact" (40x40)
 * to provide adequate space for PDF name, page number, and excerpt.
 */

'use client';

import { FileText } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';

export interface PdfReference {
  pdfId: string;
  pdfName: string;
  pageNumber: number;
  excerpt: string;
  confidence?: number;
}

export interface MeeplePdfReferenceCardProps {
  reference: PdfReference;
  /** Callback to trigger PDF page navigation */
  onJumpToPage: (pageNumber: number, pdfId: string) => void;
}

export function MeeplePdfReferenceCard({ reference, onJumpToPage }: MeeplePdfReferenceCardProps) {
  const handleClick = () => {
    onJumpToPage(reference.pageNumber, reference.pdfId);
  };

  const metadata: MeepleCardMetadata[] = [];
  if (reference.excerpt) {
    metadata.push({
      icon: FileText,
      label: `"${reference.excerpt}"`,
    });
  }

  return (
    <div className="mt-3">
      <MeepleCard
        entity="kb"
        variant="list"
        title={reference.pdfName}
        subtitle={`Pag. ${reference.pageNumber}`}
        metadata={metadata}
        onClick={handleClick}
        className="hover:border-[hsl(174,60%,40%)]/50"
        data-testid="meeple-pdf-reference-card"
      />
    </div>
  );
}
