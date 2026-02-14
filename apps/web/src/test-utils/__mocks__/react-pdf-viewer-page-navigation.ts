/**
 * Mock for @react-pdf-viewer/page-navigation (not installed as dependency)
 * Used by PdfViewerModal component
 */

import { vi } from 'vitest';

export const pageNavigationPlugin = vi.fn(() => ({
  install: vi.fn(),
}));
