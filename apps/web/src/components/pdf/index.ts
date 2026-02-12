// Barrel exports for pdf module
// Dynamic import to prevent DOMMatrix SSR error with react-pdf (Issue #4133)
import dynamic from 'next/dynamic';

export const PdfPreview = dynamic(
  () => import('./PdfPreview').then(mod => ({ default: mod.PdfPreview })),
  { ssr: false }
);
export type { PdfPreviewProps } from './PdfPreview';
export { PdfProcessingProgressBar } from './PdfProcessingProgressBar';
export type { PdfProcessingProgressBarProps } from './PdfProcessingProgressBar';
export { PdfTable } from './PdfTable';
export { PdfTableRow } from './PdfTableRow';
export { PdfUploadForm } from './PdfUploadForm';
