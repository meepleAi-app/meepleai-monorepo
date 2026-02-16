'use client';

import { useQuery } from '@tanstack/react-query';
import { RefreshCw, Play } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui';
import { useApiClient } from '@/lib/api/context';

export default function AdminPdfsPage() {
  const apiClient = useApiClient();

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
              <tr key={pdf.id} className="hover:bg-muted/30">
                <td className="p-3 text-sm font-mono">{pdf.fileName}</td>
                <td className="p-3 text-sm">{pdf.gameTitle || '—'}</td>
                <td className="p-3">
                  <span
                    className={`inline-flex px-2 py-1 text-xs font-medium rounded ${
                      pdf.processingStatus === 'completed'
                        ? 'bg-green-100 text-green-800'
                        : pdf.processingStatus === 'processing'
                        ? 'bg-blue-100 text-blue-800'
                        : pdf.processingStatus === 'failed'
                        ? 'bg-red-100 text-red-800'
                        : 'bg-gray-100 text-gray-800'
                    }`}
                  >
                    {pdf.processingStatus}
                  </span>
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
    </div>
  );
}
