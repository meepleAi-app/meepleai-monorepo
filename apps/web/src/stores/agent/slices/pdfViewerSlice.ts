/**
 * PDF Viewer Slice - PDF viewer state management
 * Issue #3251 (FRONT-015)
 *
 * Manages PDF viewer state for citation linking:
 * - PDF viewer open/close
 * - Current page and document
 * - Last viewed page (localStorage)
 */

import type { AgentStore } from '../types/store.types';
import type { StateCreator } from 'zustand';

// ============================================================================
// Types
// ============================================================================

export interface PdfViewerSlice {
  // State
  /** Whether PDF viewer modal is open */
  pdfViewerOpen: boolean;
  /** Current page number (1-indexed) */
  pdfViewerPage: number | null;
  /** Current document ID */
  pdfViewerDocumentId: string | null;

  // Actions
  /** Open PDF viewer at specific page */
  openPdfViewer: (documentId: string, page: number) => void;
  /** Close PDF viewer */
  closePdfViewer: () => void;
  /** Set current page (for navigation within viewer) */
  setPdfViewerPage: (page: number) => void;
}

// ============================================================================
// Constants
// ============================================================================

const LAST_PDF_PAGE_KEY = 'meepleai-last-pdf-page';

// ============================================================================
// Helpers
// ============================================================================

/**
 * Get last viewed page from localStorage
 */
function getLastPdfPage(): number | null {
  if (typeof window === 'undefined') return null;
  const stored = localStorage.getItem(LAST_PDF_PAGE_KEY);
  return stored ? parseInt(stored, 10) : null;
}

/**
 * Save last viewed page to localStorage
 */
function saveLastPdfPage(page: number): void {
  if (typeof window === 'undefined') return;
  localStorage.setItem(LAST_PDF_PAGE_KEY, page.toString());
}

// ============================================================================
// Slice Creator
// ============================================================================

export const createPdfViewerSlice: StateCreator<AgentStore, [], [], PdfViewerSlice> = set => ({
  // Initial state
  pdfViewerOpen: false,
  pdfViewerPage: null,
  pdfViewerDocumentId: null,

  // Actions
  openPdfViewer: (documentId, page) => {
    saveLastPdfPage(page);
    set({
      pdfViewerOpen: true,
      pdfViewerPage: page,
      pdfViewerDocumentId: documentId,
    });
  },

  closePdfViewer: () => {
    set({
      pdfViewerOpen: false,
      pdfViewerPage: null,
      pdfViewerDocumentId: null,
    });
  },

  setPdfViewerPage: page => {
    saveLastPdfPage(page);
    set({ pdfViewerPage: page });
  },
});

// Export helper for components that need last page without store
export { getLastPdfPage };
