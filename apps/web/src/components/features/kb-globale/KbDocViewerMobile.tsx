/**
 * KbDocViewerMobile — bottom-sheet PDF viewer with tab switcher.
 *
 * D-J: extends BottomSheet primitive pattern (fixed bottom + rounded-top).
 * Tab switch: PDF | Citations. Same data contract as desktop variant (D-E page-level citations).
 *
 * fileUrl derivation: Task 5 verification — orchestrator (Task 9) computes
 * `/api/v1/pdfs/${doc.id}/download` and passes as required prop.
 *
 * @see admin-mockups/design_files/sp4-kb-globale.jsx:1306 (mobile mockup)
 */

'use client';

import { type JSX, useState } from 'react';

import { Document, Page, pdfjs } from 'react-pdf';

import { cn } from '@/lib/utils';

import type { KbDocViewerCitation } from './KbDocViewerDesktop';

if (typeof window !== 'undefined' && !pdfjs.GlobalWorkerOptions.workerSrc) {
  pdfjs.GlobalWorkerOptions.workerSrc = `https://unpkg.com/pdfjs-dist@${pdfjs.version}/build/pdf.worker.min.mjs`;
}

export interface KbDocViewerMobileLabels {
  pageLabel: (n: number) => string;
  closeLabel: string;
  pageOfTotal: (cur: number, total: number) => string;
  tabPdf: string;
  tabCitations: string;
}

export interface KbDocViewerMobileProps {
  doc: { id: string; title: string; fileUrl: string; pageCount: number };
  activePage: number;
  citations: readonly KbDocViewerCitation[];
  labels: KbDocViewerMobileLabels;
  onPageChange: (page: number) => void;
  onClose: () => void;
}

export function KbDocViewerMobile({
  doc,
  activePage,
  citations,
  labels,
  onClose,
}: KbDocViewerMobileProps): JSX.Element {
  const [tab, setTab] = useState<'pdf' | 'citations'>('pdf');

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={doc.title}
      data-slot="kb-doc-viewer-mobile"
      className="fixed inset-x-0 bottom-0 top-12 bg-card rounded-t-2xl border-t border-entity-kb/20 shadow-2xl overflow-hidden flex flex-col"
    >
      {/* Sticky header */}
      <div className="flex items-center gap-3 px-4 py-3 bg-card border-b border-entity-kb/15">
        <button
          type="button"
          aria-label={labels.closeLabel}
          onClick={onClose}
          className="w-8 h-8 rounded-full bg-muted text-foreground"
        >
          ✕
        </button>
        <div className="flex-1 min-w-0">
          <div className="font-display font-bold text-sm truncate">📄 {doc.title}</div>
        </div>
        <div className="px-2.5 py-1 rounded-full bg-entity-kb/10 text-entity-kb font-mono text-xs font-bold">
          {labels.pageOfTotal(activePage, doc.pageCount)}
        </div>
      </div>

      {/* Tabs */}
      <div role="tablist" className="flex border-b border-border bg-card">
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'pdf'}
          onClick={() => setTab('pdf')}
          className={cn(
            'flex-1 py-2 text-sm font-medium',
            tab === 'pdf' ? 'text-entity-kb border-b-2 border-entity-kb' : 'text-muted-foreground'
          )}
        >
          {labels.tabPdf}
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'citations'}
          onClick={() => setTab('citations')}
          className={cn(
            'flex-1 py-2 text-sm font-medium',
            tab === 'citations'
              ? 'text-entity-kb border-b-2 border-entity-kb'
              : 'text-muted-foreground'
          )}
        >
          {labels.tabCitations}
        </button>
      </div>

      {/* Body */}
      <div className="flex-1 overflow-y-auto p-4 bg-muted">
        {tab === 'pdf' ? (
          <Document file={doc.fileUrl}>
            <Page pageNumber={activePage} />
          </Document>
        ) : (
          <ul className="space-y-2">
            {citations.map(c => (
              <li key={c.n} className="rounded-md p-3 bg-card border border-border">
                <div className="font-mono text-[10px] text-muted-foreground">
                  {c.n} · {c.refText}
                </div>
                <div className="text-xs text-foreground mt-1">{c.snippet}</div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
