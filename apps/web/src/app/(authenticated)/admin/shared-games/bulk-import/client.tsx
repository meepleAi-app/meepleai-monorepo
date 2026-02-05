'use client';

/**
 * Bulk Import from BGG Client Component
 * Issue #3385: BGG Bulk Import Feature
 *
 * Features:
 * - Paste multiple BGG IDs (one per line)
 * - CSV file upload with BGG IDs
 * - Real-time progress via SSE
 * - Results display (success/failed/skipped)
 */

import { useState, useCallback, useRef, useEffect, type ChangeEvent } from 'react';

import {
  ArrowLeft,
  Download,
  Upload,
  FileSpreadsheet,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Loader2,
} from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  CardFooter,
} from '@/components/ui/data-display/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Progress } from '@/components/ui/feedback/progress';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { cn } from '@/lib/utils';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface QueueItem {
  id: string;
  bggId: number;
  status: 'Queued' | 'Processing' | 'Completed' | 'Failed';
  gameName: string | null;
  errorMessage: string | null;
  queuedAt: string;
  processedAt: string | null;
}

interface QueueProgress {
  timestamp: string;
  queued: number;
  processing: number;
  completed: number;
  failed: number;
  eta: number;
  items: QueueItem[];
}

type ImportStep = 'input' | 'importing' | 'complete';

interface ImportResult {
  bggId: number;
  status: 'success' | 'failed' | 'skipped';
  gameName?: string;
  message?: string;
}

export function BulkImportClient() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();
  const eventSourceRef = useRef<EventSource | null>(null);
  const initialQueuedRef = useRef<number>(0);

  // State
  const [step, setStep] = useState<ImportStep>('input');
  const [inputMode, setInputMode] = useState<'paste' | 'csv'>('paste');
  const [bggIdsText, setBggIdsText] = useState('');
  const [csvFile, setCsvFile] = useState<File | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [progress, setProgress] = useState<QueueProgress | null>(null);
  const [results, setResults] = useState<ImportResult[]>([]);

  /**
   * Parse BGG IDs from text input
   */
  const parseBggIds = useCallback((text: string): number[] => {
    const lines = text.split(/[\n,]/).map((line) => line.trim());
    const ids: number[] = [];

    for (const line of lines) {
      if (!line) continue;

      // Extract number from BGG URL or plain ID
      const urlMatch = line.match(/boardgamegeek\.com\/boardgame\/(\d+)/);
      if (urlMatch) {
        ids.push(parseInt(urlMatch[1], 10));
        continue;
      }

      // Try parsing as plain number
      const num = parseInt(line, 10);
      if (!isNaN(num) && num > 0) {
        ids.push(num);
      }
    }

    return [...new Set(ids)]; // Remove duplicates
  }, []);

  /**
   * Parse BGG IDs from CSV file
   */
  const parseCsvFile = useCallback(async (file: File): Promise<number[]> => {
    const text = await file.text();
    return parseBggIds(text);
  }, [parseBggIds]);

  /**
   * Handle CSV file upload
   */
  const handleFileChange = useCallback((e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setCsvFile(file);
    }
  }, []);

  /**
   * Connect to SSE stream for progress updates
   */
  const connectToStream = useCallback(() => {
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
    }

    const eventSource = new EventSource(`${API_BASE}/api/v1/admin/bgg-queue/stream`, {
      withCredentials: true,
    });

    eventSource.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data) as QueueProgress;
        setProgress(data);

        // Track initial queue size on first message
        if (initialQueuedRef.current === 0 && data.queued > 0) {
          initialQueuedRef.current = data.queued + data.processing;
        }
      } catch (error) {
        console.error('Error parsing SSE data:', error);
      }
    };

    eventSource.onerror = () => {
      console.error('SSE connection error');
      eventSource.close();
    };

    eventSourceRef.current = eventSource;
  }, []);

  /**
   * Disconnect from SSE stream
   */
  const disconnectFromStream = useCallback(() => {
    if (eventSourceRef.current) {
      eventSourceRef.current.close();
      eventSourceRef.current = null;
    }
  }, []);

  /**
   * Submit bulk import
   */
  const handleSubmit = useCallback(async () => {
    let bggIds: number[] = [];

    if (inputMode === 'paste') {
      bggIds = parseBggIds(bggIdsText);
    } else if (csvFile) {
      bggIds = await parseCsvFile(csvFile);
    }

    if (bggIds.length === 0) {
      toast.error('No valid BGG IDs found');
      return;
    }

    if (bggIds.length > 100) {
      toast.error('Maximum 100 games per batch. Please split your import.');
      return;
    }

    setIsSubmitting(true);
    setStep('importing');
    initialQueuedRef.current = bggIds.length;

    try {
      // Start SSE connection before submitting
      connectToStream();

      // Submit batch to API
      const response = await fetch(`${API_BASE}/api/v1/admin/bgg-queue/batch`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({ bggIds }),
      });

      if (!response.ok) {
        throw new Error(`Failed to queue import: ${response.statusText}`);
      }

      toast.success(`Queued ${bggIds.length} games for import`);
    } catch (error) {
      console.error('Error submitting bulk import:', error);
      toast.error('Failed to start bulk import');
      setStep('input');
      setIsSubmitting(false);
      disconnectFromStream();
    }
  }, [inputMode, bggIdsText, csvFile, parseBggIds, parseCsvFile, connectToStream, disconnectFromStream]);

  /**
   * Check if import is complete
   */
  useEffect(() => {
    if (step === 'importing' && progress) {
      const totalProcessed = progress.completed + progress.failed;

      // Consider complete when no more items are queued or processing
      if (progress.queued === 0 && progress.processing === 0 && totalProcessed > 0) {
        // Build results from final state
        setResults(
          progress.items.map((item) => ({
            bggId: item.bggId,
            status: item.status === 'Completed' ? 'success' : item.status === 'Failed' ? 'failed' : 'skipped',
            gameName: item.gameName || undefined,
            message: item.errorMessage || undefined,
          }))
        );
        setStep('complete');
        setIsSubmitting(false);
        disconnectFromStream();
      }
    }
  }, [step, progress, disconnectFromStream]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      disconnectFromStream();
    };
  }, [disconnectFromStream]);

  /**
   * Reset to start new import
   */
  const handleReset = useCallback(() => {
    setStep('input');
    setBggIdsText('');
    setCsvFile(null);
    setProgress(null);
    setResults([]);
    initialQueuedRef.current = 0;
    disconnectFromStream();
  }, [disconnectFromStream]);

  /**
   * Navigate back
   */
  const handleBack = useCallback(() => {
    router.push('/admin/shared-games');
  }, [router]);

  // Calculate progress percentage
  const progressPercentage =
    progress && initialQueuedRef.current > 0
      ? Math.round(((progress.completed + progress.failed) / initialQueuedRef.current) * 100)
      : 0;

  if (!user) return null;

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto py-8 px-4">
        {/* Header */}
        <div className="mb-8">
          <Button variant="ghost" size="sm" onClick={handleBack} className="mb-4">
            <ArrowLeft className="h-4 w-4 mr-2" />
            Torna al Catalogo
          </Button>

          <div className="flex items-center gap-3">
            <div className="h-12 w-12 rounded-lg bg-primary/10 flex items-center justify-center">
              <Download className="h-6 w-6 text-primary" />
            </div>
            <div>
              <h1 className="text-3xl font-bold tracking-tight">Bulk Import da BGG</h1>
              <p className="text-muted-foreground">Importa multipli giochi da BoardGameGeek</p>
            </div>
          </div>
        </div>

        {/* Input Step */}
        {step === 'input' && (
          <Card className="max-w-2xl">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Upload className="h-5 w-5" />
                Input BGG IDs
              </CardTitle>
              <CardDescription>
                Inserisci gli ID dei giochi BGG da importare (max 100 per batch)
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Tabs value={inputMode} onValueChange={(v: string) => setInputMode(v as 'paste' | 'csv')}>
                <TabsList className="grid w-full grid-cols-2">
                  <TabsTrigger value="paste">Paste IDs</TabsTrigger>
                  <TabsTrigger value="csv">Upload CSV</TabsTrigger>
                </TabsList>

                <TabsContent value="paste" className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="bgg-ids">BGG IDs (uno per riga o separati da virgola)</Label>
                    <Textarea
                      id="bgg-ids"
                      placeholder={`13\n822\n178900\n\nOppure URL:\nhttps://boardgamegeek.com/boardgame/13/catan`}
                      value={bggIdsText}
                      onChange={(e) => setBggIdsText(e.target.value)}
                      rows={10}
                      className="font-mono"
                    />
                    <p className="text-xs text-muted-foreground">
                      {parseBggIds(bggIdsText).length} ID validi trovati
                    </p>
                  </div>
                </TabsContent>

                <TabsContent value="csv" className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="csv-file">File CSV con BGG IDs</Label>
                    <div className="flex items-center gap-2">
                      <Input
                        id="csv-file"
                        type="file"
                        accept=".csv,.txt"
                        onChange={handleFileChange}
                      />
                      <FileSpreadsheet className="h-5 w-5 text-muted-foreground" />
                    </div>
                    <p className="text-xs text-muted-foreground">
                      Il file deve contenere BGG IDs, uno per riga o separati da virgola
                    </p>
                    {csvFile && (
                      <p className="text-sm text-green-600">
                        File selezionato: {csvFile.name}
                      </p>
                    )}
                  </div>
                </TabsContent>
              </Tabs>
            </CardContent>
            <CardFooter>
              <Button
                onClick={handleSubmit}
                disabled={
                  isSubmitting ||
                  (inputMode === 'paste' && parseBggIds(bggIdsText).length === 0) ||
                  (inputMode === 'csv' && !csvFile)
                }
                className="w-full"
              >
                {isSubmitting ? (
                  <>
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                    Avvio import...
                  </>
                ) : (
                  <>
                    <Download className="h-4 w-4 mr-2" />
                    Avvia Import
                  </>
                )}
              </Button>
            </CardFooter>
          </Card>
        )}

        {/* Importing Step */}
        {step === 'importing' && (
          <Card className="max-w-2xl">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Loader2 className="h-5 w-5 animate-spin" />
                Importazione in corso...
              </CardTitle>
              <CardDescription>
                {progress
                  ? `Elaborazione ${progress.processing} giochi, ${progress.queued} in coda`
                  : 'Connessione alla coda di import...'}
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              {/* Progress Bar */}
              <div className="space-y-2">
                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Progresso</span>
                  <span className="font-medium">{progressPercentage}%</span>
                </div>
                <Progress value={progressPercentage} className="h-3" />
              </div>

              {/* Stats */}
              {progress && (
                <div className="grid grid-cols-4 gap-4 text-center">
                  <div className="space-y-1">
                    <div className="text-2xl font-bold text-muted-foreground">
                      {progress.queued}
                    </div>
                    <div className="text-xs text-muted-foreground">In coda</div>
                  </div>
                  <div className="space-y-1">
                    <div className="text-2xl font-bold text-blue-600">
                      {progress.processing}
                    </div>
                    <div className="text-xs text-muted-foreground">In elaborazione</div>
                  </div>
                  <div className="space-y-1">
                    <div className="text-2xl font-bold text-green-600">
                      {progress.completed}
                    </div>
                    <div className="text-xs text-muted-foreground">Completati</div>
                  </div>
                  <div className="space-y-1">
                    <div className="text-2xl font-bold text-red-600">
                      {progress.failed}
                    </div>
                    <div className="text-xs text-muted-foreground">Falliti</div>
                  </div>
                </div>
              )}

              {/* Active Items */}
              {progress && progress.items.length > 0 && (
                <div className="space-y-2">
                  <h4 className="text-sm font-medium">In elaborazione:</h4>
                  <div className="space-y-1 max-h-48 overflow-y-auto">
                    {progress.items.map((item) => (
                      <div
                        key={item.id}
                        className="flex items-center justify-between p-2 bg-muted/50 rounded-md text-sm"
                      >
                        <div className="flex items-center gap-2">
                          {item.status === 'Processing' && (
                            <Loader2 className="h-4 w-4 animate-spin text-blue-500" />
                          )}
                          {item.status === 'Queued' && (
                            <AlertCircle className="h-4 w-4 text-muted-foreground" />
                          )}
                          {item.status === 'Completed' && (
                            <CheckCircle2 className="h-4 w-4 text-green-500" />
                          )}
                          {item.status === 'Failed' && (
                            <XCircle className="h-4 w-4 text-red-500" />
                          )}
                          <span>{item.gameName || `BGG #${item.bggId}`}</span>
                        </div>
                        <Badge
                          variant={
                            item.status === 'Processing'
                              ? 'default'
                              : item.status === 'Completed'
                                ? 'default'
                                : 'secondary'
                          }
                          className={cn(
                            item.status === 'Completed' && 'bg-green-100 text-green-800',
                            item.status === 'Failed' && 'bg-red-100 text-red-800'
                          )}
                        >
                          {item.status}
                        </Badge>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* ETA */}
              {progress && progress.eta > 0 && (
                <p className="text-xs text-muted-foreground text-center">
                  Tempo stimato: ~{progress.eta} secondi
                </p>
              )}
            </CardContent>
          </Card>
        )}

        {/* Complete Step */}
        {step === 'complete' && (
          <div className="max-w-2xl space-y-6">
            <Alert className={progress && progress.failed > 0 ? 'border-orange-500' : 'border-green-500'}>
              <CheckCircle2 className="h-4 w-4" />
              <AlertTitle>Import completato!</AlertTitle>
              <AlertDescription>
                {progress && (
                  <>
                    {progress.completed} giochi importati con successo
                    {progress.failed > 0 && `, ${progress.failed} falliti`}
                  </>
                )}
              </AlertDescription>
            </Alert>

            {/* Results Summary */}
            {progress && (
              <Card>
                <CardHeader>
                  <CardTitle>Riepilogo Import</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="grid grid-cols-3 gap-4 text-center mb-6">
                    <div className="p-4 bg-green-50 dark:bg-green-950 rounded-lg">
                      <div className="text-3xl font-bold text-green-600">
                        {progress.completed}
                      </div>
                      <div className="text-sm text-muted-foreground">Importati</div>
                    </div>
                    <div className="p-4 bg-red-50 dark:bg-red-950 rounded-lg">
                      <div className="text-3xl font-bold text-red-600">
                        {progress.failed}
                      </div>
                      <div className="text-sm text-muted-foreground">Falliti</div>
                    </div>
                    <div className="p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
                      <div className="text-3xl font-bold text-gray-600">
                        {initialQueuedRef.current - progress.completed - progress.failed}
                      </div>
                      <div className="text-sm text-muted-foreground">Skippati</div>
                    </div>
                  </div>

                  {/* Results List */}
                  {results.length > 0 && (
                    <div className="space-y-2 max-h-64 overflow-y-auto">
                      {results.map((result, idx) => (
                        <div
                          key={idx}
                          className="flex items-center justify-between p-2 bg-muted/50 rounded-md text-sm"
                        >
                          <div className="flex items-center gap-2">
                            {result.status === 'success' && (
                              <CheckCircle2 className="h-4 w-4 text-green-500" />
                            )}
                            {result.status === 'failed' && (
                              <XCircle className="h-4 w-4 text-red-500" />
                            )}
                            {result.status === 'skipped' && (
                              <AlertCircle className="h-4 w-4 text-yellow-500" />
                            )}
                            <span>
                              {result.gameName || `BGG #${result.bggId}`}
                            </span>
                          </div>
                          {result.message && (
                            <span className="text-xs text-muted-foreground truncate max-w-[200px]">
                              {result.message}
                            </span>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                </CardContent>
                <CardFooter className="flex gap-2">
                  <Button onClick={handleReset} variant="outline" className="flex-1">
                    Nuovo Import
                  </Button>
                  <Button onClick={handleBack} className="flex-1">
                    Vai al Catalogo
                  </Button>
                </CardFooter>
              </Card>
            )}
          </div>
        )}
      </div>
    </AdminAuthGuard>
  );
}
