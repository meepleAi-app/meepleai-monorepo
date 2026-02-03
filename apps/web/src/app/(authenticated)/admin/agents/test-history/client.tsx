/**
 * Test History Client - Agent Test Results History
 * Issue #3379
 *
 * Admin interface for viewing and managing agent test results:
 * - View all test results with filtering
 * - Filter by typology, date range, saved status
 * - View detailed result information
 * - Delete individual results
 */

'use client';

import { useState, useCallback } from 'react';

import {
  History,
  Search,
  Trash2,
  Eye,
  RefreshCw,
  Calendar,
  Filter,
  X,
  Clock,
  Cpu,
  Zap,
  DollarSign,
  MessageSquare,
  ArrowLeft,
} from 'lucide-react';
import Link from 'next/link';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { ConfidenceBadge } from '@/components/ui/data-display/confidence-badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import type { TestResultsQuery } from '@/lib/api/schemas/test-results.schemas';
import {
  useTestResults,
  useTestResult,
  useDeleteTestResult,
} from '@/lib/hooks/useTestResults';

export function TestHistoryClient() {
  // Filter state
  const [query, setQuery] = useState<TestResultsQuery>({
    skip: 0,
    take: 20,
  });
  const [typologyFilter, setTypologyFilter] = useState<string>('');
  const [savedOnlyFilter, setSavedOnlyFilter] = useState(false);

  // Selection and dialog state
  const [selectedResultId, setSelectedResultId] = useState<string | null>(null);
  const [deleteTargetId, setDeleteTargetId] = useState<string | null>(null);

  // Fetch test results with current filters
  const effectiveQuery: TestResultsQuery = {
    ...query,
    typologyId: typologyFilter || undefined,
    savedOnly: savedOnlyFilter || undefined,
  };

  const {
    data: resultsData,
    isLoading,
    error,
    refetch,
  } = useTestResults(effectiveQuery);

  const { data: selectedResult, isLoading: isLoadingDetail } = useTestResult(
    selectedResultId || '',
    { enabled: !!selectedResultId }
  );

  const deleteResultMutation = useDeleteTestResult({
    onSuccess: () => {
      toast.success('Risultato eliminato con successo');
      setDeleteTargetId(null);
    },
    onError: (err) => {
      toast.error(`Errore durante l'eliminazione: ${err.message}`);
    },
  });

  // Handlers
  const handleRefresh = useCallback(() => {
    refetch();
    toast.success('Risultati aggiornati');
  }, [refetch]);

  const handleClearFilters = useCallback(() => {
    setTypologyFilter('');
    setSavedOnlyFilter(false);
    setQuery({ skip: 0, take: 20 });
  }, []);

  const handlePageChange = useCallback((newSkip: number) => {
    setQuery((prev) => ({ ...prev, skip: newSkip }));
  }, []);

  const handleViewDetail = useCallback((id: string) => {
    setSelectedResultId(id);
  }, []);

  const handleDeleteClick = useCallback((id: string) => {
    setDeleteTargetId(id);
  }, []);

  const handleConfirmDelete = useCallback(() => {
    if (deleteTargetId) {
      deleteResultMutation.mutate(deleteTargetId);
    }
  }, [deleteTargetId, deleteResultMutation]);

  // Format date for display
  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleString('it-IT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  // Format cost
  const formatCost = (cost: number) => {
    return `$${cost.toFixed(4)}`;
  };

  // Calculate pagination
  const totalCount = resultsData?.totalCount || 0;
  const currentPage = Math.floor((query.skip || 0) / (query.take || 20)) + 1;
  const totalPages = Math.ceil(totalCount / (query.take || 20));

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Link href="/admin/agents/test">
            <Button variant="ghost" size="sm">
              <ArrowLeft className="h-4 w-4 mr-2" />
              Test Console
            </Button>
          </Link>
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2">
              <History className="h-6 w-6" />
              Cronologia Test Agent
            </h1>
            <p className="text-muted-foreground">
              Visualizza e gestisci i risultati dei test degli agent
            </p>
          </div>
        </div>
        <Button onClick={handleRefresh} variant="outline" size="sm">
          <RefreshCw className="h-4 w-4 mr-2" />
          Aggiorna
        </Button>
      </div>

      {/* Filters Card */}
      <Card>
        <CardHeader>
          <CardTitle className="text-lg flex items-center gap-2">
            <Filter className="h-5 w-5" />
            Filtri
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap items-end gap-4">
            {/* Typology Filter */}
            <div className="space-y-2">
              <Label htmlFor="typology-filter">Tipologia</Label>
              <Input
                id="typology-filter"
                placeholder="ID Tipologia..."
                value={typologyFilter}
                onChange={(e) => setTypologyFilter(e.target.value)}
                className="w-64"
              />
            </div>

            {/* Saved Only Filter */}
            <div className="flex items-center space-x-2 pb-2">
              <Checkbox
                id="saved-only"
                checked={savedOnlyFilter}
                onCheckedChange={(checked) => setSavedOnlyFilter(checked === true)}
              />
              <Label htmlFor="saved-only">Solo salvati</Label>
            </div>

            {/* Results per page */}
            <div className="space-y-2">
              <Label>Risultati per pagina</Label>
              <Select
                value={String(query.take || 20)}
                onValueChange={(value) =>
                  setQuery((prev) => ({ ...prev, take: Number(value), skip: 0 }))
                }
              >
                <SelectTrigger className="w-24">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="10">10</SelectItem>
                  <SelectItem value="20">20</SelectItem>
                  <SelectItem value="50">50</SelectItem>
                  <SelectItem value="100">100</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* Clear Filters */}
            <Button variant="ghost" size="sm" onClick={handleClearFilters}>
              <X className="h-4 w-4 mr-2" />
              Pulisci filtri
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Error Alert */}
      {error && (
        <Alert variant="destructive">
          <AlertDescription>
            Errore nel caricamento dei risultati: {error.message}
          </AlertDescription>
        </Alert>
      )}

      {/* Results Table */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between">
            <span>Risultati ({totalCount})</span>
            {totalPages > 1 && (
              <span className="text-sm font-normal text-muted-foreground">
                Pagina {currentPage} di {totalPages}
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex items-center justify-center py-8">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary" />
            </div>
          ) : resultsData?.items.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground">
              <Search className="h-12 w-12 mx-auto mb-4 opacity-50" />
              <p>Nessun risultato trovato</p>
              {(typologyFilter || savedOnlyFilter) && (
                <p className="text-sm mt-2">
                  Prova a modificare i filtri di ricerca
                </p>
              )}
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Data</TableHead>
                    <TableHead>Query</TableHead>
                    <TableHead>Modello</TableHead>
                    <TableHead>Confidenza</TableHead>
                    <TableHead>Token</TableHead>
                    <TableHead>Latenza</TableHead>
                    <TableHead>Costo</TableHead>
                    <TableHead>Salvato</TableHead>
                    <TableHead className="text-right">Azioni</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {resultsData?.items.map((result) => (
                    <TableRow key={result.id}>
                      <TableCell className="whitespace-nowrap">
                        <div className="flex items-center gap-2">
                          <Calendar className="h-4 w-4 text-muted-foreground" />
                          {formatDate(result.executedAt)}
                        </div>
                      </TableCell>
                      <TableCell className="max-w-[200px] truncate" title={result.query}>
                        {result.query}
                      </TableCell>
                      <TableCell>
                        <Badge variant="outline">{result.modelUsed}</Badge>
                      </TableCell>
                      <TableCell>
                        <ConfidenceBadge confidence={result.confidenceScore} />
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1">
                          <Cpu className="h-3 w-3 text-muted-foreground" />
                          {result.tokensUsed.toLocaleString()}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1">
                          <Zap className="h-3 w-3 text-muted-foreground" />
                          {result.latencyMs}ms
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1">
                          <DollarSign className="h-3 w-3 text-muted-foreground" />
                          {formatCost(result.costEstimate)}
                        </div>
                      </TableCell>
                      <TableCell>
                        {result.isSaved ? (
                          <Badge variant="default">Salvato</Badge>
                        ) : (
                          <Badge variant="secondary">Non salvato</Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex items-center justify-end gap-2">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleViewDetail(result.id)}
                          >
                            <Eye className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleDeleteClick(result.id)}
                            className="text-destructive hover:text-destructive"
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="flex items-center justify-center gap-2 mt-4">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => handlePageChange(Math.max(0, (query.skip || 0) - (query.take || 20)))}
                    disabled={(query.skip || 0) === 0}
                  >
                    Precedente
                  </Button>
                  <span className="text-sm text-muted-foreground">
                    {currentPage} / {totalPages}
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => handlePageChange((query.skip || 0) + (query.take || 20))}
                    disabled={currentPage >= totalPages}
                  >
                    Successiva
                  </Button>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>

      {/* Detail Dialog */}
      <Dialog open={!!selectedResultId} onOpenChange={() => setSelectedResultId(null)}>
        <DialogContent className="max-w-3xl max-h-[80vh]">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <MessageSquare className="h-5 w-5" />
              Dettaglio Risultato Test
            </DialogTitle>
            <DialogDescription>
              Visualizza tutti i dettagli del risultato del test
            </DialogDescription>
          </DialogHeader>

          {isLoadingDetail ? (
            <div className="flex items-center justify-center py-8">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary" />
            </div>
          ) : selectedResult ? (
            <ScrollArea className="max-h-[60vh]">
              <div className="space-y-6 p-1">
                {/* Metadata Grid */}
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                  <div className="space-y-1">
                    <Label className="text-muted-foreground">Data</Label>
                    <p className="font-medium">{formatDate(selectedResult.executedAt)}</p>
                  </div>
                  <div className="space-y-1">
                    <Label className="text-muted-foreground">Modello</Label>
                    <Badge variant="outline">{selectedResult.modelUsed}</Badge>
                  </div>
                  <div className="space-y-1">
                    <Label className="text-muted-foreground">Confidenza</Label>
                    <ConfidenceBadge confidence={selectedResult.confidenceScore} />
                  </div>
                  <div className="space-y-1">
                    <Label className="text-muted-foreground">Stato</Label>
                    {selectedResult.isSaved ? (
                      <Badge variant="default">Salvato</Badge>
                    ) : (
                      <Badge variant="secondary">Non salvato</Badge>
                    )}
                  </div>
                </div>

                {/* Metrics Grid */}
                <div className="grid grid-cols-3 gap-4">
                  <Card>
                    <CardContent className="pt-4">
                      <div className="flex items-center gap-2">
                        <Cpu className="h-4 w-4 text-muted-foreground" />
                        <span className="text-sm text-muted-foreground">Token</span>
                      </div>
                      <p className="text-2xl font-bold">
                        {selectedResult.tokensUsed.toLocaleString()}
                      </p>
                    </CardContent>
                  </Card>
                  <Card>
                    <CardContent className="pt-4">
                      <div className="flex items-center gap-2">
                        <Clock className="h-4 w-4 text-muted-foreground" />
                        <span className="text-sm text-muted-foreground">Latenza</span>
                      </div>
                      <p className="text-2xl font-bold">{selectedResult.latencyMs}ms</p>
                    </CardContent>
                  </Card>
                  <Card>
                    <CardContent className="pt-4">
                      <div className="flex items-center gap-2">
                        <DollarSign className="h-4 w-4 text-muted-foreground" />
                        <span className="text-sm text-muted-foreground">Costo</span>
                      </div>
                      <p className="text-2xl font-bold">
                        {formatCost(selectedResult.costEstimate)}
                      </p>
                    </CardContent>
                  </Card>
                </div>

                {/* Strategy Override */}
                {selectedResult.strategyOverride && (
                  <div className="space-y-2">
                    <Label className="text-muted-foreground">Strategia Override</Label>
                    <Badge variant="outline">{selectedResult.strategyOverride}</Badge>
                  </div>
                )}

                {/* Query */}
                <div className="space-y-2">
                  <Label className="text-muted-foreground">Query</Label>
                  <Card>
                    <CardContent className="pt-4">
                      <p className="whitespace-pre-wrap">{selectedResult.query}</p>
                    </CardContent>
                  </Card>
                </div>

                {/* Response */}
                <div className="space-y-2">
                  <Label className="text-muted-foreground">Risposta</Label>
                  <Card>
                    <CardContent className="pt-4">
                      <p className="whitespace-pre-wrap">{selectedResult.response}</p>
                    </CardContent>
                  </Card>
                </div>

                {/* Citations */}
                {selectedResult.citationsJson && (
                  <div className="space-y-2">
                    <Label className="text-muted-foreground">Citazioni</Label>
                    <Card>
                      <CardContent className="pt-4">
                        <pre className="text-xs overflow-x-auto">
                          {JSON.stringify(JSON.parse(selectedResult.citationsJson), null, 2)}
                        </pre>
                      </CardContent>
                    </Card>
                  </div>
                )}

                {/* Notes */}
                {selectedResult.notes && (
                  <div className="space-y-2">
                    <Label className="text-muted-foreground">Note</Label>
                    <Card>
                      <CardContent className="pt-4">
                        <p className="whitespace-pre-wrap">{selectedResult.notes}</p>
                      </CardContent>
                    </Card>
                  </div>
                )}
              </div>
            </ScrollArea>
          ) : (
            <div className="text-center py-8 text-muted-foreground">
              Risultato non trovato
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <AlertDialog open={!!deleteTargetId} onOpenChange={() => setDeleteTargetId(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Conferma eliminazione</AlertDialogTitle>
            <AlertDialogDescription>
              Sei sicuro di voler eliminare questo risultato? Questa azione non può essere annullata.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Annulla</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {deleteResultMutation.isPending ? 'Eliminazione...' : 'Elimina'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
