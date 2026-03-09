'use client';

import { useState, useCallback } from 'react';

import { useQuery } from '@tanstack/react-query';
import { FileTextIcon, CopyIcon, CheckIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';

import { getExtractedText } from '../lib/queue-api';

interface ExtractedTextPreviewModalProps {
  pdfDocumentId: string | null;
  fileName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ExtractedTextPreviewModal({
  pdfDocumentId,
  fileName,
  open,
  onOpenChange,
}: ExtractedTextPreviewModalProps) {
  const [copied, setCopied] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'queue', 'extracted-text', pdfDocumentId],
    queryFn: () => getExtractedText(pdfDocumentId!),
    enabled: open && !!pdfDocumentId,
  });

  const handleCopy = useCallback(async () => {
    if (data?.extractedText) {
      await navigator.clipboard.writeText(data.extractedText);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  }, [data?.extractedText]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[80vh] flex flex-col">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FileTextIcon className="h-5 w-5" />
            Extracted Text: {fileName}
          </DialogTitle>
        </DialogHeader>

        {isLoading && (
          <div className="space-y-2 py-4">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-5/6" />
            <Skeleton className="h-4 w-4/6" />
          </div>
        )}

        {!isLoading && data && (
          <>
            {/* Meta row */}
            <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
              <Badge variant="outline">{data.processingStatus}</Badge>
              {data.pageCount != null && <span>{data.pageCount} pages</span>}
              {data.characterCount != null && (
                <span>{data.characterCount.toLocaleString()} chars</span>
              )}
              {data.processedAt && (
                <span>Processed: {new Date(data.processedAt).toLocaleString()}</span>
              )}
            </div>

            {data.processingError && (
              <div className="p-3 bg-red-50/80 dark:bg-red-900/20 rounded-lg border border-red-200/50 dark:border-red-800/30">
                <p className="text-xs text-red-700 dark:text-red-300 font-mono">
                  {data.processingError}
                </p>
              </div>
            )}

            {/* Text content */}
            <div className="flex-1 min-h-0 relative">
              <Button
                variant="ghost"
                size="sm"
                className="absolute top-2 right-2 z-10"
                onClick={handleCopy}
                disabled={!data.extractedText}
              >
                {copied ? (
                  <CheckIcon className="h-4 w-4 text-emerald-600" />
                ) : (
                  <CopyIcon className="h-4 w-4" />
                )}
              </Button>
              <ScrollArea className="h-[50vh] w-full rounded-lg border bg-slate-50 dark:bg-zinc-900 p-4">
                {data.extractedText ? (
                  <pre className="text-xs font-mono whitespace-pre-wrap text-foreground">
                    {data.extractedText}
                  </pre>
                ) : (
                  <p className="text-sm text-muted-foreground italic">
                    No extracted text available.
                  </p>
                )}
              </ScrollArea>
            </div>
          </>
        )}

        {!isLoading && !data && (
          <p className="text-sm text-muted-foreground py-8 text-center">Document not found.</p>
        )}
      </DialogContent>
    </Dialog>
  );
}
