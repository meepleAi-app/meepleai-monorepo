'use client';

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { RefreshCw, Play, Loader2, AlertCircle, RotateCcw, X } from 'lucide-react';
import { toast } from 'sonner';

import {
  Button,
  Progress,
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui';
import { useApiClient } from '@/lib/api/context';

export default function AdminPdfsPage() {
  const apiClient = useApiClient();
  const [selectedError, setSelectedError] = useState<{
    id: string;
    fileName: string;
    error: string;
  } | null>(null);

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['admin', 'pdfs'],
    queryFn: () => fetch('/api/v1/admin/pdfs').then(r => r.json()),
    refetchInterval: 5000, // Auto-refresh every 5s
  });

  const handleProcessPending = async () => {
    try {
      const response = await fetch('/api/v1/admin/pdfs/process-pending', {
        method: 'POST',
      });
      const result = await response.json();
      toast.success(`Triggered ${result.triggered} PDFs for processing`);
      refetch();
    } catch (error) {
      toast.error('Failed to trigger processing');
    }
  };

  const handleRetryFailed = async (pdfId: string, fileName: string) => {
    try {
      const response = await fetch(`/api/v1/documents/${pdfId}/retry`, {
        method: 'POST',
      });

      if (!response.ok) {
        throw new Error('Retry failed');
      }

      toast.success(`Retrying ${fileName}...`);
      setSelectedError(null);
      refetch();
    } catch (error) {
      toast.error('Failed to retry processing');
    }
  };

  if (isLoading) {
    return <div className="p-6">Loading PDFs...</div>;
  }

  const pdfs = data?.items || [];

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">PDF Processing Status</h1>
          <p className="text-muted-foreground">
            Monitor RAG document processing ({pdfs.length} total)
          </p>
        </div>
        <div className="flex gap-2">
          <Button onClick={handleProcessPending} variant="default">
            <Play className="h-4 w-4 mr-2" />
            Process Pending
          </Button>
          <Button onClick={() => refetch()} variant="outline">
            <RefreshCw className="h-4 w-4 mr-2" />
            Refresh
          </Button>
        </div>
      </div>

      <div className="bg-card rounded-lg border">
        <table className="w-full">
          <thead className="bg-muted/50">
            <tr className="text-left text-sm">
              <th className="p-3 font-medium">Filename</th>
              <th className="p-3 font-medium">Game</th>
              <th className="p-3 font-medium">Status</th>
              <th className="p-3 font-medium">Pages</th>
              <th className="p-3 font-medium">Chunks</th>
              <th className="p-3 font-medium">Uploaded</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {pdfs.map((pdf: any) => (
              <tr
                key={pdf.id}
                className={`hover:bg-muted/30 ${
                  pdf.processingStatus === 'failed' ? 'cursor-pointer' : ''
                }`}
                onClick={() => {
                  if (pdf.processingStatus === 'failed' && pdf.processingError) {
                    setSelectedError({
                      id: pdf.id,
                      fileName: pdf.fileName,
                      error: pdf.processingError,
                    });
                  }
                }}
              >
                <td className="p-3 text-sm font-mono">{pdf.fileName}</td>
                <td className="p-3 text-sm">{pdf.gameTitle || '—'}</td>
                <td className="p-3">
                  {pdf.processingStatus === 'processing' ? (
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <Loader2 className="h-3 w-3 animate-spin text-blue-600" />
                        <span className="text-xs font-medium text-blue-800">Processing...</span>
                      </div>
                      <Progress value={33} className="h-1.5 w-32" />
                      <p className="text-[10px] text-muted-foreground">
                        Extracting → Chunking → Embedding
                      </p>
                    </div>
                  ) : (
                    <span
                      className={`inline-flex items-center gap-1 px-2 py-1 text-xs font-medium rounded ${
                        pdf.processingStatus === 'completed'
                          ? 'bg-green-100 text-green-800'
                          : pdf.processingStatus === 'failed'
                          ? 'bg-red-100 text-red-800 cursor-pointer hover:bg-red-200'
                          : 'bg-gray-100 text-gray-800'
                      }`}
                    >
                      {pdf.processingStatus === 'failed' && (
                        <AlertCircle className="h-3 w-3" />
                      )}
                      {pdf.processingStatus}
                      {pdf.processingStatus === 'failed' && (
                        <span className="text-[10px] opacity-70">(click for details)</span>
                      )}
                    </span>
                  )}
                </td>
                <td className="p-3 text-sm">{pdf.pageCount || '—'}</td>
                <td className="p-3 text-sm">{pdf.chunkCount}</td>
                <td className="p-3 text-sm text-muted-foreground">
                  {new Date(pdf.uploadedAt).toLocaleString()}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Error Details Modal */}
      <Dialog open={!!selectedError} onOpenChange={() => setSelectedError(null)}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <AlertCircle className="h-5 w-5 text-red-600" />
              Processing Error
            </DialogTitle>
            <DialogDescription>
              {selectedError?.fileName}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="rounded-lg bg-red-50 border border-red-200 p-4">
              <h4 className="text-sm font-medium text-red-900 mb-2">Error Message:</h4>
              <pre className="text-xs text-red-800 whitespace-pre-wrap font-mono">
                {selectedError?.error || 'No error details available'}
              </pre>
            </div>

            <p className="text-sm text-muted-foreground">
              Common causes: PDF corruption, unsupported format, missing text layer, service timeout.
            </p>
          </div>

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setSelectedError(null)}
            >
              <X className="h-4 w-4 mr-2" />
              Close
            </Button>
            <Button
              variant="default"
              onClick={() => {
                if (selectedError) {
                  handleRetryFailed(selectedError.id, selectedError.fileName);
                }
              }}
            >
              <RotateCcw className="h-4 w-4 mr-2" />
              Retry Processing
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
