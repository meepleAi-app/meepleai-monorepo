'use client';

import { PdfInlineViewer } from '@/components/pdf/PdfInlineViewer';

interface KbDocPreviewPanelProps {
  /** Document UUID. Empty string → render null (panel-level guard). */
  readonly docId: string;
}

/**
 * KbDocPreviewPanel — admin variant of PdfInlineViewer.
 * Renders the original PDF blob with the full admin toolbar
 * (jump-to-page, zoom, open-in-tab, download). No anti-leak (admin context).
 *
 * Mounted by KbDocDetailPanel when activeTab === 'preview'.
 *
 * Issue #1654 F3-FU-5.
 */
export function KbDocPreviewPanel({ docId }: KbDocPreviewPanelProps) {
  if (!docId) return null;
  return (
    <div className="p-4">
      <PdfInlineViewer
        documentId={docId}
        features={{
          download: true,
          openInTab: true,
          jumpToPage: true,
          zoom: true,
        }}
      />
    </div>
  );
}
