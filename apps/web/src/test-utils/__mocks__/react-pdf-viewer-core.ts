/**
 * Mock for @react-pdf-viewer/core (not installed as dependency)
 * Used by PdfViewerModal component
 */

import { vi } from 'vitest';

export const Viewer = vi.fn(() => null);
export const Worker = vi.fn(({ children }: { children?: unknown }) => children ?? null);
export const SpecialZoomLevel = {
  ActualSize: 'ActualSize',
  PageFit: 'PageFit',
  PageWidth: 'PageWidth',
};
