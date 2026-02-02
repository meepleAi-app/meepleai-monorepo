/**
 * PDF Viewer Slice Tests
 * Issue #3251: PDF viewer state management
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { create } from 'zustand';

import { createPdfViewerSlice, PdfViewerSlice, getLastPdfPage } from '../pdfViewerSlice';

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: vi.fn((key: string) => store[key] || null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key];
    }),
    clear: vi.fn(() => {
      store = {};
    }),
  };
})();

Object.defineProperty(window, 'localStorage', { value: localStorageMock });

// Create a test store with only the pdfViewer slice
function createTestStore() {
  return create<PdfViewerSlice>()((...args) => ({
    ...createPdfViewerSlice(...args),
  }));
}

describe('pdfViewerSlice', () => {
  let store: ReturnType<typeof createTestStore>;

  beforeEach(() => {
    vi.clearAllMocks();
    localStorageMock.clear();
    store = createTestStore();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('Initial State', () => {
    it('has pdfViewerOpen set to false initially', () => {
      expect(store.getState().pdfViewerOpen).toBe(false);
    });

    it('has pdfViewerPage set to null initially', () => {
      expect(store.getState().pdfViewerPage).toBeNull();
    });

    it('has pdfViewerDocumentId set to null initially', () => {
      expect(store.getState().pdfViewerDocumentId).toBeNull();
    });
  });

  describe('openPdfViewer', () => {
    it('sets pdfViewerOpen to true', () => {
      store.getState().openPdfViewer('doc-123', 5);

      expect(store.getState().pdfViewerOpen).toBe(true);
    });

    it('sets pdfViewerPage to specified page', () => {
      store.getState().openPdfViewer('doc-123', 10);

      expect(store.getState().pdfViewerPage).toBe(10);
    });

    it('sets pdfViewerDocumentId to specified document', () => {
      store.getState().openPdfViewer('doc-456', 1);

      expect(store.getState().pdfViewerDocumentId).toBe('doc-456');
    });

    it('saves page to localStorage', () => {
      store.getState().openPdfViewer('doc-123', 15);

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        'meepleai-last-pdf-page',
        '15'
      );
    });

    it('updates state when opened multiple times', () => {
      store.getState().openPdfViewer('doc-1', 5);
      store.getState().openPdfViewer('doc-2', 20);

      expect(store.getState().pdfViewerDocumentId).toBe('doc-2');
      expect(store.getState().pdfViewerPage).toBe(20);
    });
  });

  describe('closePdfViewer', () => {
    beforeEach(() => {
      // Open viewer first
      store.getState().openPdfViewer('doc-123', 10);
    });

    it('sets pdfViewerOpen to false', () => {
      store.getState().closePdfViewer();

      expect(store.getState().pdfViewerOpen).toBe(false);
    });

    it('resets pdfViewerPage to null', () => {
      store.getState().closePdfViewer();

      expect(store.getState().pdfViewerPage).toBeNull();
    });

    it('resets pdfViewerDocumentId to null', () => {
      store.getState().closePdfViewer();

      expect(store.getState().pdfViewerDocumentId).toBeNull();
    });
  });

  describe('setPdfViewerPage', () => {
    beforeEach(() => {
      store.getState().openPdfViewer('doc-123', 1);
    });

    it('updates pdfViewerPage to new value', () => {
      store.getState().setPdfViewerPage(25);

      expect(store.getState().pdfViewerPage).toBe(25);
    });

    it('saves new page to localStorage', () => {
      store.getState().setPdfViewerPage(42);

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        'meepleai-last-pdf-page',
        '42'
      );
    });

    it('does not affect pdfViewerOpen state', () => {
      store.getState().setPdfViewerPage(10);

      expect(store.getState().pdfViewerOpen).toBe(true);
    });

    it('does not affect pdfViewerDocumentId state', () => {
      store.getState().setPdfViewerPage(10);

      expect(store.getState().pdfViewerDocumentId).toBe('doc-123');
    });
  });

  describe('getLastPdfPage', () => {
    it('returns null when no page is stored', () => {
      const page = getLastPdfPage();

      expect(page).toBeNull();
    });

    it('returns stored page number', () => {
      localStorageMock.setItem('meepleai-last-pdf-page', '33');

      const page = getLastPdfPage();

      expect(page).toBe(33);
    });

    it('parses stored string as integer', () => {
      localStorageMock.setItem('meepleai-last-pdf-page', '100');

      const page = getLastPdfPage();

      expect(page).toBe(100);
      expect(typeof page).toBe('number');
    });
  });

  describe('Workflow Integration', () => {
    it('can open, navigate, and close PDF viewer', () => {
      // Open at page 1
      store.getState().openPdfViewer('rulebook', 1);
      expect(store.getState().pdfViewerOpen).toBe(true);
      expect(store.getState().pdfViewerPage).toBe(1);

      // Navigate to page 15
      store.getState().setPdfViewerPage(15);
      expect(store.getState().pdfViewerPage).toBe(15);

      // Navigate to page 30
      store.getState().setPdfViewerPage(30);
      expect(store.getState().pdfViewerPage).toBe(30);

      // Close viewer
      store.getState().closePdfViewer();
      expect(store.getState().pdfViewerOpen).toBe(false);
      expect(store.getState().pdfViewerPage).toBeNull();
    });

    it('can switch between documents', () => {
      store.getState().openPdfViewer('doc-a', 5);
      expect(store.getState().pdfViewerDocumentId).toBe('doc-a');

      store.getState().openPdfViewer('doc-b', 1);
      expect(store.getState().pdfViewerDocumentId).toBe('doc-b');
      expect(store.getState().pdfViewerPage).toBe(1);
    });
  });
});
