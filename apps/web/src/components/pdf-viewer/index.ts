/**
 * PDF Viewer Components Export (Issue #3155)
 */

// Dynamic import to prevent DOMMatrix SSR error with react-pdf (Issue #4133)
import dynamic from 'next/dynamic';

export const PdfViewer = dynamic(
  () => import('./PdfViewer').then(mod => ({ default: mod.PdfViewer })),
  { ssr: false }
);
export type { PdfViewerProps, PdfViewerRef } from './PdfViewer';

export { PdfControls, type PdfControlsProps } from './PdfControls';
