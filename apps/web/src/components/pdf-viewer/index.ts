/**
 * PDF Viewer Components Export (Issue #3155)
 */

// Dynamic import to prevent SSR errors with react-pdf (Issue #4133, #4252)
import dynamic from 'next/dynamic';

export const PdfViewer = dynamic(
  () => import('./PdfViewer').then(mod => ({ default: mod.PdfViewer })),
  { ssr: false }
);
export type { PdfViewerProps, PdfViewerRef } from './PdfViewer';

export { PdfControls, type PdfControlsProps } from './PdfControls';
