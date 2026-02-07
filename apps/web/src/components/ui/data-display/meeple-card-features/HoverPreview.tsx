/**
 * HoverPreview - Hover Tooltip Preview for MeepleCard
 * Issue #3827
 */

'use client';

import { useState } from 'react';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/overlays/popover';

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

export function HoverPreview({
  gameId,
  previewData,
  onFetchPreview,
  delay = 300,
  disabled = false,
  children,
}: HoverPreviewProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [data, setData] = useState<HoverPreviewData | null>(previewData || null);
  const [loading, setLoading] = useState(false);

  // Disable on mobile (no hover on touch)
  if (disabled || (typeof window !== 'undefined' && window.matchMedia('(max-width: 768px)').matches)) {
    return <>{children}</>;
  }

  const handleMouseEnter = () => {
    const timer = setTimeout(async () => {
      setIsOpen(true);
      if (!data && onFetchPreview) {
        setLoading(true);
        try {
          const fetchedData = await onFetchPreview(gameId);
          setData(fetchedData);
        } catch (error) {
          console.error('HoverPreview: fetch error:', error);
        } finally {
          setLoading(false);
        }
      }
    }, delay);
    return () => clearTimeout(timer);
  };

  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger
        asChild
        onMouseEnter={handleMouseEnter}
        onMouseLeave={() => setIsOpen(false)}
      >
        {children}
      </PopoverTrigger>
      <PopoverContent className="w-80" data-testid="hover-preview-content">
        {loading ? (
          <div className="animate-pulse space-y-2">
            <div className="h-4 bg-muted rounded w-3/4" />
            <div className="h-3 bg-muted rounded w-1/2" />
          </div>
        ) : data ? (
          <div className="space-y-2 text-sm">
            {data.description && <p className="line-clamp-3">{data.description}</p>}
            {data.designer && <p className="text-muted-foreground">Designer: {data.designer}</p>}
            {data.complexity && <p className="text-muted-foreground">Complexity: {data.complexity}/5</p>}
          </div>
        ) : (
          <p className="text-muted-foreground text-sm">Preview not available</p>
        )}
      </PopoverContent>
    </Popover>
  );
}
