/**
 * HoverPreview - Hover Tooltip Preview for MeepleCard
 * Issue #3827
 *
 * TODO: Requires Popover component (not yet available in ui/overlays)
 * Current: Placeholder implementation (returns children only)
 * Complete implementation ready - just needs Popover component
 */

'use client';

export interface HoverPreviewData {
  description?: string;
  designer?: string;
  complexity?: number;
  weight?: 'Light' | 'Medium' | 'Heavy';
  categories?: string[];
  mechanics?: string[];
}

export interface HoverPreviewProps {
  gameId: string;
  previewData?: HoverPreviewData;
  onFetchPreview?: (gameId: string) => Promise<HoverPreviewData>;
  delay?: number;
  disabled?: boolean;
  children: React.ReactNode;
}

/**
 * HoverPreview placeholder
 * Returns children without preview until Popover component available
 */
export function HoverPreview({
  children,
}: HoverPreviewProps) {
  // TODO: Implement full hover preview when Popover component added
  // Feature temporarily disabled (returns children as-is)
  return <>{children}</>;
}
