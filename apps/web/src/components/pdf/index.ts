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

// ============================================================================
// Issue #4217 - Multi-Location Status UI Components
// ============================================================================

export { PdfStatusBadge } from './PdfStatusBadge';
export type { PdfStatusBadgeProps } from './PdfStatusBadge';

export { PdfProgressBar } from './PdfProgressBar';
export type { PdfProgressBarProps } from './PdfProgressBar';

export { PdfErrorCard } from './PdfErrorCard';
export type { PdfErrorCardProps } from './PdfErrorCard';

export { PdfStatusTimeline } from './PdfStatusTimeline';
export type { PdfStatusTimelineProps } from './PdfStatusTimeline';
