/**
 * Mock for @react-pdf-viewer/default-layout (not installed as dependency)
 * Used by PdfViewerModal component
 */

import { vi } from 'vitest';

export const defaultLayoutPlugin = vi.fn(() => ({
  install: vi.fn(),
}));
